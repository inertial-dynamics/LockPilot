using LockPilot.Shared;
using NetMQ;
using NetMQ.Sockets;

namespace LockPilot;

class CommandServer : IDisposable
{
    readonly PullSocket m_Socket = new();

    public CommandServer(int port)
    {
        m_Socket.Options.Linger = TimeSpan.Zero;
        m_Socket.Bind($"tcp://*:{port}");
    }

    public bool TryDequeue(out IMessage command)
    {
        command = m_Socket.TryReceiveFrameBytes(out var bytes) ? IMessage.Deserialize(bytes) : null;
        return command != null;
    }

    public void Dispose()
    {
        m_Socket.Dispose();
    }
}
