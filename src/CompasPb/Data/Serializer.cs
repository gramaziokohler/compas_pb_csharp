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
        public const string CurrentVersion = "0.1.0";
        /// <summary>
        /// Packs the given object into a byte array.
        /// </summary>
        /// <returns></returns>
        public static byte[] PackAsBytes(AnyData data)
        {
            MessageData messageData = new MessageData { Data = data, Version = CurrentVersion };
            byte[] dataBytes = messageData.ToByteArray();
            return dataBytes;
        }

        /// <summary>
        /// Packs the given object into an AnyData.
        /// </summary>
        /// <returns></returns>
        public static AnyData PackAsAnyData(object obj)
        {
            return obj switch
            {
                null => throw new ArgumentNullException(
                    nameof(obj),
                    "Object to pack cannot be null."),

                // IMessage like FrameData, VectorData, ...
                IMessage message => new AnyData { Message = Any.Pack(message) },

                // List
                IEnumerable items when obj is not string => PackAnyData(PackList(items)),

                // Dictionary
                IEnumerable<KeyValuePair<string, object>> dict when obj is not string =>
                    PackAnyData(PackDict(dict.ToDictionary(kv => kv.Key, kv => kv.Value))),

                // Primitive types
                int or float or string or bool or byte => PackAnyData(PackPrimitiveData(obj)),

                _ => throw new ArgumentException(
                    $"Unsupported type: {obj.GetType()}. Supported types are IMessage, IEnumerable, Dictionary, and primitive types."),
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
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), "Value to pack cannot be null.");
            }

            return value switch
            {
                int i => new AnyData { Value = Value.ForNumber(i) },
                float f => new AnyData { Value = Value.ForNumber(f) },
                double f => new AnyData { Value = Value.ForNumber(f) },
                string s => new AnyData { Value = Value.ForString(s) },
                bool b => new AnyData { Value = Value.ForBool(b) },
                // Serialize byte arrays as Base64 strings, fina a better way.
                byte[] bytes => new AnyData { Value = Value.ForString(Convert.ToBase64String(bytes)) },
                byte byt => new AnyData { Value = Value.ForString(Convert.ToBase64String(new[] { byt })) },
                null => new AnyData { Value = Value.ForNull() },
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
                dictData.Items.Add(kvp.Key, packedValue);
            }

            return dictData;
        }
    }
}
