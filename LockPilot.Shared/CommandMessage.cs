using MessagePack;

namespace LockPilot.Shared;

[MessagePackObject]
public class CommandMessage : IMessage
{
    [Key(0)]
    public Command Command { get; init; }
}
