using LockPilot.Shared;
using LockPilot.Tracking;
using NetMQ;
using NetMQ.Sockets;

namespace LockPilot;

class OverlayWriter : IDisposable
{
    readonly RadioSocket m_Socket = new();

    public OverlayWriter(string host, int port)
    {
        m_Socket.Options.Linger = TimeSpan.Zero;
        m_Socket.Connect($"udp://{host}:{port}");
    }

    public void Write(TargetTracker tracker)
    {
        var payload = new OverlayMessage
        {
            State = tracker.State,
            Rect = tracker.State == TargetTrackerState.Tracking ? new OverlayRect
            {
                X = tracker.DetectionRect.X,
                Y = tracker.DetectionRect.Y,
                Width = tracker.DetectionRect.Width,
                Height = tracker.DetectionRect.Height
            } : null
        };
        m_Socket.TrySend("overlay", OverlayMessage.Serialize(payload));
    }

    public void Dispose()
    {
        m_Socket.Dispose();
    }
}
