using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
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
            Func<ByteString, IMessage> parser,
            Func<IMessage, object?> deserializer
        )
        {
            MessageType = messageType;
            Parser = parser;
            Deserializer = deserializer;
        }

        public System.Type MessageType { get; }

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

    static Registry()
    {
        RegisterAssembly(typeof(Registry).Assembly);
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
            if (
                type.IsAbstract
                || !type.IsClass
                || !typeof(IMessage).IsAssignableFrom(type)
                || type.GetConstructor(System.Type.EmptyTypes) is null
            )
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not IMessage prototype)
            {
                continue;
            }

            RegisterMessage(
                prototype.Descriptor.FullName,
                type,
                value => prototype.Descriptor.Parser.ParseFrom(value),
                message => message,
                overwriteDeserializer: false
            );
        }
    }

    /// <summary>
    /// Scans currently loaded assemblies for protobuf messages. Domain conversion functions
    /// still need to be registered explicitly.
    /// </summary>
    public static void DiscoverLoadedAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            RegisterAssembly(assembly);
        }
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
        var serializer = FindByInheritance(Serializers, value.GetType());
        message = serializer?.Invoke(value);
        return message is not null;
    }

    internal static bool TryPackFallback(object value, out IDictionary? data)
    {
        var registration = FindByInheritance(FallbackSerializers, value.GetType());
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
            prototype.Descriptor.FullName,
            typeof(TMessage),
            value => parser.ParseFrom(value),
            message => deserializer((TMessage)message),
            overwriteDeserializer
        );
    }

    private static void RegisterMessage(
        string protobufName,
        System.Type messageType,
        Func<ByteString, IMessage> parser,
        Func<IMessage, object?> deserializer,
        bool overwriteDeserializer
    )
    {
        _ = ProtobufTypes.AddOrUpdate(
            protobufName,
            _ => new ProtobufRegistration(messageType, parser, deserializer),
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
                    ? new ProtobufRegistration(messageType, parser, deserializer)
                    : existing;
            }
        );
    }

    private static ProtobufRegistration? DiscoverLoadedAssembliesAndFind(string protobufName)
    {
        DiscoverLoadedAssemblies();
        return ProtobufTypes.TryGetValue(protobufName, out var registration) ? registration : null;
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
