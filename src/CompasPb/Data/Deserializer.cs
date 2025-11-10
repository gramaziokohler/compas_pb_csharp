
namespace CompasPb.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;

    public static class Deserializer
    {
        /// <summary>
        /// Unpacks a byte array into an AnyData.
        /// </summary>
        /// <returns></returns>
        public static AnyData UnpackBytes(byte[] data)
        {
            MessageData messageData = MessageData.Parser.ParseFrom(data);
            return messageData.Data;
        }

        /// <summary>
        /// Unpacks the given AnyData into an object.
        /// </summary>
        /// <returns></returns>
        public static object? UnpackAnyData(AnyData data, System.Type? dataType = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "AnyData to unpack cannot be null.");
            }

            dataType ??= GetType(data);
            if (dataType == null)
            {
                return null;
            }

            return dataType.Name switch
            {
                nameof(ListData) => UnpackListData(data),
                nameof(DictData) => UnpackDictData(data),
                _ when typeof(IMessage).IsAssignableFrom(dataType) => UnpackAs(data.Message, dataType),

                // Maybe some fallback handle as dictionary
                _ => throw new ArgumentException($"Unsupported type: {dataType}. Supported types are IMessage, ListData, DictData, and PrimitiveData."),
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

            return data.Message.TryUnpack<T>(out T result) ? result : null;
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

        private static List<object?> UnpackListData(AnyData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "ListData to unpack cannot be null.");
            }

            if (!data.Message.TryUnpack<ListData>(out ListData listData))
            {
                throw new InvalidOperationException("Failed to unpack as ListData.");
            }

            var result = new List<object?>();
            foreach (var item in listData.Items)
            {
                result.Add(UnpackAnyData(item));
            }

            return result;
        }

        private static Dictionary<string, object?> UnpackDictData(AnyData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "DictData to unpack cannot be null.");
            }

            if (!data.Message.TryUnpack<DictData>(out DictData dictData))
            {
                throw new InvalidOperationException("Failed to unpack as DictData.");
            }

            var result = new Dictionary<string, object?>();
            foreach (var kvp in dictData.Items)
            {
                result[kvp.Key] = UnpackAnyData(kvp.Value);
            }

            return result;
        }

        // private static object? UnpackPrimitiveData(AnyData data)
        // {
        //     if (data == null)
        //     {
        //         throw new ArgumentNullException(nameof(data), "PrimitiveData to unpack cannot be null.");
        //     }
        //
        //     if (!data.Data.TryUnpack<PrimitiveData>(out PrimitiveData primitiveData))
        //     {
        //         throw new InvalidOperationException("Failed to unpack as PrimitiveData.");
        //     }
        //
        //     return primitiveData.DataCase switch
        //     {
        //         AnyData.DataOneofCase.Value.Injjj
        //         data .DataOneofCase.Int => primitiveData.Int,
        //         PrimitiveData.DataOneofCase.Float => primitiveData.Float,
        //         PrimitiveData.DataOneofCase.Str => primitiveData.Str,
        //         PrimitiveData.DataOneofCase.Bool => primitiveData.Bool,
        //         PrimitiveData.DataOneofCase.Bytes => primitiveData.Bytes.ToByteArray(),
        //         PrimitiveData.DataOneofCase.None => null,
        //         _ => throw new ArgumentException($"Unknown primitive data case: {primitiveData.DataCase}"),
        //     };
        // }

        private static object? UnpackAs(Any anyData, System.Type targetType)
        {
            if (anyData == null || targetType == null)
            {
                return null;
            }

            try
            {
                var method = typeof(Any).GetMethods()
                  .FirstOrDefault(m =>
                    m.Name == "TryUnpack" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 1)
                  ?.MakeGenericMethod(targetType); // create a generic method for the target type

                if (method == null)
                {
                    return null;
                }

                var parameters = new object?[] { null };
                var result = method.Invoke(anyData, parameters);
                if (result is bool success && success)
                {
                    return parameters[0];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to unpack {targetType.Name}: {ex.Message}");
            }

            return null;
        }
    }
}
