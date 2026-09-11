using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LockPilot.Tracking;

namespace LockPilot;

class OverlayWriter : IDisposable
{
    readonly UdpClient m_Client = new();

    public OverlayWriter(string host, int port)
    {
        m_Client.Connect(host, port);
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
        m_Client.Send(Encoding.UTF8.GetBytes(json));
    }

    public void Dispose()
    {
        m_Client.Dispose();
    }
}
