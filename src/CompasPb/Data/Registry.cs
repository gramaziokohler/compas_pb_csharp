using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;

namespace CompasPb.Data
{
  public static class Registry
  {
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

    public static System.Type? GetType(string typeName)
    {
      _protoRegistry.TryGetValue(typeName, out var type);
      return type;
    }
    public static void RegisterType<T>() where T : IMessage<T>
    {
      var type = typeof(T);
      var typeUrl = $"type.googleapis.com/{type.FullName}";
      _protoRegistry.TryAdd(typeUrl, type);
    }
  }
}