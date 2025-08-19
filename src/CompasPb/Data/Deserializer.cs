using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data
{
  public static class Deserializer
  {
    /// <summary>
    /// Unpacks a byte array into an AnyData.
    /// </summary>
    public static AnyData UnpackBytes(byte[] data)
    {
      MessageData messageData = MessageData.Parser.ParseFrom(data);
      return messageData.Data;
    }

    /// <summary>
    /// Unpacks the given AnyData into an object.
    /// </summary>
    public static object? UnpackAnyData(AnyData data, System.Type? dataType = null)
    {
      if (data?.Data == null)
        throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");

      dataType ??= GetType(data);
      if (dataType == null)
        return null;

      return dataType.Name switch
      {
        nameof(ListData) => UnpackListData(data),
        nameof(DictData) => UnpackDictData(data),
        nameof(PrimitiveData) => UnpackPrimitiveData(data),
        _ when typeof(IMessage).IsAssignableFrom(dataType) => data.Data.UnpackAs(dataType),
        // handle as dictionary
        _ => throw new ArgumentException($"Unsupported type: {dataType}. Supported types are IMessage, ListData, DictData, and PrimitiveData.")
      };
    }
    public static T? Unpack<T>(AnyData data) where T : class, IMessage<T>, new()
    {
        if (data?.Data == null)
            throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");
        
        return data.Data.TryUnpack<T>(out T result) ? result : null;
    }

    public static System.Type? GetType(AnyData data)
    {
      if (data?.Data == null)
        return null;
      string typeUrl = data.Data.TypeUrl;
      if (string.IsNullOrEmpty(typeUrl))
        return null;
      return Registry.GetType(typeUrl);
    }

    private static IEnumerable<object?> UnpackListData(AnyData data)
    {
      if (data == null)
        throw new ArgumentNullException(nameof(data), "ListData to unpack cannot be null.");

      if (!data.Data.TryUnpack<ListData>(out ListData listData))
        throw new InvalidOperationException("Failed to unpack as ListData.");

      var result = new List<object?>();
      foreach (var item in listData.Data)
      {
        result.Add(UnpackAnyData(item));
      }
      return result;
    }

    private static Dictionary<string, object?> UnpackDictData(AnyData data)
    {
      if (data == null)
        throw new ArgumentNullException(nameof(data), "DictData to unpack cannot be null.");

      if (!data.Data.TryUnpack<DictData>(out DictData dictData))
        throw new InvalidOperationException("Failed to unpack as DictData.");

      var result = new Dictionary<string, object?>();
      foreach (var kvp in dictData.Data)
      {
        result[kvp.Key] = UnpackAnyData(kvp.Value);
      }
      return result;
    }

    private static object? UnpackPrimitiveData(AnyData data)
    {
      if (data == null)
        throw new ArgumentNullException(nameof(data), "PrimitiveData to unpack cannot be null.");

      if (!data.Data.TryUnpack<PrimitiveData>(out PrimitiveData primitiveData))
        throw new InvalidOperationException("Failed to unpack as PrimitiveData.");

      return primitiveData.DataCase switch
      {
        PrimitiveData.DataOneofCase.Int => primitiveData.Int,
        PrimitiveData.DataOneofCase.Float => primitiveData.Float,
        PrimitiveData.DataOneofCase.Str => primitiveData.Str,
        PrimitiveData.DataOneofCase.Bool => primitiveData.Bool,
        PrimitiveData.DataOneofCase.Bytes => primitiveData.Bytes.ToByteArray(),
        PrimitiveData.DataOneofCase.None => null,
        _ => throw new ArgumentException($"Unknown primitive data case: {primitiveData.DataCase}"),
      };
    }

    private static object? UnpackAs(this Any anyData, System.Type targetType)
    {
      if (anyData == null || targetType == null)
        return null;

      var method = typeof(Any).GetMethod("TryUnpack", new System.Type[0])
                              ?.MakeGenericMethod(targetType);
      if (method == null)
        return null;

      var parameters = new object?[] { null };
      var result = method.Invoke(anyData, parameters);
      var success = result != null && (bool)result;
      return success ? parameters[0] : null;
    }
  }
}