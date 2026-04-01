using System;
using System.Collections;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data;

public static class Serializer
{
    public static string CurrentVersion = PackageInfo.Version;

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

    private static AnyData PackAnyData<T>(T obj) where T : IMessage<T>
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
            byte[] bytes => new AnyData { Value = Value.ForString(Convert.ToBase64String(bytes)), },
            byte byt => new AnyData { Value = Value.ForString(Convert.ToBase64String(new[] { byt })), },
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
            var packedItem = PackAsAnyData(item);
            listData.Items.Add(packedItem);
        }

        return listData;
    }

    private static DictData PackDict(IDictionary dict)
    {
        if (dict == null)
        {
            throw new ArgumentNullException(nameof(dict), "Dictionary to pack cannot be null.");
        }

        var dictData = new DictData();
        foreach (DictionaryEntry entry in dict)
        {
            if (entry.Key is null)
            {
                throw new ArgumentNullException("Dictionary key cannot be null.");
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
