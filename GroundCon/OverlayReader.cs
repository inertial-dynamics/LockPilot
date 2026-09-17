using LockPilot.Shared;
using NetMQ;
using NetMQ.Sockets;

namespace GroundCon;

class OverlayReader : IDisposable
{
    readonly DishSocket m_Socket = new();

    public OverlayReader(int port)
    {
        m_Socket.Options.Linger = TimeSpan.Zero;
        m_Socket.Join("overlay");
        m_Socket.Bind($"udp://*:{port}");
    }

    public async Task<OverlayMessage> ReceiveAsync(CancellationToken cancellationToken)
    {
        var (_, bytes) = await m_Socket.ReceiveBytesAsync(cancellationToken);
        return OverlayMessage.Deserialize(bytes);
    }

    public void Dispose()
    {
        m_Socket.Dispose();
    }
}
