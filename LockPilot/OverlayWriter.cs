using LockPilot.Tracking;
using NetMQ;
using NetMQ.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LockPilot;

class OverlayWriter : IDisposable
{
    readonly RadioSocket m_Socket = new();

    public OverlayWriter(string host, int port)
    {
        m_Socket.Options.Linger = TimeSpan.Zero;
        m_Socket.Connect($"udp://{host}:{port}");
    }

    static readonly JsonSerializerOptions m_JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public void Write(TargetTracker tracker)
    {
        var payload = new
        {
            tracker.State,
            Rect = tracker.State == TargetTrackerState.Tracking ? new
            {
                tracker.DetectionRect.X,
                tracker.DetectionRect.Y,
                tracker.DetectionRect.Width,
                tracker.DetectionRect.Height
            } : null
        };
        var json = JsonSerializer.Serialize(payload, m_JsonOptions);
        m_Socket.TrySend("overlay", json);
    }

    public void Dispose()
    {
        m_Socket.Dispose();
    }
}
