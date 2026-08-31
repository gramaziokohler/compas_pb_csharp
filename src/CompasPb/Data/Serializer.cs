using System;
using System.Collections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

/// <summary>
/// Encodes objects into the compas_pb wire format. Internal: callers go through
/// <see cref="CompasPbSerializer"/>, which is the runtime's single entry point in.
/// </summary>
internal static class Serializer
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

    /// <summary>
    /// Encodes a packed <see cref="AnyData"/> as a protobuf-JSON string.
    /// </summary>
    /// <remarks>
    /// Mirrors upstream <c>pb_dump_json</c>, which calls <c>MessageToJson</c> with its defaults.
    /// Those defaults omit fields at their default value, so this leaves
    /// <c>FormatDefaultValues</c> off: the JSON a C# runtime writes stays comparable with the
    /// JSON Python writes for the same object. Fields set through a <c>oneof</c> — which is how
    /// every <see cref="AnyData"/> arm is encoded — are written even when they hold a default
    /// value, so a zero or an empty string still survives the round trip.
    /// </remarks>
    public static string PackAsJson(AnyData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var message = new MessageData { Data = data, Version = CurrentVersion };
        var formatter = new JsonFormatter(
            JsonFormatter.Settings.Default.WithTypeRegistry(Registry.GetJsonTypeRegistry())
        );
        return formatter.Format(message);
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
            // The hint is here because the usual cause is timing, not a missing registration.
            // A package registers its conversions when its assembly loads, and the runtime loads
            // an assembly only when the process first uses one of its types, so a conversion can
            // genuinely be absent at the moment it is needed and present a moment later.
            _ => throw new ArgumentException(
                $"Unsupported protobuf value type: {obj.GetType()}. Nothing is registered to "
                    + "convert it. If a package registers this type, its assembly may not have "
                    + "been loaded yet: call Registry.DiscoverRegistrations() once it is loaded, "
                    + "or register the conversion from application startup.",
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
