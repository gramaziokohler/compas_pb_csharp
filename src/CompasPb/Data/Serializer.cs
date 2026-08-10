using System;
using System.Collections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf.Reflection;

namespace CompasPb.Data;

internal static class Serializer
{
    public static readonly string CurrentVersion = PackageInfo.Version;

    /// <summary>
    /// Packs the given object into a byte array.
    /// </summary>
    /// <returns></returns>
    public static byte[] PackAsBytes(AnyData data)
    {
        var messageData = new MessageData { Data = data, Version = CurrentVersion };
        byte[] dataBytes = messageData.ToByteArray();
        return dataBytes;
    }

    /// <summary>
    /// Packs the given AnyData into a JSON string.
    /// </summary>
    public static string PackAsJson(AnyData data)
    {
        var messageData = new MessageData { Data = data, Version = CurrentVersion };
        var registry = TypeRegistry.FromFiles(
            GeometryReflection.Descriptor,
            MessageReflection.Descriptor,
            DatastructuresReflection.Descriptor
        );
        var formatter = new JsonFormatter(
            new JsonFormatter.Settings(false).WithFormatDefaultValues(true).WithTypeRegistry(registry)
        );
        return formatter.Format(messageData);
    }

    /// <summary>
    /// Packs the given object into an AnyData.
    /// </summary>
    /// <returns></returns>
    public static AnyData PackAsAnyData(object? obj)
    {
        if (obj is null)
        {
            return new AnyData { Value = Value.ForNull() };
        }

        return obj switch
        {
            // IMessage (FrameData, VectorData, ...)
            IMessage message => new AnyData { Message = Any.Pack(message) },
            // Dictionary
            IDictionary dict => PackAnyData(PackDict(dict)),
            // List/ Array
            IEnumerable items when obj is not string && obj is not IDictionary => PackAnyData(
                PackList(items)
            ),
            // Primitive types
            _ when Helper.IsPrimitiveType(obj) => PackPrimitiveData(obj),

            _ => throw new ArgumentNullException(
                $"Unsupported type: {obj.GetType()}"
                    + $"Supported types are IMessage, IEnumerable, Dictionary, and primitive types."
            ),
        };
    }

    private static AnyData PackAnyData<T>(T obj)
        where T : IMessage<T>
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj), "Object to pack cannot be null.");
        }

        Any anyData = Any.Pack(obj);
        return new AnyData { Message = anyData };
    }

    private static AnyData PackPrimitiveData(object value)
    {
        return value switch
        {
            null => new AnyData { Value = Value.ForNull() },
            int i => new AnyData { Value = Value.ForNumber(i) },
            float f => new AnyData { Value = Value.ForNumber(f) },
            double d => new AnyData { Value = Value.ForNumber(d) },
            long l => new AnyData { Value = Value.ForNumber(l) },
            decimal m => new AnyData { Value = Value.ForNumber((double)m) },
            string s => new AnyData { Value = Value.ForString(s) },
            bool b => new AnyData { Value = Value.ForBool(b) },

            // Serialize byte arrays as Base64 strings, fina a better way.
            byte[] bytes => new AnyData { Value = Value.ForString(Convert.ToBase64String(bytes)) },
            byte byt => new AnyData
            {
                Value = Value.ForString(Convert.ToBase64String(new[] { byt })),
            },
            _ => throw new ArgumentException($"Unsupported type: {value.GetType()}"),
        };
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
            AnyData packedItem = PackAsAnyData(item);
            listData.Items.Add(packedItem);
        }

        return listData;
    }

    private static DictData PackDict(IDictionary dict)
    {
        if (dict == null)
        {
            throw new ArgumentException($"Dictionary cannot be null. {nameof(dict)}");
        }

        var dictData = new DictData();
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException($"Dictionary key cannot be null. {nameof(entry.Key)}");
            }
            else
            {
                string key = entry.Key.ToString()!;
                AnyData packedValue = PackAsAnyData(entry.Value);
                dictData.Items.Add(key, packedValue);
            }
        }

        return dictData;
    }
}
