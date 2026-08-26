using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

public static class Registry
{
    // thead-safe dictionary to store registered types
    private static readonly ConcurrentDictionary<string, System.Type> ProtoRegistry = new();

    private static readonly Dictionary<
        System.Type,
        Func<Google.Protobuf.WellKnownTypes.Any, object?>
    > _unpackDelegates = new();

    // Custom serializers: domain type → function that returns Any
    private static readonly Dictionary<System.Type, Func<object, Any>> _serializers = new();

    // Custom deserializers: protobuf full name → function that returns domain object
    private static readonly Dictionary<string, Func<Any, object>> _deserializers = new();

    // Cached Google TypeRegistry for JsonFormatter/JsonParser, built from scanned types
    private static TypeRegistry? _jsonTypeRegistry;

    private static bool initialized = false;

    static Registry()
    {
        Initialize();
    }

    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        RegisterAllTypes();
        BuildUnpackDelegates();
        BuildJsonTypeRegistry();
        initialized = true;
    }

    private static void RegisterAllTypes()
    {
        RegisterAssemblyTypes(typeof(Registry).Assembly);
    }

    /// <summary>
    /// Registers all IMessage types from the given assembly.
    /// Call this from your plugin startup to register types from external assemblies.
    /// Safe to call multiple times -- already-registered types are overwritten idempotently.
    /// </summary>
    /// <example>
    /// <code>
    /// // At startup, register types from your domain package
    /// Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
    /// </code>
    /// </example>
    public static void RegisterAssembly(System.Reflection.Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        RegisterAssemblyTypes(assembly);
        BuildUnpackDelegates();
        BuildJsonTypeRegistry();
    }

    private static void RegisterAssemblyTypes(Assembly assembly)
    {
        var types = assembly
            .GetTypes()
            .Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

        foreach (var type in types)
        {
            // Register by simple class name (backward compat)
            ProtoRegistry[type.Name] = type;

            // Register by full protobuf name via the Descriptor property
            var descriptorProp = type.GetProperty(
                "Descriptor",
                BindingFlags.Public | BindingFlags.Static
            );
            if (descriptorProp?.GetValue(null) is MessageDescriptor descriptor)
            {
                ProtoRegistry[descriptor.FullName] = type;
            }
        }
    }

    /// <summary>
    /// Registers a custom serializer for a domain type.
    /// When <c>Pack</c> encounters an object of type <typeparamref name="TDomain"/>,
    /// it calls this function instead of requiring <c>IMessage</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// Registry.RegisterSerializer&lt;Plane&gt;(plane =>
    ///     Any.Pack(new FrameData { Point = ... }));
    /// </code>
    /// </example>
    public static void RegisterSerializer<TDomain>(Func<TDomain, Any> serializer)
    {
        _serializers[typeof(TDomain)] = obj => serializer((TDomain)obj);
    }

    /// <summary>
    /// Registers a custom deserializer for a protobuf type.
    /// When <c>Unpack</c> encounters a message with the given protobuf name,
    /// it calls this function to produce a domain object instead of the raw <c>IMessage</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// Registry.RegisterDeserializer("compas_pb.data.FrameData", any =>
    /// {
    ///     var frame = any.Unpack&lt;FrameData&gt;();
    ///     return new Plane(frame.Point.X, frame.Point.Y, frame.Point.Z);
    /// });
    /// </code>
    /// </example>
    public static void RegisterDeserializer(string protoFullName, Func<Any, object> deserializer)
    {
        _deserializers[protoFullName] = deserializer;
    }

    internal static AnyData? TrySerialize(object obj)
    {
        if (_serializers.TryGetValue(obj.GetType(), out var fn))
        {
            return new AnyData { Message = fn(obj) };
        }
        return null;
    }

    /// <summary>
    /// Removes a custom serializer for a domain type.
    /// </summary>
    public static void UnregisterSerializer<TDomain>()
    {
        _serializers.Remove(typeof(TDomain));
    }

    /// <summary>
    /// Removes a custom deserializer for a protobuf type.
    /// </summary>
    public static void UnregisterDeserializer(string protoFullName)
    {
        _deserializers.Remove(protoFullName);
    }

    internal static object? TryDeserialize(Any message)
    {
        var fullName = message.TypeUrl.StartsWith("type.googleapis.com/")
            ? message.TypeUrl.Substring("type.googleapis.com/".Length)
            : message.TypeUrl;

        if (_deserializers.TryGetValue(fullName, out var fn))
        {
            return fn(message);
        }
        return null;
    }

    public static TypeRegistry GetJsonTypeRegistry() =>
        _jsonTypeRegistry ?? throw new InvalidOperationException("Registry not initialized.");

    public static IEnumerable<System.Type> GetRegisteredTypes()
    {
        return ProtoRegistry.Values;
    }

    public static System.Type? GetType(string typeUrl)
    {
        if (string.IsNullOrEmpty(typeUrl))
        {
            return null;
        }

        // Strip the type.googleapis.com/ prefix if present
        var fullName = typeUrl.StartsWith("type.googleapis.com/")
            ? typeUrl.Substring("type.googleapis.com/".Length)
            : typeUrl;

        // Try direct lookup by full protobuf name (e.g. "compas_pb.data.PointData")
        if (ProtoRegistry.TryGetValue(fullName, out var type))
        {
            return type;
        }

        // Try simple name lookup (e.g. "PointData") for backward compatibility
        string simpleName =
            fullName.Contains("/") ? fullName.Substring(fullName.LastIndexOf('/') + 1)
            : fullName.Contains(".") ? fullName.Substring(fullName.LastIndexOf('.') + 1)
            : fullName;

        if (ProtoRegistry.TryGetValue(simpleName, out type))
        {
            return type;
        }

        return null;
    }

    private static void BuildUnpackDelegates()
    {
        var tryUnpackMethod = typeof(Google.Protobuf.WellKnownTypes.Any)
            .GetMethods()
            .First(m =>
                m.Name == "TryUnpack"
                && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1
            );

        foreach (var type in ProtoRegistry.Values)
        {
            var closedMethod = tryUnpackMethod.MakeGenericMethod(type);
            _unpackDelegates[type] = (any) =>
            {
                var args = new object?[] { null };
                var success = closedMethod.Invoke(any, args) is true;
                return success ? args[0] : null;
            };
        }
    }

    private static void BuildJsonTypeRegistry()
    {
        var descriptors = ProtoRegistry
            .Values.Select(t =>
            {
                var prop = t.GetProperty(
                    "Descriptor",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );
                return prop?.GetValue(null) as MessageDescriptor;
            })
            .Where(d => d is not null)
            .Cast<MessageDescriptor>();

        _jsonTypeRegistry = TypeRegistry.FromMessages(descriptors);
    }

    public static object? UnpackAs(Google.Protobuf.WellKnownTypes.Any any, System.Type targetType)
    {
        if (any == null || targetType == null)
        {
            return null;
        }

        return _unpackDelegates.TryGetValue(targetType, out var fn) ? fn(any) : null;
    }
}
