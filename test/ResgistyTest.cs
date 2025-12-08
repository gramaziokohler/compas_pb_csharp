using System;
using CompasPb.Data;
using Xunit;

public class RegistryTest
{
    [Theory]
    [InlineData(typeof(PointData))]
    [InlineData(typeof(LineData))]
    [InlineData(typeof(FrameData))]
    [InlineData(typeof(VectorData))]
    [InlineData(typeof(MeshData))]
    [InlineData(typeof(CircleData))]
    [InlineData(typeof(ListData))]
    [InlineData(typeof(DictData))]
    public void GetRegistered_Type(Type expectedType)
    {
        var registeredTypes = Registry.GetRegisteredTypes();
        Assert.Contains(expectedType, registeredTypes);
    }
}
