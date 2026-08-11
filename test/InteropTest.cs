using System;
using System.Collections.Generic;
using System.IO;
using CompasPb.Data;
using Xunit;

public class InteropTest
{
    [Fact]
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
}
