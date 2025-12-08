using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;

namespace CompasPb.Data
{

    public static class Registry
    {
        // thead-safe dictionary to store registered types
        private static readonly ConcurrentDictionary<string, System.Type> ProtoRegistry = new();
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
            initialized = true;
        }

        private static void RegisterAllTypes()
        {
            // register all IMessage types in the assembly
            var types = typeof(Registry).Assembly
                .GetTypes()
                .Where(t => typeof(IMessage).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.IsClass);

            foreach (var type in types)
            {
                ProtoRegistry[type.Name] = type;
            }
        }

        public static IEnumerable<Type> GetRegisteredTypes()
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
            return ProtoRegistry.Values
                .FirstOrDefault(t => t.Name == simpleName || t.FullName == typeUrl);
        }

        private static void RegisterType<T>()
            where T : IMessage<T>
        {
            var type = typeof(T);
            var typeUrl = $"type.googleapis.com/{type.FullName}";
            ProtoRegistry.TryAdd(typeUrl, type);
        }
    }
}
