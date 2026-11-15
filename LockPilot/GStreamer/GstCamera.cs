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
        var source = DescribeSource(settings);
        var appSinkTail = "appsink name=sink drop=true max-buffers=1 sync=false";
        var encoder = OperatingSystem.IsWindows() ? "videoconvert ! video/x-raw,format=NV12 ! mfh264enc" : "x264enc tune=zerolatency speed-preset=ultrafast";
        var rtpTail = $"{encoder} ! h264parse ! rtph264pay pt=96 config-interval=-1 ! udpsink host={settings.GroundStation.Host} port={settings.GroundStation.RtpPort} sync=false";
        var description = $"{source} ! videoconvert n-threads=0 ! video/x-raw,format=GRAY8 ! tee name=t " +
            $"t. ! queue max-size-bytes=0 max-size-time=0 max-size-buffers=2 ! {rtpTail} " +
            $"t. ! queue max-size-bytes=0 max-size-time=0 max-size-buffers=1 leaky=downstream ! {appSinkTail}";
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

    private static string DescribeSource(AppSettings settings)
    {
        if (settings.Camera.RtpPort != null)
        {
            return $"udpsrc port={settings.Camera.RtpPort} caps=\"application/x-rtp,media=video,encoding-name=H264,payload=96,clock-rate=90000\" ! " +
                "rtpjitterbuffer latency=30 ! rtph264depay ! h264parse ! avdec_h264";
        }

        var device = OperatingSystem.IsWindows() ? "mfvideosrc" : "libcamerasrc";
        return $"{device} ! video/x-raw,framerate={settings.Camera.FrameRate}/1";
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
