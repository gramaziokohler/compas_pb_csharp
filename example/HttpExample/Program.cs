using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CompasPb.Data;
using CompasPb.Route;

namespace CompasPb.HttpExample
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("example");
            RouteHttpClient client = new RouteHttpClient("http://localhost:8000/api");
            // await client.TestConnection();
            // Get data (unpack)
            Console.WriteLine("Get the data from backend");
            var response = await client.GetData();
            DataHandler dataHandler = new DataHandler();
            if (response == null)
            {
                return;
            }

            var unpackedData = dataHandler.UnpackAsAnyData(response); // Unpack to AnyData
            var unpackedDataType = dataHandler.TryToGetType(unpackedData);
            Console.WriteLine($"unpack the data type:{unpackedDataType}");
            var lst = dataHandler.UnpackListData(unpackedData);   // unpack to system.collection

            foreach (var item in lst)
            {
                // Console.WriteLine($"Unpacked item: {item}");
                var DataType = dataHandler.TryToGetType(item);
                Console.WriteLine($"Unpacked item type: {DataType}");
                var data = dataHandler.UnpackAnyData(item, DataType);
                Console.WriteLine($"Unpacked item: {data}");
            }

            // Post Data (pack)
            Console.WriteLine("Post the data to backend");
            List<object> nestedList = new List<object>()
            {
                new List<int> { 1, 2, 3 },
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
            var packData = dataHandler.PackAsBytes(dataHandler.PackAsAnyData(nestedList));
            Console.WriteLine($"Packed data: {packData}");
            await client.PostData(packData);
        }
    }
}
