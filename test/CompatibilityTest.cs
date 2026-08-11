using System;
using System.Collections;
using System.Collections.Generic;
using CompasPb.Data;
using Google.Protobuf;
using Xunit;

public class CompatibilityTest
{
    [Fact]
    public void RoundTrip_Fallback_PreservesCompasJsonDump()
    {
        var input = new TestFallback();

        var bytes = Serializer.PackAsBytes(Serializer.PackAsAnyData(input));
        var envelope = MessageData.Parser.ParseFrom(bytes);
        var result = Assert.IsType<Dictionary<string, object?>>(
            Deserializer.UnpackAnyData(Deserializer.UnpackBytes(bytes))
        );

        Assert.Equal(AnyData.DataOneofCase.Fallback, envelope.Data.DataCase);
        Assert.Equal("example/Thing", result["dtype"]);
        Assert.Equal("thing-guid", result["guid"]);
    }

    [Fact]
    public void UnpackBytes_AcceptsCompatibleVersionWithinMajorOne()
    {
        var bytes = new MessageData
        {
            Version = "1.9.3",
            Data = Serializer.PackAsAnyData(42),
        }.ToByteArray();

        var result = Deserializer.UnpackAnyData(Deserializer.UnpackBytes(bytes));

        Assert.Equal(42L, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2.0.0")]
    public void UnpackBytes_RejectsMissingOrIncompatibleVersion(string version)
    {
        var bytes = new MessageData
        {
            Version = version,
            Data = Serializer.PackAsAnyData(42),
        }.ToByteArray();

        Assert.Throws<InvalidOperationException>(() => Deserializer.UnpackBytes(bytes));
    }

    private sealed class TestFallback : ICompasFallback
    {
        public IDictionary ToFallbackData() =>
            new Dictionary<string, object>
            {
                ["dtype"] = "example/Thing",
                ["data"] = new Dictionary<string, object> { ["value"] = 42 },
                ["guid"] = "thing-guid",
            };
    }
}
