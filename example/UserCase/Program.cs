using System;
using System.Collections.Generic;
using System.IO;
using CompasPb;
using CompasPb.Data;

namespace CompasPb.UserCase
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Example: ");

            var serializer = new CompasPbSerializer();

            // ======= Single object =======
            Console.WriteLine("======= Single FrameData =======");
            var frame = new FrameData
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
            };

            byte[] frameBytes = serializer.Pack(frame);
            Console.WriteLine($"Packed FrameData: {frameBytes.Length} bytes");

            // Typed unpack
            FrameData? unpackedFrame = serializer.Unpack<FrameData>(frameBytes);
            Console.WriteLine($"Unpacked FrameData: {unpackedFrame?.Name} ({unpackedFrame?.Point})");

            // Dynamic unpack
            object? dynamicFrame = serializer.Unpack(frameBytes);
            Console.WriteLine($"Dynamic unpack: {dynamicFrame} (Type: {dynamicFrame?.GetType()})");

            // ======= JSON =======
            Console.WriteLine("\n======= JSON =======");
            string json = serializer.PackAsJson(frame);
            Console.WriteLine($"JSON: {json}");

            FrameData? fromJson = serializer.UnpackJson<FrameData>(json);
            Console.WriteLine($"From JSON: {fromJson?.Name} ({fromJson?.Point})");

            // ======= Nested data =======
            Console.WriteLine("\n======= Nested List =======");
            var nestedList = new List<object>()
            {
                new List<int> { 1, 2, 3 },
                new Dictionary<string, object> { { "key1", 123 }, { "key2", "value2" } },
                1,
                frame,
            };

            byte[] listBytes = serializer.Pack(nestedList);
            Console.WriteLine($"Packed nested list: {listBytes.Length} bytes");

            string filePath = "packedData.bin";
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fileStream.Write(listBytes, 0, listBytes.Length);
            }
            Console.WriteLine($"Packed data written to {filePath}");

            // Unpack from file
            byte[] response = File.ReadAllBytes(filePath);
            var unpacked = serializer.Unpack(response);
            if (unpacked is List<object> unpackedList)
            {
                foreach (var item in unpackedList)
                {
                    Console.WriteLine($" - {item} (Type: {item?.GetType()})");
                }
            }
        }
    }
}
