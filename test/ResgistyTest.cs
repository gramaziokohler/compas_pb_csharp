using CompasPb.Data;
using Google.Protobuf.WellKnownTypes;
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
    public void GetRegistered_Type(System.Type expectedType)
    {
        var registeredTypes = Registry.GetRegisteredTypes();
        Assert.Contains(expectedType, registeredTypes);
    }

    [Fact]
    public void UnpackAs_KnownType_ReturnsInstance()
    {
        var point = new PointData
        {
            X = 1.0f,
            Y = 2.0f,
            Z = 3.0f,
        };
        var any = Any.Pack(point);

        var result = Registry.UnpackAs(any, typeof(PointData));

        Assert.NotNull(result);
        var unpacked = Assert.IsType<PointData>(result);
        Assert.Equal(1.0f, unpacked.X);
        Assert.Equal(2.0f, unpacked.Y);
        Assert.Equal(3.0f, unpacked.Z);
    }

    [Fact]
    public void UnpackAs_UnknownType_ReturnsNull()
    {
        var point = new PointData { X = 1.0f };
        var any = Any.Pack(point);

        // Pass a type not in the registry
        var result = Registry.UnpackAs(any, typeof(string));

        Assert.Null(result);
    }
}
