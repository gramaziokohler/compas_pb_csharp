using Google.Protobuf;

namespace CompasPb;

/// <summary>
/// Contract for serializing and deserializing COMPAS data types using Protocol Buffers.
/// </summary>
public interface ICompasPbSerializer
{
    /// <summary>
    /// Packs the given object into a protobuf binary byte array.
    /// Supports IMessage types, lists, dictionaries, and primitives.
    /// </summary>
    byte[] Pack(object? data);

    /// <summary>
    /// Packs the given object into a JSON string.
    /// Uses the same MessageData envelope as the binary format.
    /// </summary>
    string PackAsJson(object? data);

    /// <summary>
    /// Unpacks a protobuf binary byte array into an object.
    /// Returns the dynamic type (may require casting).
    /// </summary>
    object? Unpack(byte[] data);

    /// <summary>
    /// Unpacks a protobuf binary byte array into a typed object.
    /// </summary>
    /// <typeparam name="T">The expected protobuf message type.</typeparam>
    T? Unpack<T>(byte[] data)
        where T : class, IMessage<T>, new();

    /// <summary>
    /// Unpacks a JSON string into an object.
    /// Returns the dynamic type (may require casting).
    /// </summary>
    object? UnpackJson(string json);

    /// <summary>
    /// Unpacks a JSON string into a typed object.
    /// </summary>
    /// <typeparam name="T">The expected protobuf message type.</typeparam>
    T? UnpackJson<T>(string json)
        where T : class, IMessage<T>, new();
}
