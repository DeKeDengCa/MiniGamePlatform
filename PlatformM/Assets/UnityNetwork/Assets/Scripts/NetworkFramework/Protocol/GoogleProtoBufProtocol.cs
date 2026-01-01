using Google.Protobuf;

namespace NetworkFramework.Protocol
{
    public static class GoogleProtoBufProtocol
    {
        public static byte[] Serialize<T>(T message) where T : IMessage<T>
        {
            return message.ToByteArray();
        }

        public static T Deserialize<T>(byte[] data) where T : IMessage<T>, new()
        {
            T message = new T();
            message.MergeFrom(data);
            return message;
        }
    }
}