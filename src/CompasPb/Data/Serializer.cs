using System;
using System.Collections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

public static class Serializer
{
    public static readonly string CurrentVersion = PackageInfo.Version;

    public static byte[] PackAsBytes(AnyData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        return new MessageData { Data = data, Version = CurrentVersion }.ToByteArray();
    }

    public static AnyData PackAsAnyData(object? obj)
    {
        if (obj is null)
        {
            return new AnyData { Value = Value.ForNull() };
        }

        if (Registry.TryPack(obj, out var registeredMessage))
        {
            return new AnyData { Message = Any.Pack(registeredMessage) };
        }

        if (Registry.TryPackFallback(obj, out var registeredFallback))
        {
            return new AnyData
            {
                Fallback = new FallbackData { Data = PackDict(registeredFallback) },
            };
        }

        return obj switch
        {
            ICompasFallback fallback => new AnyData
            {
                Fallback = new FallbackData { Data = PackDict(fallback.ToFallbackData()) },
            },
            IMessage message => new AnyData { Message = Any.Pack(message) },
            IDictionary dictionary => new AnyData { DictValue = PackDict(dictionary) },
            byte[] bytes => new AnyData
            {
                Value = Value.ForString("base64:" + Convert.ToBase64String(bytes)),
            },
            string text => new AnyData { Value = Value.ForString(text) },
            bool boolean => new AnyData { Value = Value.ForBool(boolean) },
            _ when IsIntegral(obj) => new AnyData { IntValue = Convert.ToInt64(obj) },
            _ when IsFloatingPoint(obj) => new AnyData { DoubleValue = Convert.ToDouble(obj) },
            IEnumerable items => new AnyData { ListValue = PackList(items) },
            _ => throw new ArgumentException(
                $"Unsupported protobuf value type: {obj.GetType()}.",
                nameof(obj)
            ),
        };
    }

    private static bool IsIntegral(object value) =>
        value is sbyte
        || value is byte
        || value is short
        || value is ushort
        || value is int
        || value is uint
        || value is long
        || value is ulong;

    private static bool IsFloatingPoint(object value) =>
        value is float || value is double || value is decimal;

    private static ListData PackList(IEnumerable items)
    {
        var packed = new ListData();
        foreach (var item in items)
        {
            packed.Items.Add(PackAsAnyData(item));
        }
        return packed;
    }

    private static DictData PackDict(IDictionary dictionary)
    {
        if (dictionary is null)
        {
            throw new ArgumentNullException(nameof(dictionary));
        }

        var packed = new DictData();
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("Dictionary keys cannot be null.", nameof(dictionary));
            }
            packed.Items.Add(entry.Key.ToString()!, PackAsAnyData(entry.Value));
        }
        return packed;
    }
}
