using Google.Protobuf;

namespace CompasPb.Data;

public interface ICompasPbSerializer
{
    byte[] Pack(object? data);

    object? Unpack(byte[] data);

    T? Unpack<T>(byte[] data)
        where T : class, IMessage<T>, new();
}
