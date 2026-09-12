using LockPilot;
using LockPilot.GStreamer;
using LockPilot.Shared;
using LockPilot.Tracking;
using NetMQ;
using OpenCvSharp;

try
{
    GstRuntime.Initialize();
}
catch (Exception ex)
{
    Console.WriteLine($"Cannot initialize GStreamer: {ex.Message}");
    return;
}

var settings = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

try
{
    using var capture = GstCamera.Open(settings);
    if (!capture.IsOpened)
    {
        Console.WriteLine($"Cannot open camera via GStreamer: {capture.Error}");
        return;
    }

    using var tracker = new TargetTracker(settings);
    using var overlayWriter = new OverlayWriter(settings.GroundStation.Host, settings.GroundStation.OverlayPort);
    using var commandServer = new CommandServer(settings.CommandPort);
    using var image = new Mat();

    Console.WriteLine($"RTP H.264 to {settings.GroundStation.Host}:{settings.GroundStation.RtpPort}");
    Console.WriteLine($"Overlay JSON to {settings.GroundStation.Host}:{settings.GroundStation.OverlayPort}");
    Console.WriteLine($"Commands TCP on {settings.CommandPort}");
    while (true)
    {
        Thread.Sleep(1);

        if (!capture.Read(image))
        {
            Console.WriteLine("Failed to read frame from camera");
            break;
        }

        tracker.Update(image);
        overlayWriter.Write(tracker);

        var quit = false;
        while (commandServer.TryDequeue(out var command))
        {
            if (command == Command.Quit)
            {
                quit = true;
                break;
            }
            if (command == Command.Reset)
            {
                tracker.Reset();
                continue;
            }
            if (command == Command.Capture)
            {
                var aimRect = Geometry.GetCenterRect(image.Width, image.Height, settings.AimWidth, settings.AimHeight);
                tracker.Capture(image, aimRect);
            }
        }
        if (quit)
        {
            break;
        }
    }
}
finally
{
    NetMQConfig.Cleanup();
}
