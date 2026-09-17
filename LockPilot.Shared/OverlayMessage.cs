using MessagePack;

namespace LockPilot.Shared;

[MessagePackObject]
public class OverlayMessage
{
    [Key(0)]
    public TargetTrackerState State { get; init; }

    [Key(1)]
    public OverlayRect Rect { get; init; }

    public static byte[] Serialize(OverlayMessage message) => MessagePackSerializer.Serialize(message);

    public static OverlayMessage Deserialize(byte[] bytes) => MessagePackSerializer.Deserialize<OverlayMessage>(bytes);

    public override string ToString() => Rect != null ? $"{State} {Rect.X},{Rect.Y} {Rect.Width}x{Rect.Height}" : $"{State}";
}
