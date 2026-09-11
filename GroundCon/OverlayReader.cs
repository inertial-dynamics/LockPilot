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

    public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
    {
        var (_, json) = await m_Socket.ReceiveStringAsync(cancellationToken);
        return json;
    }

    public void Dispose()
    {
        m_Socket.Dispose();
    }
}
