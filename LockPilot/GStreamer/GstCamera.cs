using Gst;
using Gst.App;
using OpenCvSharp;

namespace LockPilot.GStreamer;

class GstCamera : IDisposable
{
    Pipeline m_Pipeline;
    AppSink m_AppSink;

    public bool IsOpened { get; private set; }

    public string Error { get; private set; }

    public static GstCamera Open(AppSettings settings)
    {
        var camera = new GstCamera();
        try
        {
            camera.OpenCore(settings);
        }
        catch (Exception ex)
        {
            camera.Error = ex.Message;
        }
        return camera;
    }

    private void OpenCore(AppSettings settings)
    {
        var source = OperatingSystem.IsWindows() ? $"mfvideosrc device-index={settings.CameraIndex}" : "libcamerasrc";
        var appSinkTail = "videoconvert ! video/x-raw,format=GRAY8 ! appsink name=sink drop=true max-buffers=1 sync=false";
        var encoder = OperatingSystem.IsWindows() ? "mfh264enc" : "x264enc tune=zerolatency speed-preset=ultrafast";
        var rtpTail = $"videoconvert ! {encoder} ! h264parse ! rtph264pay pt=96 config-interval=-1 ! udpsink host={settings.Udp.Host} port={settings.Udp.RtpPort} sync=false";
        var description = $"{source} ! tee name=t " +
            $"t. ! queue max-size-bytes=0 max-size-time=0 max-size-buffers=2 ! {rtpTail} " +
            $"t. ! queue max-size-bytes=0 max-size-time=0 max-size-buffers=1 leaky=downstream ! {appSinkTail}";
        if (Global.ParseLaunch(description) is not Pipeline pipeline)
        {
            Error = "GStreamer did not produce a pipeline";
            return;
        }
        m_Pipeline = pipeline;
        if (pipeline.GetByName("sink") is not AppSink appSink)
        {
            Error = "GStreamer appsink not found";
            return;
        }
        m_AppSink = appSink;
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

    public bool Read(Mat image)
    {
        if (!IsOpened)
        {
            return false;
        }

        try
        {
            return ReadCore(image);
        }
        finally
        {
            GstSharp.DrainPendingReleases();
        }
    }

    private bool ReadCore(Mat image)
    {
        using var sample = m_AppSink.TryPullSample(ClockTime.FromSeconds(2));
        if (sample != null)
        {
            using var caps = sample.GetCaps();
            using var buffer = sample.GetBuffer();
            if (caps?.GetSize() > 0 && buffer != null)
            {
                using var structure = caps.GetStructure(0);
                if (structure.GetInt("width", out var width) && structure.GetInt("height", out var height))
                {
                    using var map = buffer.Map(MapFlags.Read);
                    image.Create(height, width, MatType.CV_8UC1);
                    map.Span.CopyTo(image.AsSpan<byte>());
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
