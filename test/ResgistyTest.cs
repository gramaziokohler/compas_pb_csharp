using Xunit;
using CompasPb.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using Google.Protobuf;

public class RegistryTest
{
  [Fact]
  public void TestRegistry()
  {
    var expected = new List<IMessage>
    {
      new PointData(),
      new LineData(),
      new FrameData(),
      new VectorData(),
      new MeshData(),
      new CircleData(),
      new PrimitiveData(),
      new ListData(),
      new DictData(),
      // Add other message types as needed
    };

    var registeredTypes = Registry.GetRegisteredTypes().ToList();
    foreach (var instance in expected)
    {
        var instanceType = instance.GetType();
        bool isRegistered = registeredTypes.Contains(instanceType);
        Console.WriteLine($"Type {instanceType.Name}: {(isRegistered ? "Registered" : "Missing")}");
        Assert.True(isRegistered, $"{instanceType.Name} should be registered");
    }
  }
}