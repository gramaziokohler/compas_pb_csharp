using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

public static class Deserializer
{
    /// <summary>
    /// Unpacks a byte array into an AnyData.
    /// </summary>
    /// <returns></returns>
    public static AnyData UnpackBytes(byte[] data)
    {
        MessageData messageData = MessageData.Parser.ParseFrom(data);
        GetVersion(messageData.Version);
        return messageData.Data;
    }

    /// <summary>
    /// Unpacks the given AnyData into an object. Dispatch is driven by the
    /// generated DataOneofCase enum (compile-time exhaustive over the protobuf
    /// oneof). The optional dataType overrides Registry lookup for the Message case.
    /// </summary>
    /// <returns></returns>
    public static object? UnpackAnyData(AnyData data, System.Type? dataType = null)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");
        }

        return data.DataCase switch
        {
            AnyData.DataOneofCase.Value => UnpackPrimitive(data.Value),
            AnyData.DataOneofCase.Message => UnpackMessage(data.Message, dataType),
            AnyData.DataOneofCase.Fallback => UnpackFallback(data.Fallback),
            AnyData.DataOneofCase.None => null,
            _ => null,
        };
    }

    /// <summary>
    /// Unpacks the given AnyData into an object of type T.
    /// </summary>
    /// <returns></returns>
    public static T? Unpack<T>(AnyData data)
        where T : class, IMessage<T>, new()
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");
        }

        return data.Message != null && data.Message.TryUnpack<T>(out T result) ? result : null;
    }

    public static System.Type? GetType(AnyData data)
    {
        if (data?.Message == null)
        {
            return null;
        }

        string typeUrl = data.Message.TypeUrl;
        if (string.IsNullOrEmpty(typeUrl))
        {
            return null;
        }

        return Registry.GetType(typeUrl);
    }

    private static object? UnpackMessage(Any any, System.Type? dataType)
    {
        if (any == null)
        {
            return null;
        }

        // Structured containers: match via generated protobuf descriptor.
        // TryUnpack<T> checks TypeUrl against T's descriptor and returns false on mismatch
        // (no exception), so chaining is safe and avoids reflection at this layer.
        if (any.TryUnpack<ListData>(out var listData))
        {
            return UnpackListItems(listData);
        }
        if (any.TryUnpack<DictData>(out var dictData))
        {
            return UnpackDictItems(dictData);
        }

        // Plain IMessage: caller-provided type wins, else Registry lookup.
        dataType ??= Registry.GetType(any.TypeUrl);
        return dataType == null ? null : UnpackAs(any, dataType);
    }

    private static List<object?> UnpackListItems(ListData listData)
    {
        var result = new List<object?>(listData.Items.Count);
        foreach (var item in listData.Items)
        {
            result.Add(UnpackAnyData(item));
        }
        return result;
    }

    private static Dictionary<string, object?> UnpackDictItems(DictData dictData)
    {
        var result = new Dictionary<string, object?>(dictData.Items.Count);
        foreach (var kvp in dictData.Items)
        {
            result[kvp.Key] = UnpackAnyData(kvp.Value);
        }
        return result;
    }

    private static Dictionary<string, object?> UnpackFallback(FallbackData fallback)
    {
        return fallback.Data == null
            ? new Dictionary<string, object?>()
            : UnpackDictItems(fallback.Data);
    }

    private static object? UnpackPrimitive(Value value)
    {
        if (value == null)
        {
            return null;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.NullValue => null,
            Value.KindOneofCase.NumberValue => value.NumberValue,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.StringValue => value.StringValue,
            _ => null,
        };
    }

    private static object? UnpackAs(Any anyData, System.Type targetType)
    {
        if (anyData == null || targetType == null)
        {
            return null;
        }

        return Registry.UnpackAs(anyData, targetType);
    }

    private static void GetVersion(string version)
    {
        string currentVersion = PackageInfo.Version;
        if (version != currentVersion)
        {
            Console.WriteLine(
                $"Warning: Version mismatch. Data version: {version}, Current version: {currentVersion}"
            );
        }
    }
}
