using System.Net;
using System.Net.Sockets;
using OpenCvSharp;

namespace LockPilot;

class UdpWriter : IDisposable
{
    readonly UdpClient m_Client = new();

    public UdpWriter(string destination)
    {
        var uri = new Uri(destination);
        var endPoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.IsDefaultPort ? 5000 : uri.Port);
        m_Client.Connect(endPoint);
    }

    public void Dispose()
    {
        m_Client.Dispose();
    }

    public void Write(Mat image)
    {
        for (var quality = 80; quality >= 10; quality -= 10)
        {
            Cv2.ImEncode(".jpg", image, out var buffer, new ImageEncodingParam(ImwriteFlags.JpegQuality, quality));
            if (buffer.Length <= 65507)
            {
                m_Client.Send(buffer, buffer.Length);
                return;
            }
        }
        Console.WriteLine($"Failed to encode image to fit in UDP packet (size: {image.Width}x{image.Height})");
    }
}
