using Gst;
using Gst.App;
using Gst.Interop;

namespace LockPilot.GStreamer;

static class GstRuntime
{
    public static void Initialize()
    {
        var options = new GstSharpOptions();
        if (OperatingSystem.IsWindows())
        {
            options.WindowsFlavor = GstFlavor.Msvc;
        }
        GstApp.Initialize(options);
    }
}
