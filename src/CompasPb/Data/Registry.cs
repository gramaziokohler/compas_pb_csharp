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
        string simpleName = fullName.Contains("/")
            ? fullName.Substring(fullName.LastIndexOf('/') + 1)
            : fullName.Contains(".")
                ? fullName.Substring(fullName.LastIndexOf('.') + 1)
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
