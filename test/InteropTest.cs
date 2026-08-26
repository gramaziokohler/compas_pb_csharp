using System;
using System.Collections.Generic;
using System.IO;
using CompasPb;
using CompasPb.Data;
using Xunit;

public class InteropTest
{
    [Fact]
    // Deliberately a compas_pb 1.1 payload read by a 1.2 runtime: the wire format is
    // compatible across a major version, and this is the regression test for that.
    public void PythonCompasPb11Model_DeserializesFallbackAndKnownMessages()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "compas_pb_1_1_model.b64"
        );
        var bytes = Convert.FromBase64String(File.ReadAllText(fixturePath));

        var envelope = MessageData.Parser.ParseFrom(bytes);
        var model = Assert.IsType<Dictionary<string, object?>>(
            Deserializer.UnpackAnyData(Deserializer.UnpackBytes(bytes))
        );
        var modelData = Assert.IsType<Dictionary<string, object?>>(model["data"]);
        var elements = Assert.IsType<Dictionary<string, object?>>(modelData["elements"]);
        var element = Assert.IsType<Dictionary<string, object?>>(
            elements["22222222-2222-2222-2222-222222222222"]
        );
        var elementData = Assert.IsType<Dictionary<string, object?>>(element["data"]);
        var transformation = Assert.IsType<TransformationData>(elementData["transformation"]);

        Assert.Equal("1.1.0", envelope.Version);
        Assert.Equal(AnyData.DataOneofCase.Fallback, envelope.Data.DataCase);
        Assert.Equal("compas_model.models/Model", model["dtype"]);
        Assert.Equal("QR_0", element["name"]);
        Assert.Equal(1.0, transformation.Matrix[3]);
        Assert.Equal(2.0, transformation.Matrix[7]);
        Assert.Equal(3.0, transformation.Matrix[11]);
    }

    [Fact]
    public void CSharpPayload_MatchesBytesProducedByPythonCompasPb12()
    {
        const string pythonPayload =
            "Cig6JgoCICoKCSkAAAAAAAAIQAoPEg0aC2Jhc2U2NDpBUDg9CgQSAggAEgUxLjIuMA==";
        var input = new List<object?> { 42, 3.0, new byte[] { 0, 255 }, null };

        var csharpPayload = Serializer.PackAsBytes(Serializer.PackAsAnyData(input));

        Assert.Equal(pythonPayload, Convert.ToBase64String(csharpPayload));
    }

    [Fact]
    // The JSON half of the contract's "tested both directions against what Python produced".
    // The fixture comes straight out of upstream pb_dump_json, so it also pins the shape of the
    // JSON we write: defaults omitted inside a message, but every oneof arm present.
    public void PythonCompasPb12Json_DeserializesInCSharp()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "compas_pb_1_2_payload.json"
        );
        var json = File.ReadAllText(fixturePath);

        var payload = Assert.IsType<Dictionary<string, object?>>(
            new CompasPbSerializer().UnpackJson(json)
        );

        var frame = Assert.IsType<FrameData>(payload["frame"]);
        Assert.Equal(1.0, frame.Point.X);
        Assert.Equal(3.0, frame.Point.Z);
        Assert.Equal(1.0, frame.Xaxis.X);
        Assert.Equal(1.0, frame.Yaxis.Y);

        // Python wrote each of these at its default value through a oneof arm; losing any of
        // them would mean the two runtimes disagree about what an omitted field means.
        Assert.Equal(0L, payload["count"]);
        Assert.Equal(0.0, payload["ratio"]);
        Assert.Equal("", payload["label"]);
        Assert.Equal(false, payload["flag"]);

        var items = Assert.IsType<List<object?>>(payload["items"]);
        Assert.Equal(1L, items[0]);
        Assert.Equal(2.0, items[1]);
        Assert.Equal("x", items[2]);
    }

    [Fact]
    public void CSharpJson_MatchesTheShapePythonWrites()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "compas_pb_1_2_payload.json"
        );
        var serializer = new CompasPbSerializer();

        // Re-emitting what Python wrote has to land on the same JSON, modulo map ordering.
        var reEmitted = serializer.PackAsJson(serializer.UnpackJson(File.ReadAllText(fixturePath)));

        Assert.Contains("\"intValue\": \"0\"", reEmitted);
        Assert.Contains("\"doubleValue\": 0", reEmitted);
        Assert.Contains("\"value\": \"\"", reEmitted);
        Assert.Contains("\"value\": false", reEmitted);
        Assert.Contains("\"@type\": \"type.googleapis.com/compas_pb.data.FrameData\"", reEmitted);
        Assert.Contains("\"version\": \"" + PackageInfo.Version + "\"", reEmitted);
    }
}
