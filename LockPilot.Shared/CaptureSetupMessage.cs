using MessagePack;

namespace LockPilot.Shared;

[MessagePackObject]
public class CaptureSetupMessage : IMessage
{
    [Key(0)]
    public int AimWidth { get; init; }

    [Key(1)]
    public int AimHeight { get; init; }
}
