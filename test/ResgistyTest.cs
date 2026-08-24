using System.Collections.Generic;
using System.Linq;
using CompasPb;
using CompasPb.Data;
using CompasPb.Test.Domain;
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
    public void RegisterAssembly_ExternalProto_ResolvesType()
    {
        // ToolPathData lives in the test assembly (simulates a domain package)
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        var result = Registry.GetType("test.domain.ToolPathData");
        Assert.Equal(typeof(ToolPathData), result);
    }

    [Fact]
    public void RegisterAssembly_ExternalProto_PackUnpackRoundTrip()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        var toolPath = new ToolPathData
        {
            Name = "milling_01",
            ToolFrame = new ToolFrameData
            {
                X = 1.0,
                Y = 2.0,
                Z = 3.0,
            },
        };
        toolPath.Segments.Add(
            new ToolFrameData
            {
                X = 10.0,
                Y = 20.0,
                Z = 30.0,
            }
        );

        var serializer = new CompasPbSerializer();
        var bytes = serializer.Pack(toolPath);
        var result = serializer.Unpack<ToolPathData>(bytes);

        Assert.NotNull(result);
        Assert.Equal("milling_01", result.Name);
        Assert.Equal(1.0, result.ToolFrame.X);
        Assert.Single(result.Segments);
        Assert.Equal(10.0, result.Segments[0].X);
    }

    [Fact]
    public void RegisterSerializer_CustomType_PacksViaDomainFunction()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        // Register a converter: TestToolPath (domain type) -> ToolPathData (proto)
        Registry.RegisterSerializer<TestToolPath>(tp =>
            Any.Pack(
                new ToolPathData
                {
                    Name = tp.Name,
                    ToolFrame = new ToolFrameData
                    {
                        X = tp.ToolX,
                        Y = tp.ToolY,
                        Z = tp.ToolZ,
                    },
                }
            )
        );

        try
        {
            var serializer = new CompasPbSerializer();
            var domain = new TestToolPath
            {
                Name = "path_01",
                ToolX = 5.0,
                ToolY = 6.0,
                ToolZ = 7.0,
            };

            var bytes = serializer.Pack(domain);
            var result = serializer.Unpack(bytes);

            // Without a custom deserializer, it comes back as the raw ToolPathData
            var proto = Assert.IsType<ToolPathData>(result);
            Assert.Equal("path_01", proto.Name);
            Assert.Equal(5.0, proto.ToolFrame.X);
            Assert.Equal(6.0, proto.ToolFrame.Y);
            Assert.Equal(7.0, proto.ToolFrame.Z);
        }
        finally
        {
            Registry.UnregisterSerializer<TestToolPath>();
        }
    }

    [Fact]
    public void RegisterDeserializer_CustomType_UnpacksViaDomainFunction()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        Registry.RegisterSerializer<TestToolPath>(tp =>
            Any.Pack(
                new ToolPathData
                {
                    Name = tp.Name,
                    ToolFrame = new ToolFrameData
                    {
                        X = tp.ToolX,
                        Y = tp.ToolY,
                        Z = tp.ToolZ,
                    },
                }
            )
        );
        Registry.RegisterDeserializer(
            "test.domain.ToolPathData",
            any =>
            {
                var proto = any.Unpack<ToolPathData>();
                return new TestToolPath
                {
                    Name = proto.Name,
                    ToolX = proto.ToolFrame.X,
                    ToolY = proto.ToolFrame.Y,
                    ToolZ = proto.ToolFrame.Z,
                };
            }
        );

        try
        {
            var serializer = new CompasPbSerializer();
            var input = new TestToolPath
            {
                Name = "path_02",
                ToolX = 1.0,
                ToolY = 2.0,
                ToolZ = 3.0,
            };

            var bytes = serializer.Pack(input);
            var result = serializer.Unpack(bytes);

            // With custom deserializer, it comes back as the domain type
            var domain = Assert.IsType<TestToolPath>(result);
            Assert.Equal("path_02", domain.Name);
            Assert.Equal(1.0, domain.ToolX);
            Assert.Equal(2.0, domain.ToolY);
            Assert.Equal(3.0, domain.ToolZ);
        }
        finally
        {
            Registry.UnregisterSerializer<TestToolPath>();
            Registry.UnregisterDeserializer("test.domain.ToolPathData");
        }
    }

    [Fact]
    public void CompasPbRegistrationAttribute_IsAssemblyTargeted()
    {
        var attr = typeof(CompasPbRegistrationAttribute);
        var usage = (System.AttributeUsageAttribute)
            System.Attribute.GetCustomAttribute(attr, typeof(System.AttributeUsageAttribute))!;
        Assert.Equal(System.AttributeTargets.Assembly, usage.ValidOn);
    }
}

/// <summary>
/// A plain domain type (not IMessage) used to test custom serializer/deserializer registration.
/// Simulates a domain model wrapping ToolPathData (the way Unity or Rhino types wrap proto types).
/// </summary>
public class TestToolPath
{
    public string Name { get; set; } = "";
    public double ToolX { get; set; }
    public double ToolY { get; set; }
    public double ToolZ { get; set; }
}
