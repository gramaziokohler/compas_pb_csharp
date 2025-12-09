using System;
using System.Collections.Generic;
using System.IO;
using CompasPb.Data;

namespace CompasPb.UserCase
{

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Example: ");

            // Pack Data
            var nestedList = new List<object>()
            {
                new List<int> { 1, 2, 3 },
                new Dictionary<string, object>
                {
                    { "key1", 123 },
                    { "key2", "value2" },
                },
                1,
                new FrameData
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "testFrame",
                    Point = new PointData
                    {
                        X = 1.02F,
                        Y = 2.02F,
                        Z = 3.02F,
                    },
                    Xaxis = new VectorData
                    {
                        X = 1.02F,
                        Y = 0.02F,
                        Z = 0.02F,
                    },
                    Yaxis = new VectorData
                    {
                        X = 0.02F,
                        Y = 1.02F,
                        Z = 0.02F,
                    },
                },
            };

            var packData = Serializer.PackAsBytes(Serializer.PackAsAnyData(nestedList));
            Console.WriteLine($"Packed data: {packData}");

            string filePath = "packedData.bin";
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(packData, 0, packData.Length);
            }

            Console.WriteLine($"Packed data written to {filePath}");

            // Unpack data
            byte[] response = File.ReadAllBytes(filePath);
            AnyData responsedMessage = Deserializer.UnpackBytes(response);

            Console.WriteLine("======= Unpacking Data without given type =======");
            var unpacked = Deserializer.UnpackAnyData(responsedMessage);
            if (unpacked is List<object> unpackedList)
            {
                foreach (var item in unpackedList)
                {
                    Console.WriteLine($" - {item} (Type: {item?.GetType()})");
                }
            }
            else
            {
                Console.WriteLine($"Unpacked data: {unpacked} (Type: {unpacked?.GetType()})");
            }

            // Console.WriteLine("======= Unpacking Data with given type =======");
            // var responesDataType = Deserializer.GetType(responsedMessage);
            // var unpackedGivenType = Deserializer.Unpack<ListData>(responsedMessage);
            // Console.WriteLine($"Unpacked {responesDataType} : {unpackedGivenType}");
        }
    }
}
