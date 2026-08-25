using System.Collections.Generic;
using CompasPb.Data;
using Google.Protobuf.WellKnownTypes;
using Xunit;

public class SerializerTest
{
    private readonly ICompasPbSerializer _serializer = new CompasPbSerializer();

    [Fact]
    public void RoundTrip_Int()
    {
        var bytes = _serializer.Pack(42);
        var result = _serializer.Unpack(bytes);
        Assert.Equal(42L, result);
    }

    [Fact]
    public void RoundTrip_Double()
    {
        var bytes = _serializer.Pack(3.14);
        var result = _serializer.Unpack(bytes);
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void RoundTrip_String()
    {
        var bytes = _serializer.Pack("hello");
        var result = _serializer.Unpack(bytes);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void RoundTrip_Bool()
    {
        var bytes = _serializer.Pack(true);
        var result = _serializer.Unpack(bytes);
        Assert.Equal(true, result);
    }

    [Fact]
    public void RoundTrip_Null()
    {
        var bytes = _serializer.Pack(null);
        var result = _serializer.Unpack(bytes);
        Assert.Null(result);
    }

    [Fact]
    public void RoundTrip_List()
    {
        var input = new List<object> { 1, "two", true };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack(bytes);

        var list = Assert.IsType<List<object?>>(result);
        Assert.Equal(3, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Equal("two", list[1]);
        Assert.Equal(true, list[2]);
    }

    [Fact]
    public void RoundTrip_Dict()
    {
        var input = new Dictionary<string, object> { { "x", 1 }, { "label", "point" } };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack(bytes);

        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(1L, dict["x"]);
        Assert.Equal("point", dict["label"]);
    }

    [Fact]
    public void RoundTrip_PointData_Dynamic()
    {
        var input = new PointData
        {
            X = 1.0f,
            Y = 2.0f,
            Z = 3.0f,
        };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack(bytes);

        var point = Assert.IsType<PointData>(result);
        Assert.Equal(1.0f, point.X);
        Assert.Equal(2.0f, point.Y);
        Assert.Equal(3.0f, point.Z);
    }

    [Fact]
    public void RoundTrip_PointData_Typed()
    {
        var input = new PointData
        {
            X = 1.0f,
            Y = 2.0f,
            Z = 3.0f,
        };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack<PointData>(bytes);

        Assert.NotNull(result);
        Assert.Equal(1.0f, result.X);
        Assert.Equal(2.0f, result.Y);
        Assert.Equal(3.0f, result.Z);
    }

    [Fact]
    public void RoundTrip_FrameData_Typed()
    {
        var input = new FrameData
        {
            Name = "testFrame",
            Point = new PointData
            {
                X = 1.0f,
                Y = 2.0f,
                Z = 3.0f,
            },
            Xaxis = new VectorData
            {
                X = 1.0f,
                Y = 0.0f,
                Z = 0.0f,
            },
            Yaxis = new VectorData
            {
                X = 0.0f,
                Y = 1.0f,
                Z = 0.0f,
            },
        };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack<FrameData>(bytes);

        Assert.NotNull(result);
        Assert.Equal("testFrame", result.Name);
        Assert.Equal(1.0f, result.Point.X);
        Assert.Equal(1.0f, result.Xaxis.X);
    }

    [Fact]
    public void RoundTrip_NestedList()
    {
        var input = new List<object>
        {
            new List<object> { 1, 2, 3 },
            new Dictionary<string, object> { { "key", "value" } },
        };
        var bytes = _serializer.Pack(input);
        var result = _serializer.Unpack(bytes);

        var outer = Assert.IsType<List<object?>>(result);
        Assert.Equal(2, outer.Count);
        var inner = Assert.IsType<List<object?>>(outer[0]);
        Assert.Equal(3, inner.Count);
    }

    [Fact]
    public void Pack_UsesDirectContainerAndNumericArms()
    {
        var integer = Serializer.PackAsAnyData(42);
        var floatingPoint = Serializer.PackAsAnyData(42.0);
        var list = Serializer.PackAsAnyData(new List<object>());
        var dictionary = Serializer.PackAsAnyData(new Dictionary<string, object>());

        Assert.Equal(AnyData.DataOneofCase.IntValue, integer.DataCase);
        Assert.Equal(AnyData.DataOneofCase.DoubleValue, floatingPoint.DataCase);
        Assert.Equal(AnyData.DataOneofCase.ListValue, list.DataCase);
        Assert.Equal(AnyData.DataOneofCase.DictValue, dictionary.DataCase);
    }

    [Fact]
    public void RoundTrip_Bytes_UsesPythonCompatibleBase64Prefix()
    {
        byte[] input = [0, 1, 2, 254, 255];

        var bytes = _serializer.Pack(input);
        var result = Assert.IsType<byte[]>(_serializer.Unpack(bytes));

        Assert.Equal(input, result);
    }

    [Fact]
    public void Unpack_LegacyAnyWrappedContainers()
    {
        var legacyList = new ListData();
        legacyList.Items.Add(Serializer.PackAsAnyData(1));
        var legacyDictionary = new DictData();
        legacyDictionary.Items.Add("items", new AnyData { Message = Any.Pack(legacyList) });

        var result = Assert.IsType<Dictionary<string, object?>>(
            Deserializer.UnpackAnyData(new AnyData { Message = Any.Pack(legacyDictionary) })
        );
        var items = Assert.IsType<List<object?>>(result["items"]);

        Assert.Equal(1L, items[0]);
    }
}
