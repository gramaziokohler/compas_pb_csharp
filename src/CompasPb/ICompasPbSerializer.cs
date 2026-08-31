using CompasPb.Data;
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

    /// <summary>
    /// Converts an object to <see cref="AnyData"/> without wrapping it in a versioned envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Pack(object?)"/> is the entry point for a whole payload. This is the level
    /// below it, for a domain package whose own message has <c>AnyData</c> fields — which
    /// compas_pb's built-in schema already does, in <c>MeshData.edge_keys</c>,
    /// <c>GraphData.node_keys</c>, and <c>AttributeColumn.values</c>. Filling those in by hand
    /// would mean reimplementing the recursive dispatch in every domain package.
    /// </para>
    /// <para>
    /// This mirrors upstream <c>compas_pb.core</c>, whose own <c>conversions.py</c> uses the
    /// same value-level conversion to populate exactly these fields.
    /// </para>
    /// </remarks>
    AnyData PackAsAnyData(object? data);

    /// <summary>
    /// Converts an <see cref="AnyData"/> read out of a message field back into an object.
    /// </summary>
    /// <remarks>The counterpart of <see cref="PackAsAnyData"/>.</remarks>
    object? UnpackAnyData(AnyData data);
}
