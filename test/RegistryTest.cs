using System;
using System.Collections.Generic;
using CompasPb.Data;
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
