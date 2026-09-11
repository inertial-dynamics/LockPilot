using LockPilot.Shared;
using NetMQ;
using NetMQ.Sockets;

namespace GroundCon;

class CommandClient : IDisposable
{
    readonly PushSocket m_Socket = new();

    public CommandClient(string host, int port)
    {
        m_Socket.Options.Linger = TimeSpan.Zero;
        m_Socket.Connect($"tcp://{host}:{port}");
    }

    public void Send(Command command) => m_Socket.SendFrame(CommandMessage.Serialize(command));

    public void Dispose()
    {
        m_Socket.Dispose();
        NetMQConfig.Cleanup();
    }
}
