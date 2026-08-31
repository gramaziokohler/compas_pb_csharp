using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

/// <summary>
/// Maps application types to protobuf messages and protobuf full names back to objects.
/// </summary>
/// <remarks>
/// Domain packages should register conversion functions during their own startup. The model
/// types do not need to implement a CompasPb-specific interface.
/// </remarks>
public static class Registry
{
    private sealed class ProtobufRegistration
    {
        public ProtobufRegistration(
            System.Type messageType,
            MessageDescriptor descriptor,
            Func<ByteString, IMessage> parser,
            Func<IMessage, object?> deserializer
        )
        {
            MessageType = messageType;
            Descriptor = descriptor;
            Parser = parser;
            Deserializer = deserializer;
        }

        public System.Type MessageType { get; }

        public MessageDescriptor Descriptor { get; }

        public Func<ByteString, IMessage> Parser { get; }

        public Func<IMessage, object?> Deserializer { get; }
    }

    private sealed class FallbackRegistration
    {
        public FallbackRegistration(string dtype, Func<object, IDictionary> serializer)
        {
            Dtype = dtype;
            Serializer = serializer;
        }

        public string Dtype { get; }

        public Func<object, IDictionary> Serializer { get; }
    }

    private sealed class PackMiss
    {
        public PackMiss(System.Type type, int generation)
        {
            Type = type;
            Generation = generation;
        }

        public System.Type Type { get; }

        public int Generation { get; }
    }

    private static readonly ConcurrentDictionary<System.Type, Func<object, IMessage>> Serializers =
        new();

    private static readonly ConcurrentDictionary<string, ProtobufRegistration> ProtobufTypes = new(
        StringComparer.Ordinal
    );

    private static readonly ConcurrentDictionary<
        System.Type,
        FallbackRegistration
    > FallbackSerializers = new();

    private static readonly ConcurrentDictionary<
        string,
        Func<IDictionary<string, object?>, object?>
    > FallbackDeserializers = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, byte> ScannedAssemblies = new(
        StringComparer.Ordinal
    );

    private static readonly ConcurrentDictionary<System.Type, byte> InvokedRegistrars = new();

    // The generation a pack lookup last swept a type at without finding a serializer. Gating on
    // it keeps the sweep to at most once per type per registration, which matters because
    // TryPack runs for every value in a payload, primitives included.
    private static readonly ConcurrentDictionary<System.Type, int> PackMissGenerations = new();

    // The most recent of those, read before the dictionary is. A payload asks about the same
    // handful of types over and over — every element of a coordinate list is a double, and each
    // one is looked up in both pack registries — so this keeps the steady state to a reference
    // compare. One field rather than two so a concurrent replacement cannot pair a type with
    // another type's generation.
    private static PackMiss? LastPackMiss;

    private static readonly ConcurrentQueue<Exception> DiscoveryFailureLog = new();

    // Rebuilt lazily whenever a protobuf type is registered. protobuf-json needs a descriptor
    // for every message that can appear inside an Any, which is what the registry already holds.
    private static TypeRegistry? JsonTypeRegistry;

    // Bumped whenever a registration lands, so a pack miss can tell "nothing has registered
    // since I last looked" from "something did".
    private static int RegistrationGeneration;

    /// <summary>
    /// The failures swallowed while sweeping loaded assemblies for registrations.
    /// </summary>
    /// <remarks>
    /// A sweep runs over assemblies the caller does not own, so it never lets one of them throw:
    /// a plugin with a broken registrar would otherwise stop every assembly after it from
    /// registering, and the first sweep runs from this type's initializer, where an escaping
    /// exception would poison the registry for the rest of the process. The failures land here
    /// instead. Each registrar is claimed before it runs, so a failing one is never retried and
    /// this is the only record of it. <see cref="DiscoverRegistrations(Assembly)"/> still throws:
    /// there the caller named the assembly, so the failure is theirs to handle.
    /// </remarks>
    public static IReadOnlyList<Exception> DiscoveryFailures => DiscoveryFailureLog.ToArray();

    static Registry()
    {
        RegisterAssembly(typeof(Registry).Assembly);

        // Reading assembly-level attributes does not enumerate types, so this stays cheap enough
        // to run before the first pack. It is what lets a referenced package's conversions apply
        // without the host application calling into that package.
        DiscoverRegistrations();

        // Subscribed last, once this initializer has nothing left to do. Subscribing first would
        // cover the assemblies the sweep itself loads, but it would also let a load on another
        // thread run a handler that blocks on this initializer while the sweep holds it — and
        // the sweep loads assemblies of its own. An assembly that slips through that window is
        // picked up by the rediscovery a pack miss triggers instead.
        WatchAssemblyLoads();
    }

    /// <summary>
    /// Registers both directions of a domain-model conversion.
    /// </summary>
    public static void Register<TObject, TMessage>(
        Func<TObject, TMessage> serializer,
        Func<TMessage, TObject> deserializer
    )
        where TMessage : class, IMessage<TMessage>, new()
    {
        RegisterSerializer(serializer);
        RegisterDeserializer(deserializer);
    }

    /// <summary>
    /// Registers a serializer for an application type. Base-class registrations also apply to
    /// derived objects.
    /// </summary>
    public static void RegisterSerializer<TObject, TMessage>(Func<TObject, TMessage> serializer)
        where TMessage : class, IMessage<TMessage>, new()
    {
        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }

        RegisterMessage<TMessage>(message => message, overwriteDeserializer: false);
        Serializers[typeof(TObject)] = value => serializer((TObject)value);
        NoteRegistrationChanged();
    }

    /// <summary>
    /// Registers a deserializer under the protobuf descriptor's fully qualified name.
    /// </summary>
    public static void RegisterDeserializer<TMessage, TObject>(Func<TMessage, TObject> deserializer)
        where TMessage : class, IMessage<TMessage>, new()
    {
        if (deserializer is null)
        {
            throw new ArgumentNullException(nameof(deserializer));
        }

        RegisterMessage<TMessage>(message => deserializer(message), overwriteDeserializer: true);
    }

    /// <summary>
    /// Registers fallback conversion functions for a COMPAS JSON-dump dtype.
    /// </summary>
    public static void RegisterFallback<TObject>(
        string dtype,
        Func<TObject, IDictionary> serializer,
        Func<IDictionary<string, object?>, TObject> deserializer
    )
    {
        if (string.IsNullOrWhiteSpace(dtype))
        {
            throw new ArgumentException("A fallback dtype is required.", nameof(dtype));
        }
        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }
        if (deserializer is null)
        {
            throw new ArgumentNullException(nameof(deserializer));
        }

        FallbackSerializers[typeof(TObject)] = new FallbackRegistration(
            dtype,
            value => serializer((TObject)value)
        );
        FallbackDeserializers[dtype] = values => deserializer(values);
        NoteRegistrationChanged();
    }

    /// <summary>
    /// Registers protobuf message types in an assembly for identity deserialization.
    /// </summary>
    public static void RegisterAssembly(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        string assemblyKey = assembly.FullName ?? assembly.GetName().Name ?? assembly.ToString();
        if (!ScannedAssemblies.TryAdd(assemblyKey, 0))
        {
            return;
        }

        foreach (var type in GetLoadableTypes(assembly))
        {
            try
            {
                RegisterMessageType(type);
            }
            catch (Exception exception)
            {
                // The assembly is already marked scanned, so it is never revisited: a message
                // type whose initializer or descriptor blows up costs us that one type rather
                // than every type declared after it.
                RecordDiscoveryFailure(exception);
            }
        }
    }

    private static void RegisterMessageType(System.Type type)
    {
        if (
            type.IsAbstract
            || !type.IsClass
            || !typeof(IMessage).IsAssignableFrom(type)
            || type.GetConstructor(System.Type.EmptyTypes) is null
        )
        {
            return;
        }

        if (Activator.CreateInstance(type) is not IMessage prototype)
        {
            return;
        }

        RegisterMessage(
            prototype.Descriptor,
            type,
            value => prototype.Descriptor.Parser.ParseFrom(value),
            message => message,
            overwriteDeserializer: false
        );
    }

    /// <summary>
    /// Scans currently loaded assemblies for protobuf messages and for
    /// <see cref="CompasPbRegistrationsAttribute"/> declarations.
    /// </summary>
    public static void DiscoverLoadedAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                RegisterAssembly(assembly);
            }
            catch (Exception exception)
            {
                RecordDiscoveryFailure(exception);
            }
        }

        DiscoverRegistrations();
    }

    /// <summary>
    /// Invokes the registrars declared by <see cref="CompasPbRegistrationsAttribute"/> on every
    /// loaded assembly. Each registrar runs at most once, so this is safe to call again after
    /// more assemblies have loaded.
    /// </summary>
    /// <remarks>
    /// A registrar that throws does not stop the ones after it, in its own assembly or in any
    /// other; its failure is recorded in <see cref="DiscoveryFailures"/> instead.
    /// </remarks>
    public static void DiscoverRegistrations()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var attribute in GetRegistrationAttributes(assembly))
            {
                try
                {
                    InvokeRegistrar(assembly, attribute);
                }
                catch (Exception exception)
                {
                    RecordDiscoveryFailure(exception);
                }
            }
        }
    }

    /// <summary>
    /// Keeps discovery current as the process loads more assemblies.
    /// </summary>
    /// <remarks>
    /// A sweep only ever sees what <see cref="AppDomain.GetAssemblies"/> reports, and the runtime
    /// loads a referenced assembly lazily — on first use of one of its types, not because a
    /// project referenced it. A domain package whose types the host has not touched yet is
    /// therefore invisible to the sweep, and without this its registrar would never run unless
    /// the host called discovery again by hand at exactly the right moment.
    /// </remarks>
    private static void WatchAssemblyLoads()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }
        catch (Exception exception)
        {
            // A host that refuses the subscription still gets the startup sweep and the
            // rediscovery a pack miss triggers.
            RecordDiscoveryFailure(exception);
        }
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        try
        {
            // The attribute read only, never RegisterAssembly: the assembly is still loading
            // here, so enumerating its types would drag its dependencies in early and can throw
            // on an assembly that is perfectly usable once loaded. The IMessage scan stays where
            // it was, on the unpack path, which reaches this assembly through a later sweep.
            DiscoverRegistrations(args.LoadedAssembly);
        }
        catch (Exception exception)
        {
            // This runs on whatever thread happened to load the assembly, with no caller to hand
            // the failure to, so it lands in DiscoveryFailures like any other sweep failure.
            RecordDiscoveryFailure(exception);
        }
    }

    /// <summary>
    /// Invokes the registrars declared by <see cref="CompasPbRegistrationsAttribute"/> on one
    /// assembly. Each registrar runs at most once.
    /// </summary>
    /// <remarks>
    /// Unlike the sweep over every loaded assembly, this throws when a registrar fails: the
    /// caller picked the assembly, so the failure is about code they meant to load.
    /// </remarks>
    public static void DiscoverRegistrations(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        foreach (var attribute in GetRegistrationAttributes(assembly))
        {
            InvokeRegistrar(assembly, attribute);
        }
    }

    private static IEnumerable<CompasPbRegistrationsAttribute> GetRegistrationAttributes(
        Assembly assembly
    )
    {
        try
        {
            return assembly.GetCustomAttributes<CompasPbRegistrationsAttribute>();
        }
        catch (FileNotFoundException)
        {
            // An assembly whose dependencies cannot be resolved cannot carry registrations we
            // could act on either, so skip it rather than breaking unrelated serialization.
            return Array.Empty<CompasPbRegistrationsAttribute>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<CompasPbRegistrationsAttribute>();
        }
    }

    private static void InvokeRegistrar(Assembly assembly, CompasPbRegistrationsAttribute attribute)
    {
        // Claim the registrar before invoking it: a registrar that triggers discovery again must
        // not re-enter itself.
        if (!InvokedRegistrars.TryAdd(attribute.RegistrarType, 0))
        {
            return;
        }

        string methodName = string.IsNullOrWhiteSpace(attribute.MethodName)
            ? CompasPbRegistrationsAttribute.DefaultMethodName
            : attribute.MethodName;

        var method = attribute.RegistrarType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            System.Type.EmptyTypes,
            null
        );

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Assembly '{assembly.GetName().Name}' declares "
                    + $"[assembly: CompasPbRegistrations(typeof({attribute.RegistrarType.Name}))], "
                    + $"but '{attribute.RegistrarType.FullName}' has no public static "
                    + $"parameterless method named '{methodName}'."
            );
        }

        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new InvalidOperationException(
                $"The CompasPb registrar '{attribute.RegistrarType.FullName}.{methodName}' "
                    + "threw while registering conversions.",
                exception.InnerException
            );
        }
    }

    /// <summary>
    /// The protobuf <see cref="TypeRegistry"/> backing the JSON format, covering every message
    /// this registry knows about.
    /// </summary>
    /// <remarks>
    /// protobuf-json cannot read or write an <c>Any</c> field without a descriptor for the
    /// message inside it. The binary path can resolve a type lazily when a lookup misses, but
    /// <see cref="JsonParser"/> and <see cref="JsonFormatter"/> need the whole set up front, so
    /// this scans loaded assemblies before handing the registry over. Python's
    /// <c>MessageToJson</c> gets the same coverage from the default descriptor pool.
    /// </remarks>
    public static TypeRegistry GetJsonTypeRegistry()
    {
        DiscoverLoadedAssemblies();

        var cached = JsonTypeRegistry;
        if (cached is not null)
        {
            return cached;
        }

        var built = TypeRegistry.FromMessages(
            ProtobufTypes.Values.Select(registration => registration.Descriptor)
        );
        JsonTypeRegistry = built;
        return built;
    }

    public static IEnumerable<System.Type> GetRegisteredTypes()
    {
        DiscoverLoadedAssemblies();
        return ProtobufTypes.Values.Select(registration => registration.MessageType).Distinct();
    }

    public static System.Type? GetType(string typeUrl)
    {
        string? protobufName = GetProtobufName(typeUrl);
        if (protobufName is null)
        {
            return null;
        }

        if (!ProtobufTypes.TryGetValue(protobufName, out var registration))
        {
            DiscoverLoadedAssemblies();
            if (!ProtobufTypes.TryGetValue(protobufName, out registration))
            {
                return null;
            }
        }

        return registration?.MessageType;
    }

    public static object? UnpackAs(Any any, System.Type targetType)
    {
        if (any is null || targetType is null)
        {
            return null;
        }

        string? protobufName = GetProtobufName(any.TypeUrl);
        if (protobufName is null)
        {
            return null;
        }

        ProtobufRegistration? registration;
        if (!ProtobufTypes.TryGetValue(protobufName, out registration))
        {
            registration = DiscoverLoadedAssembliesAndFind(protobufName);
        }
        if (registration is null)
        {
            return null;
        }

        if (registration.MessageType != targetType)
        {
            return null;
        }

        return registration.Parser(any.Value);
    }

    internal static bool TryPack(object value, out IMessage? message)
    {
        var serializer = FindForPack(Serializers, value.GetType());
        message = serializer?.Invoke(value);
        return message is not null;
    }

    internal static bool TryPackFallback(object value, out IDictionary? data)
    {
        var registration = FindForPack(FallbackSerializers, value.GetType());
        if (registration is null)
        {
            data = null;
            return false;
        }

        var serialized = registration.Serializer(value);
        var normalized = new Dictionary<string, object?>();
        foreach (DictionaryEntry entry in serialized)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("Fallback dictionary keys cannot be null.");
            }
            normalized[entry.Key.ToString()!] = entry.Value;
        }
        normalized["dtype"] = registration.Dtype;
        data = normalized;
        return true;
    }

    internal static bool TryUnpackFallback(IDictionary<string, object?> data, out object? value)
    {
        if (
            data.TryGetValue("dtype", out var dtypeValue)
            && dtypeValue is string dtype
            && FallbackDeserializers.TryGetValue(dtype, out var deserializer)
        )
        {
            value = deserializer(data);
            return true;
        }

        value = null;
        return false;
    }

    internal static object? Unpack(Any any)
    {
        string? protobufName = GetProtobufName(any.TypeUrl);
        if (protobufName is null)
        {
            throw new NotSupportedException("The protobuf Any message has no type URL.");
        }

        ProtobufRegistration? registration;
        if (!ProtobufTypes.TryGetValue(protobufName, out registration))
        {
            registration = DiscoverLoadedAssembliesAndFind(protobufName);
        }
        if (registration is null)
        {
            throw new NotSupportedException(
                $"No deserializer is registered for protobuf type '{protobufName}'."
            );
        }

        return registration.Deserializer(registration.Parser(any.Value));
    }

    internal static string? GetProtobufName(string typeUrl)
    {
        if (string.IsNullOrWhiteSpace(typeUrl))
        {
            return null;
        }

        int separator = typeUrl.LastIndexOf('/');
        return separator >= 0 ? typeUrl.Substring(separator + 1) : typeUrl;
    }

    private static void RegisterMessage<TMessage>(
        Func<TMessage, object?> deserializer,
        bool overwriteDeserializer
    )
        where TMessage : class, IMessage<TMessage>, new()
    {
        var prototype = new TMessage();
        var parser = new MessageParser<TMessage>(() => new TMessage());
        RegisterMessage(
            prototype.Descriptor,
            typeof(TMessage),
            value => parser.ParseFrom(value),
            message => deserializer((TMessage)message),
            overwriteDeserializer
        );
    }

    private static void RegisterMessage(
        MessageDescriptor descriptor,
        System.Type messageType,
        Func<ByteString, IMessage> parser,
        Func<IMessage, object?> deserializer,
        bool overwriteDeserializer
    )
    {
        string protobufName = descriptor.FullName;
        _ = ProtobufTypes.AddOrUpdate(
            protobufName,
            _ => new ProtobufRegistration(messageType, descriptor, parser, deserializer),
            (_, existing) =>
            {
                if (existing.MessageType != messageType)
                {
                    throw new InvalidOperationException(
                        $"Protobuf type '{protobufName}' is already registered by "
                            + $"'{existing.MessageType.AssemblyQualifiedName}'. Shared compas_pb "
                            + "messages must come from the CompasPb runtime package."
                    );
                }

                return overwriteDeserializer
                    ? new ProtobufRegistration(messageType, descriptor, parser, deserializer)
                    : existing;
            }
        );

        NoteRegistrationChanged();
    }

    private static ProtobufRegistration? DiscoverLoadedAssembliesAndFind(string protobufName)
    {
        DiscoverLoadedAssemblies();
        return ProtobufTypes.TryGetValue(protobufName, out var registration) ? registration : null;
    }

    /// <summary>
    /// Looks a pack conversion up, re-running registrar discovery once if none is registered yet.
    /// </summary>
    /// <remarks>
    /// The unpack path already rediscovers on a miss. The pack path has the same hole and the
    /// worse symptom: a caller holding a domain object whose package registered nothing yet gets
    /// "unsupported type" rather than a failed lookup it could retry. <see cref="OnAssemblyLoad"/>
    /// closes it on every runtime that raises the event; this covers the ones that do not, and
    /// the window before the handler is attached. Only the registrar sweep runs, never
    /// <see cref="DiscoverLoadedAssemblies"/>: nothing but a registrar can add a pack conversion,
    /// so enumerating every loaded assembly's types here would buy nothing.
    /// </remarks>
    private static TValue? FindForPack<TValue>(
        ConcurrentDictionary<System.Type, TValue> registry,
        System.Type type
    )
        where TValue : class
    {
        var found = FindByInheritance(registry, type);
        if (found is not null)
        {
            return found;
        }

        // Both pack registries share the gate: a value is looked up in each one in turn, and one
        // sweep settles the question for both.
        int generation = Volatile.Read(ref RegistrationGeneration);
        var recent = Volatile.Read(ref LastPackMiss);
        if (recent is not null && recent.Type == type && recent.Generation == generation)
        {
            return null;
        }
        if (PackMissGenerations.TryGetValue(type, out int swept) && swept == generation)
        {
            Volatile.Write(ref LastPackMiss, new PackMiss(type, generation));
            return null;
        }

        DiscoverRegistrations();
        found = FindByInheritance(registry, type);
        generation = Volatile.Read(ref RegistrationGeneration);
        PackMissGenerations[type] = generation;
        Volatile.Write(ref LastPackMiss, new PackMiss(type, generation));
        return found;
    }

    private static TValue? FindByInheritance<TValue>(
        ConcurrentDictionary<System.Type, TValue> registry,
        System.Type type
    )
        where TValue : class
    {
        for (System.Type? candidate = type; candidate is not null; candidate = candidate.BaseType)
        {
            if (registry.TryGetValue(candidate, out var value))
            {
                return value;
            }
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (registry.TryGetValue(interfaceType, out var value))
            {
                return value;
            }
        }

        return null;
    }

    // A registration landed. The cached JSON registry no longer covers every known descriptor,
    // and a pack lookup that already swept for a type without finding one has a reason to look
    // again.
    private static void NoteRegistrationChanged()
    {
        JsonTypeRegistry = null;
        _ = Interlocked.Increment(ref RegistrationGeneration);
    }

    private static void RecordDiscoveryFailure(Exception exception)
    {
        DiscoveryFailureLog.Enqueue(exception);
    }

    private static IEnumerable<System.Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).Cast<System.Type>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<System.Type>();
        }
    }
}
