using Avalonia;
using GroundCon.GStreamer;
using NetMQ;

namespace GroundCon;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            GstRuntime.Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cannot initialize GStreamer: {ex.Message}");
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            NetMQConfig.Cleanup();
        }
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
}
