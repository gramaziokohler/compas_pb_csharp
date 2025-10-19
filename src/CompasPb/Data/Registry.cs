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
        private static readonly ConcurrentDictionary<string, System.Type> _protoRegistry = new();
        private static bool _initialized = false;

        static Registry()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            RegisterAllTypes();
            _initialized = true;
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
                _protoRegistry[type.Name] = type;
            }
        }

        public static IEnumerable<Type> GetRegisteredTypes()
        {
            return _protoRegistry.Values;
        }

        public static System.Type? GetType(string typeUrl)
        {
            if (string.IsNullOrEmpty(typeUrl))
                return null;

            // Handle full type URL
            if (typeUrl.StartsWith("type.googleapis.com/"))
            {
                typeUrl = typeUrl.Substring("type.googleapis.com/".Length);
            }

            // Try direct lookup first (full name like "compas_pb.data.FrameData")
            if (_protoRegistry.TryGetValue(typeUrl, out var type))
                return type;

            // Try simple name lookup (just "FrameData")
            string simpleName = typeUrl.Contains('.') ? typeUrl.Split('.').Last() : typeUrl;
            if (_protoRegistry.TryGetValue(simpleName, out type))
                return type;

            // Fallback: search by simple type name or full name
            return _protoRegistry.Values
                .FirstOrDefault(t => t.Name == simpleName || t.FullName == typeUrl);
        }

        private static void RegisterType<T>() where T : IMessage<T>
        {
            var type = typeof(T);
            var typeUrl = $"type.googleapis.com/{type.FullName}";
            _protoRegistry.TryAdd(typeUrl, type);
        }
    }
}
