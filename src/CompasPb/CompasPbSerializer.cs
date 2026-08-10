using CompasPb.Data;
using Google.Protobuf;

namespace CompasPb;

/// <summary>
/// Serializes and deserializes COMPAS data types using Protocol Buffers.
/// Supports binary (protobuf) and JSON formats.
/// </summary>
public class CompasPbSerializer : ICompasPbSerializer
{
    /// <inheritdoc />
    public byte[] Pack(object? data) => Serializer.PackAsBytes(Serializer.PackAsAnyData(data));

    /// <inheritdoc />
    public object? Unpack(byte[] data) =>
        Deserializer.UnpackAnyData(Deserializer.UnpackBytes(data));

    /// <inheritdoc />
    public T? Unpack<T>(byte[] data)
        where T : class, IMessage<T>, new() =>
        Deserializer.Unpack<T>(Deserializer.UnpackBytes(data));

    /// <inheritdoc />
    public string PackAsJson(object? data) =>
        Serializer.PackAsJson(Serializer.PackAsAnyData(data));

    /// <inheritdoc />
    public object? UnpackJson(string json) =>
        Deserializer.UnpackAnyData(Deserializer.UnpackJson(json));

    /// <inheritdoc />
    public T? UnpackJson<T>(string json)
        where T : class, IMessage<T>, new() =>
        Deserializer.Unpack<T>(Deserializer.UnpackJson(json));
}
