using System;
using System.Collections.Generic;
using System.IO;
using CompasPb.Data;
using CompasPb.Route;

namespace CompasPb.UserCase
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Example");

      // Pack Data
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
        var responsedMessage = Deserializer.UnpackBytes(response);
        var unpackedData = Deserializer.UnpackAnyData(responsedMessage); 
        Console.WriteLine($"Unpacked data: {unpackedData}");


        // var unpackedData2 = Deserializer.Unpack<ListData>(responsedMessage);
        // Console.WriteLine($"Unpacked data: {unpackedData}");
        // Console.WriteLine($"Unpacked data type: {unpackedDataType}");
      //   var lst = dataHandler.UnpackListData(unpackedData);
      //   foreach (var item in lst)
      //   {
      //     var DataType = dataHandler.TryToGetType(item);
      //     Console.WriteLine($"Unpacked item type: {DataType}");
      //     var data = dataHandler.UnpackAnyData(item, DataType);
      //     Console.WriteLine($"Unpacked {DataType}: {data}");
      //   }
    }
  }
}
