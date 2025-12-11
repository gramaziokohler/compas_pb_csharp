using CompasPb.Data;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;
using System.Collections;
using Xunit;
using System;


namespace CompasPb.Test
{
    public class SerializerTest
    {
        public static IEnumerable<object[]> TestPrimitiveData =>
            new List<object[]>
            {
                new object[] { 42 },
                new object[] { 3.14f },
                new object[] { 2.71828 },
                new object[] { 12345678901234L },
                new object[] { 99.99m },
                new object[] { "Hello, World!" },
                new object[] { true },
                new object[] { (byte)255 },
                new object[] { new byte[] { 0x01, 0x02, 0x03, 0x04 } },
            };

        [Theory]
        [MemberData(nameof(TestPrimitiveData))]
        public void PackAndUnpack_PrimitiveData(object primitiveData)
        {
            AnyData packedData = Serializer.PackAsAnyData(primitiveData);

            object? unpackedData = Deserializer.UnpackAnyData(packedData);

            switch (primitiveData)
            {
                // case decimal m:
                //     Assert.Equal((double)m, (double)unpackedData!, 15);
                //     break;
                //
                // case long l:
                //     Assert.Equal(l, Convert.ToInt64(unpackedData!));
                //     break;
                //
                // case float f:
                //     Assert.Equal(f, (float)(double)unpackedData!, 7);
                //     break;

                case double d:
                    Assert.Equal(d, (double)unpackedData!, 15);
                    break;

                default:
                    Assert.Equal(primitiveData, unpackedData);
                    break;
            }
        }
    }
}
