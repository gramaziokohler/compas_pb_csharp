using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;

namespace CompasPb.Data
{

  public static class Registry
  {
    private static readonly Dictionary<string, System.Type> _protoRegistry = new();

    static Registry()
    {
      RegisterAllTypes();
    }

    private static void RegisterAllTypes()
    {
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
  }
}