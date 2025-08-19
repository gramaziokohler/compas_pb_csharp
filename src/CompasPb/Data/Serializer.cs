using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data
{
  public static class Serializer
  {
    /// <summary>
    /// Packs the given object into a byte array.
    /// </summary>
    public static byte[] PackAsBytes(AnyData data)
    {
      MessageData messageData = new MessageData { Data = data };
      byte[] dataBytes = messageData.ToByteArray();
      return dataBytes;
    }

    /// <summary>
    /// Packs the given object into an AnyData.
    /// </summary>
    public static AnyData PackAsAnyData(object obj)
    {
      return obj switch
      {
        null => throw new ArgumentNullException(
            nameof(obj),
            "Object to pack cannot be null."
        ),

        // IMessage like FrameData, VectorData, ...
        IMessage message => new AnyData { Data = Any.Pack(message) },
        // List
        IEnumerable items when obj is not string => PackAnyData(PackList(items)),
        // Dictionary
        IEnumerable<KeyValuePair<string, object>> dict when obj is not string =>
            PackAnyData(PackDict(dict.ToDictionary(kv => kv.Key, kv => kv.Value))),
        // Primitive types
        int or float or string or bool or byte => PackAnyData(PackPrimitiveData(obj)),

        _ => throw new ArgumentException(
            $"Unsupported type: {obj.GetType()}. Supported types are IMessage, IEnumerable, Dictionary, and primitive types."
        ),
      };
    }

    private static AnyData PackAnyData<T>(T obj) where T : IMessage<T>
    {
      if (obj == null)
      {
        throw new ArgumentNullException(nameof(obj), "Object to pack cannot be null.");
      }
      Any anyData = Any.Pack(obj);
      return new AnyData { Data = anyData };
    }

    private static ListData PackList(IEnumerable items)
    {
      if (items == null)
      {
        throw new ArgumentNullException(nameof(items), "Items to pack cannot be null.");
      }
      var listData = new ListData();
      foreach (var item in items)
      {
        var packedItem = PackAsAnyData(item);
        listData.Data.Add(packedItem);
      }
      return listData;
    }

    private static DictData PackDict(Dictionary<string, object> dict)
    {
      if (dict == null)
      {
        throw new ArgumentNullException(nameof(dict), "Dictionary to pack cannot be null.");
      }
      var dictData = new DictData();
      foreach (var kvp in dict)
      {
        var packedValue = PackAsAnyData(kvp.Value);
        dictData.Data.Add(kvp.Key, packedValue);
      }
      return dictData;
    }

    // Align with new update with COMPASPB python soon
    private static PrimitiveData PackPrimitiveData(object value)
    {
      if (value == null)
      {
        throw new ArgumentNullException(nameof(value), "Value to pack cannot be null.");
      }
      return value switch
      {
        int i => new PrimitiveData { Int = i },
        float f => new PrimitiveData { Float = f },
        string s => new PrimitiveData { Str = s },
        bool b => new PrimitiveData { Bool = b },
        byte byt => new PrimitiveData { Bytes = ByteString.CopyFrom(new byte[] { byt }) },
        _ => throw new ArgumentException($"Unsupported type: {value.GetType()}"),
      };
    }
  }
}
