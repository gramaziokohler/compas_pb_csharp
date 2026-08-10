using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

public static class Deserializer
{
    public static AnyData UnpackBytes(byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        var message = MessageData.Parser.ParseFrom(data);
        ValidateVersion(message.Version);
        return message.Data;
    }

    public static object? UnpackAnyData(AnyData data, System.Type? dataType = null)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return data.DataCase switch
        {
            AnyData.DataOneofCase.Value => UnpackValue(data.Value),
            AnyData.DataOneofCase.IntValue => data.IntValue,
            AnyData.DataOneofCase.DoubleValue => data.DoubleValue,
            AnyData.DataOneofCase.DictValue => UnpackDict(data.DictValue),
            AnyData.DataOneofCase.ListValue => UnpackList(data.ListValue),
            AnyData.DataOneofCase.Fallback => UnpackDict(data.Fallback.Data),
            AnyData.DataOneofCase.Message => UnpackMessage(data.Message, dataType),
            AnyData.DataOneofCase.None => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(data),
                data.DataCase,
                "Unknown AnyData variant."
            ),
        };
    }

    public static T? Unpack<T>(AnyData data)
        where T : class, IMessage<T>, new()
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        return data.Message is not null && data.Message.TryUnpack<T>(out var result)
            ? result
            : null;
    }

    public static System.Type? GetType(AnyData data) =>
        data is not null && data.DataCase == AnyData.DataOneofCase.Message
            ? Registry.GetType(data.Message.TypeUrl)
            : null;

    private static object? UnpackMessage(Any message, System.Type? dataType)
    {
        if (message.TryUnpack<ListData>(out var list))
        {
            return UnpackList(list);
        }
        if (message.TryUnpack<DictData>(out var dictionary))
        {
            return UnpackDict(dictionary);
        }

        dataType ??= Registry.GetType(message.TypeUrl);
        return dataType is null ? null : Registry.UnpackAs(message, dataType);
    }

    private static List<object?> UnpackList(ListData data)
    {
        var result = new List<object?>(data.Items.Count);
        foreach (var item in data.Items)
        {
            result.Add(UnpackAnyData(item));
        }
        return result;
    }

    private static Dictionary<string, object?> UnpackDict(DictData data)
    {
        var result = new Dictionary<string, object?>(data.Items.Count);
        foreach (var item in data.Items)
        {
            result[item.Key] = UnpackAnyData(item.Value);
        }
        return result;
    }

    private static object? UnpackValue(Value value) =>
        value.KindCase switch
        {
            Value.KindOneofCase.NullValue => null,
            Value.KindOneofCase.BoolValue => value.BoolValue,
            Value.KindOneofCase.NumberValue => value.NumberValue,
            Value.KindOneofCase.StringValue
                when value.StringValue.StartsWith("base64:", StringComparison.Ordinal) =>
                Convert.FromBase64String(value.StringValue.Substring(7)),
            Value.KindOneofCase.StringValue => value.StringValue,
            _ => throw new InvalidOperationException(
                $"Unsupported protobuf Value kind: {value.KindCase}."
            ),
        };

    private static void ValidateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException(
                $"The protobuf payload has no compas_pb version; reader is {PackageInfo.Version}."
            );
        }

        if (WireCompatibilityKey(version) != WireCompatibilityKey(PackageInfo.Version))
        {
            throw new InvalidOperationException(
                $"Incompatible compas_pb wire format: payload is {version}, reader is {PackageInfo.Version}."
            );
        }
    }

    private static string WireCompatibilityKey(string version)
    {
        var parts = version.Split('.');
        return parts[0] == "0" && parts.Length > 1 ? $"0.{parts[1]}" : parts[0];
    }
}
