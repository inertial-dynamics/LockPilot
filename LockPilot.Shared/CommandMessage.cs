using MessagePack;

namespace LockPilot.Shared;

[MessagePackObject]
public class CommandMessage
{
    [Key(0)]
    public Command Cmd { get; init; }

    public static byte[] Serialize(Command command) => MessagePackSerializer.Serialize(new CommandMessage { Cmd = command });

    public static Command Deserialize(byte[] bytes) => MessagePackSerializer.Deserialize<CommandMessage>(bytes).Cmd;
}
