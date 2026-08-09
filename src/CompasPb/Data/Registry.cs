using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
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
        initialized = true;
    }

    private static void RegisterAllTypes()
    {
        // register all IMessage types in the assembly
        var types = typeof(Registry)
            .Assembly.GetTypes()
            .Where(t => typeof(IMessage).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

        foreach (var type in types)
        {
            ProtoRegistry[type.Name] = type;
        }
    }

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

        // Handle full type URL
        if (typeUrl.StartsWith("type.googleapis.com/"))
        {
            typeUrl = typeUrl.Substring("type.googleapis.com/".Length);
        }

        // Try direct lookup first (full name like "compas_pb.data.FrameData")
        if (ProtoRegistry.TryGetValue(typeUrl, out var type))
        {
            return type;
        }

        // Try simple name lookup (just "FrameData")
        string simpleName = typeUrl.Contains('.') ? typeUrl.Split('.').Last() : typeUrl;
        if (ProtoRegistry.TryGetValue(simpleName, out type))
        {
            return type;
        }

        // Fallback: search by simple type name or full name
        return ProtoRegistry.Values.FirstOrDefault(t =>
            t.Name == simpleName || t.FullName == typeUrl
        );
    }

    private static void RegisterType<T>()
        where T : IMessage<T>
    {
        var type = typeof(T);
        var typeUrl = $"type.googleapis.com/{type.FullName}";
        ProtoRegistry.TryAdd(typeUrl, type);
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

    public static object? UnpackAs(Google.Protobuf.WellKnownTypes.Any any, System.Type targetType)
    {
        if (any == null || targetType == null)
        {
            return null;
        }

        return _unpackDelegates.TryGetValue(targetType, out var fn) ? fn(any) : null;
    }
}
