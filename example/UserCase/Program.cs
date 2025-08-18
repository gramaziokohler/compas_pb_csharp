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
      DataHandler dataHandler = new DataHandler();
      Console.WriteLine("Pack data");
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
      var packData = dataHandler.PackAsBytes(dataHandler.PackAsAnyData(dataHandler));
      Console.WriteLine($"Packed data: {packData}");
      // string filePath = "packedData.bin";
      // Write the file
      //   using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
      //   {
      //     fileStream.Write(packData, 0, packData.Length);
      //   }
      //   Console.WriteLine($"Packed data written to packedData.pb");

      //   // Unpack data
      //   byte[] response = File.ReadAllBytes(filePath);
      //   var unpackedData = dataHandler.UnpackAsAnyData(response); 
      //   var unpackedDataType = dataHandler.TryToGetType(unpackedData);
      //   Console.WriteLine($"unpack the data type:{unpackedDataType}");

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
