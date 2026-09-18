using MessagePack;

namespace LockPilot.Shared;

[Union(0, typeof(CommandMessage))]
[Union(1, typeof(SetupMessage))]
public interface IMessage
{
    public static byte[] Serialize(IMessage command) => MessagePackSerializer.Serialize(command);

    public static IMessage Deserialize(byte[] bytes) => MessagePackSerializer.Deserialize<IMessage>(bytes);
}
