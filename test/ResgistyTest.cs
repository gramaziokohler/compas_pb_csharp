using CompasPb;
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

    [Theory]
    [InlineData("type.googleapis.com/compas_pb.data.PointData", typeof(PointData))]
    [InlineData("compas_pb.data.PointData", typeof(PointData))]
    [InlineData("PointData", typeof(PointData))]
    public void GetType_ResolvesTypeUrl(string typeUrl, System.Type expected)
    {
        var result = Registry.GetType(typeUrl);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetType_UnknownTypeUrl_ReturnsNull()
    {
        var result = Registry.GetType("type.googleapis.com/some.unknown.Type");
        Assert.Null(result);
    }

    [Fact]
    public void RegisterAssembly_SameAssembly_DoesNotThrow()
    {
        // Re-registering the same assembly is a no-op (idempotent)
        Registry.RegisterAssembly(typeof(Registry).Assembly);

        var result = Registry.GetType("compas_pb.data.PointData");
        Assert.Equal(typeof(PointData), result);
    }

    [Fact]
    public void CompasPbRegistrationAttribute_IsAssemblyTargeted()
    {
        var attr = typeof(CompasPbRegistrationAttribute);
        var usage = (System.AttributeUsageAttribute)System.Attribute.GetCustomAttribute(
            attr,
            typeof(System.AttributeUsageAttribute)
        )!;
        Assert.Equal(System.AttributeTargets.Assembly, usage.ValidOn);
    }
}
