using System;
using System.Collections.Generic;
using CompasPb;
using CompasPb.Data;
using CompasPb.Test.Domain;
using Google.Protobuf;
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

    [Fact]
    public void Register_UsesFunctionsAndBaseClassRegistration()
    {
        Registry.Register<ExternalBase, StringValue>(
            value => new StringValue { Value = value.Text },
            value => new ExternalBase(value.Value)
        );

        var input = new ExternalDerived("registered downstream");
        var bytes = Serializer.PackAsBytes(Serializer.PackAsAnyData(input));
        var envelope = MessageData.Parser.ParseFrom(bytes);
        var result = Assert.IsType<ExternalBase>(
            Deserializer.UnpackAnyData(Deserializer.UnpackBytes(bytes))
        );

        Assert.Equal(AnyData.DataOneofCase.Message, envelope.Data.DataCase);
        Assert.Equal(
            "type.googleapis.com/google.protobuf.StringValue",
            envelope.Data.Message.TypeUrl
        );
        Assert.Equal(input.Text, result.Text);
    }

    [Fact]
    public void RegisterFallback_RebuildsRegisteredObject()
    {
        Registry.RegisterFallback<ExternalFallback>(
            "example/ExternalFallback",
            value => new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object> { ["text"] = value.Text },
            },
            values =>
            {
                var data = Assert.IsType<Dictionary<string, object?>>(values["data"]);
                return new ExternalFallback(Assert.IsType<string>(data["text"]));
            }
        );

        var input = new ExternalFallback("fallback downstream");
        var bytes = Serializer.PackAsBytes(Serializer.PackAsAnyData(input));
        var envelope = MessageData.Parser.ParseFrom(bytes);
        var result = Assert.IsType<ExternalFallback>(
            Deserializer.UnpackAnyData(Deserializer.UnpackBytes(bytes))
        );

        Assert.Equal(AnyData.DataOneofCase.Fallback, envelope.Data.DataCase);
        Assert.Equal(input.Text, result.Text);
    }

    [Fact]
    public void TypeUrl_UsesExactFullNameAfterLastSlash()
    {
        var point = new PointData { X = 1.0f };
        var valid = new Any
        {
            TypeUrl = "custom.registry/compas_pb.data.PointData",
            Value = point.ToByteString(),
        };
        var collidingShortName = new Any
        {
            TypeUrl = "custom.registry/example.other.PointData",
            Value = point.ToByteString(),
        };

        _ = Assert.IsType<PointData>(Deserializer.UnpackAnyData(new AnyData { Message = valid }));
        _ = Assert.Throws<NotSupportedException>(() =>
            Deserializer.UnpackAnyData(new AnyData { Message = collidingShortName })
        );
    }

    [Theory]
    [InlineData("type.googleapis.com/compas_pb.data.PointData")]
    [InlineData("custom.registry/compas_pb.data.PointData")]
    [InlineData("compas_pb.data.PointData")]
    public void GetType_ResolvesTheFullNameAfterTheLastSlash(string typeUrl)
    {
        Assert.Equal(typeof(PointData), Registry.GetType(typeUrl));
    }

    [Fact]
    public void GetType_DoesNotResolveASimpleName()
    {
        // The runtime contract keys on the full protobuf name. Matching "PointData" alone would
        // collide the moment a domain package ships a message of the same name.
        Assert.Null(Registry.GetType("PointData"));
    }

    [Fact]
    public void GetType_UnknownTypeUrl_ReturnsNull()
    {
        Assert.Null(Registry.GetType("type.googleapis.com/some.unknown.Type"));
    }

    [Fact]
    public void RegisterAssembly_SameAssemblyTwice_IsANoOp()
    {
        Registry.RegisterAssembly(typeof(Registry).Assembly);
        Registry.RegisterAssembly(typeof(Registry).Assembly);

        Assert.Equal(typeof(PointData), Registry.GetType("compas_pb.data.PointData"));
    }

    [Fact]
    public void RegisterAssembly_RejectsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() => Registry.RegisterAssembly(null!));
    }

    [Fact]
    public void RegisterAssembly_DomainPackageProto_ResolvesType()
    {
        // ToolPathData is generated from test/Protos/toolpath.proto into the test assembly, which
        // stands in for a domain package shipping its own bindings.
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        Assert.Equal(typeof(ToolPathData), Registry.GetType("test.domain.ToolPathData"));
    }

    [Fact]
    public void RegisterAssembly_DomainPackageProto_RoundTrips()
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
        var result = serializer.Unpack<ToolPathData>(serializer.Pack(toolPath));

        Assert.NotNull(result);
        Assert.Equal("milling_01", result.Name);
        Assert.Equal(1.0, result.ToolFrame.X);
        Assert.Equal(10.0, Assert.Single(result.Segments).X);
    }

    [Fact]
    public void Register_DomainTypeThatIsNotAMessage_RoundTripsThroughItsOwnFunctions()
    {
        // The Unity/Rhino case: a plain model type that knows nothing about protobuf, paired with
        // a domain package's message. ToolFrameData is claimed by no other test, and registering a
        // domain type also claims that message's deserializer.
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);
        Registry.Register<TestToolFrame, ToolFrameData>(
            frame => new ToolFrameData
            {
                X = frame.X,
                Y = frame.Y,
                Z = frame.Z,
            },
            message => new TestToolFrame(message.X, message.Y, message.Z)
        );

        var serializer = new CompasPbSerializer();
        var restored = serializer.Unpack(serializer.Pack(new TestToolFrame(5.0, 6.0, 7.0)));

        var frame = Assert.IsType<TestToolFrame>(restored);
        Assert.Equal(5.0, frame.X);
        Assert.Equal(6.0, frame.Y);
        Assert.Equal(7.0, frame.Z);
    }

    [Fact]
    public void GetJsonTypeRegistry_CoversDomainPackageMessages()
    {
        Registry.RegisterAssembly(typeof(ToolPathData).Assembly);

        var registry = Registry.GetJsonTypeRegistry();

        Assert.NotNull(registry.Find("compas_pb.data.PointData"));
        Assert.NotNull(registry.Find("test.domain.ToolPathData"));
    }

    /// <summary>
    /// A plain domain type, with no protobuf awareness, standing in for a Unity or Rhino model
    /// class that a downstream package maps onto a message.
    /// </summary>
    private sealed class TestToolFrame
    {
        public TestToolFrame(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }

    [Fact]
    public void UnpackAs_ReturnsTheRawMessageEvenWhenADomainConversionIsRegistered()
    {
        // What a consumer that routes on protobuf type needs: Registry.GetType plus UnpackAs give
        // back the message itself, not the domain object Unpack would build from it. CompasXRSharp's
        // Unity converter dispatches on the message type this way.
        Registry.Register<TestToolPathName, ToolPathData>(
            value => new ToolPathData { Name = value.Name },
            message => new TestToolPathName(message.Name)
        );

        var any = Any.Pack(new ToolPathData { Name = "milling_02" });
        var messageType = Registry.GetType(any.TypeUrl);

        Assert.Equal(typeof(ToolPathData), messageType);
        var raw = Assert.IsType<ToolPathData>(Registry.UnpackAs(any, messageType!));
        Assert.Equal("milling_02", raw.Name);

        // The envelope-level entry point still yields the domain object.
        var serializer = new CompasPbSerializer();
        var domain = serializer.Unpack(serializer.Pack(new TestToolPathName("milling_02")));
        Assert.IsType<TestToolPathName>(domain);
    }

    private sealed class TestToolPathName
    {
        public TestToolPathName(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private class ExternalBase
    {
        public ExternalBase(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    private sealed class ExternalDerived : ExternalBase
    {
        public ExternalDerived(string text)
            : base(text) { }
    }

    private sealed class ExternalFallback
    {
        public ExternalFallback(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }
}
