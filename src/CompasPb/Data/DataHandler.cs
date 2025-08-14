using System;
using System.Collections;
using System.Linq;
using CompasPb.Data;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace CompasPb.Data
{
    /// <summary>
    /// Handles data serialization and packaging operations for Protocol Buffer messages.
    /// </summary>
    public class DataHandler
    {
        #region Pack Operations
        /// <summary>
        /// Packs the given object into a byte array.
        /// </summary>
        /// <param name="data">The AnyData to pack.</param>
        /// <returns>A byte array representing the packed data.</returns>
        public byte[] PackAsBytes(AnyData data)
        {
            MessageData messageData = new MessageData { Data = data };
            byte[] dataBytes = messageData.ToByteArray();
            return dataBytes;
        }

        /// <summary>
        /// Packs the given object into an AnyData.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public AnyData PackAsAnyData(object obj)
        {
            return obj switch
            {
                null => throw new ArgumentNullException(
                    nameof(obj),
                    "Object to pack cannot be null."
                ),

                // IMessage like FrameData, VectorData
                IMessage message => new AnyData { Data = Any.Pack(message) },

                // List
                IEnumerable items when obj is not string => PackAnyData(PackList(items)),

                // Dictionary
                IEnumerable<KeyValuePair<string, object>> dict when obj is not string =>
                    PackAnyData(PackDict(dict.ToDictionary(kv => kv.Key, kv => kv.Value))),

                // Primitive types
                _ => PackAnyData(PackPrimitiveData(obj)),
                // addd more types as needed
            };
        }

        private AnyData PackAnyData<T>(T obj)
            where T : IMessage<T>
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Object to pack cannot be null.");
            }
            Any anyData = Any.Pack(obj);
            return new AnyData { Data = anyData };
        }

        private ListData PackList(IEnumerable items)
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

        private DictData PackDict(Dictionary<string, object> dict)
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

        private PrimitiveData PackPrimitiveData(object value)
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
        #endregion


        #region Unpack Operations
        public AnyData UnpackAsAnyData(byte[] data)
        {
            MessageData messageData = MessageData.Parser.ParseFrom(data);
            return messageData.Data;
        }

        public object? UnpackAnyData(AnyData data, System.Type? dataType = null)
        {
            if (data.Data == null)
            {
                throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");
            }
            // Auto-detect type if not provided
            dataType ??= TryToGetType(data);
            if (dataType == null)
                return null;

            // Use TryUnpack<T> which returns bool and has out parameter
            // use reflection to find the TryUnpack method
            var tryUnpackMethod = data
                .Data.GetType()
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "TryUnpack"
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 1
                );

            if (tryUnpackMethod != null)
            {
                var genericMethod = tryUnpackMethod.MakeGenericMethod(dataType);
                var parameters = new object?[] { null };
                var success = (bool)genericMethod.Invoke(data.Data, parameters);

                if (success && parameters[0] != null)
                {
                    if (parameters[0] is PrimitiveData primitiveData)
                    {
                        return UnpackPrimitiveData(data);
                    }
                    return parameters[0];
                }
            }
            return null;
        }

        // helper method
        // Just a experimental method for test now
        public System.Type? TryToGetType(AnyData data)
        {
            if (data?.Data == null)
                return null;

            string typeUrl = data.Data.TypeUrl;
            if (string.IsNullOrEmpty(typeUrl))
                return null;
            string typeName = typeUrl.Substring("type.googleapis.com/".Length);

            // use reflection to find the type
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // First try exact match with full type name
                var type = assembly.GetType(typeName);
                if (type != null && typeof(IMessage).IsAssignableFrom(type))
                    return type;

                // If not found, try searching by simple name 
                string simpleName = typeName.Contains('.') ? typeName.Split('.').Last() : typeName;

                type = assembly
                    .GetTypes()
                    .FirstOrDefault(t =>
                        t.Name == simpleName && typeof(IMessage).IsAssignableFrom(t)
                    );

                if (type != null)
                    return type;
            }
            return null;
        }

        public IEnumerable<CompasPb.Data.AnyData> UnpackListData(AnyData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "ListData to unpack cannot be null.");
            }
            _ = data.Data.TryUnpack<ListData>(out ListData listData);
            return listData.Data;
        }

        public IEnumerable<KeyValuePair<string, CompasPb.Data.AnyData>> UnpackDictData(AnyData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "DictData to unpack cannot be null.");
            }
            _ = data.Data.TryUnpack<DictData>(out DictData dictData);
            return dictData.Data;
        }

        private object? UnpackPrimitiveData(AnyData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Value to pack cannot be null.");
            }

            data.Data.TryUnpack<PrimitiveData>(out PrimitiveData primitiveData);
            return primitiveData.DataCase switch
            {
                PrimitiveData.DataOneofCase.Int => primitiveData.Int,
                PrimitiveData.DataOneofCase.Float => primitiveData.Float,
                PrimitiveData.DataOneofCase.Str => primitiveData.Str,
                PrimitiveData.DataOneofCase.Bool => primitiveData.Bool,
                PrimitiveData.DataOneofCase.Bytes => primitiveData.Bytes.ToByteArray(),
                PrimitiveData.DataOneofCase.None => null,
                _ => throw new ArgumentException($"Unknown data case: {primitiveData.DataCase}"),
            };
        }
        #endregion
    }
}
