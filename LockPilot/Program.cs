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
    Console.WriteLine($"Overlay MessagePack to {settings.GroundStation.Host}:{settings.GroundStation.OverlayPort}");
    Console.WriteLine($"Commands TCP on {settings.CommandPort}");

    var aimWidth = 0;
    var aimHeight = 0;
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
        while (commandServer.TryDequeue(out var message))
        {
            if (message is CaptureSetupMessage captureSetupMessage)
            {
                HandleCaptureSetupMessage(captureSetupMessage);
                continue;
            }
            if (message is CommandMessage commandMessage)
            {
                if (HandleCommandMessage(commandMessage))
                {
                    quit = true;
                    break;
                }
            }
        }
        if (quit)
        {
            break;
        }
    }

    void HandleCaptureSetupMessage(CaptureSetupMessage captureSetupMessage)
    {
        aimWidth = captureSetupMessage.AimWidth;
        aimHeight = captureSetupMessage.AimHeight;
        Console.WriteLine($"Capture setup {aimWidth}x{aimHeight}");
    }

    bool HandleCommandMessage(CommandMessage commandMessage)
    {
        Console.WriteLine($"Command {commandMessage.Command}");
        switch (commandMessage.Command)
        {
            case Command.Quit:
                return true;
            case Command.Reset:
                tracker.Reset();
                break;
            case Command.Capture:
                var aimRect = Geometry.GetCenterRect(image.Width, image.Height, aimWidth, aimHeight);
                Console.WriteLine($"Capture aim {aimRect.X},{aimRect.Y} {aimRect.Width}x{aimRect.Height}");
                tracker.Capture(image, aimRect);
                break;
        }
        return false;
    }
}
finally
{
    NetMQConfig.Cleanup();
}
