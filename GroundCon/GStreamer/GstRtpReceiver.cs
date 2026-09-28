using Gst;
using Gst.App;

namespace GroundCon.GStreamer;

class GstRtpReceiver : IDisposable
{
    Pipeline m_Pipeline;
    AppSink m_AppSink;

    public bool IsOpened { get; private set; }

    public string Error { get; private set; }

    public static GstRtpReceiver Open(int port)
    {
        var receiver = new GstRtpReceiver();
        try
        {
            receiver.OpenCore(port);
        }
        catch (Exception ex)
        {
            receiver.Error = ex.Message;
        }
        return receiver;
    }

    private void OpenCore(int port)
    {
        var description =
            $"udpsrc port={port} caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96,clock-rate=90000\" ! " +
            "rtpjitterbuffer latency=30 ! rtph264depay ! h264parse ! avdec_h264 ! " +
            "videoconvert ! video/x-raw,format=BGRA ! appsink name=sink drop=true max-buffers=1 sync=false";
        if (Global.ParseLaunch(description) is not Pipeline pipeline)
        {
            Error = "GStreamer did not produce a pipeline";
            return;
        }
        m_Pipeline = pipeline;
        m_AppSink = (AppSink)pipeline.GetByName("sink");
        if (pipeline.SetState(State.Playing) == StateChangeReturn.Failure)
        {
            Error = "Failed to start GStreamer pipeline";
            return;
        }
        var change = pipeline.GetState(out _, out _, ClockTime.FromSeconds(5));
        if (change == StateChangeReturn.Failure)
        {
            Error = "GStreamer pipeline failed to reach PLAYING";
            return;
        }

        IsOpened = true;
    }

    public bool Read(ref FrameBuffer frame)
    {
        if (!IsOpened)
        {
            return false;
        }

        try
        {
            return ReadCore(ref frame);
        }
        finally
        {
            GstSharp.DrainPendingReleases();
        }
    }

    private bool ReadCore(ref FrameBuffer frame)
    {
        using var sample = m_AppSink.TryPullSample(ClockTime.FromMilliseconds(100));
        if (sample != null)
        {
            using var caps = sample.GetCaps();
            using var buffer = sample.GetBuffer();
            if (caps?.GetSize() > 0 && buffer != null)
            {
                using var structure = caps.GetStructure(0);
                if (structure.GetInt("width", out var width) && structure.GetInt("height", out var height))
                {
                    var data = frame?.Data;
                    var byteCount = width * height * 4;
                    if (data == null || data.Length != byteCount)
                    {
                        data = new byte[byteCount];
                    }
                    using var map = buffer.Map(MapFlags.Read);
                    map.Span.CopyTo(data);
                    frame = new(data, width, height);
                    return true;
                }
            }
        }
        return false;
    }

    public void Dispose()
    {
        IsOpened = false;

        m_AppSink = null;
        if (m_Pipeline != null)
        {
            m_Pipeline.SetState(State.Null);
            m_Pipeline.Dispose();
            m_Pipeline = null;
        }
    }
}
