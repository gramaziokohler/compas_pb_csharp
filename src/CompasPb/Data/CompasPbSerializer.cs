using Google.Protobuf;

namespace CompasPb.Data;

public class CompasPbSerializer : ICompasPbSerializer
{
    public byte[] Pack(object? data) => Serializer.PackAsBytes(Serializer.PackAsAnyData(data));

    public object? Unpack(byte[] data) =>
        Deserializer.UnpackAnyData(Deserializer.UnpackBytes(data));

    public T? Unpack<T>(byte[] data)
        where T : class, IMessage<T>, new() =>
        Deserializer.Unpack<T>(Deserializer.UnpackBytes(data));
}
