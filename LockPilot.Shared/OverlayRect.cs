using MessagePack;

namespace LockPilot.Shared;

[MessagePackObject]
public class OverlayRect
{
    [Key(0)]
    public int X { get; init; }

    [Key(1)]
    public int Y { get; init; }

    [Key(2)]
    public int Width { get; init; }

    [Key(3)]
    public int Height { get; init; }
}
