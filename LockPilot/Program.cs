using LockPilot;
using LockPilot.GStreamer;
using LockPilot.Tracking;
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

using var capture = GstCamera.Open(settings);
if (!capture.IsOpened)
{
    Console.WriteLine($"Cannot open camera via GStreamer: {capture.Error}");
    return;
}

using var tracker = new TargetTracker(settings);
using var image = new Mat();

Console.WriteLine($"RTP H.264 to {settings.Rtp.Host}:{settings.Rtp.Port}");
Console.WriteLine("Controls: Space = capture/re-acquire, R = reset, Esc/Q = quit");
while (true)
{
    Thread.Sleep(1);

    if (!capture.Read(image))
    {
        Console.WriteLine("Failed to read frame from camera");
        break;
    }

    tracker.Update(image);

    if (tracker.State == TargetTrackerState.Tracking)
    {
        var box = tracker.DetectionRect;
        Console.WriteLine($"{tracker.State} {box.X},{box.Y},{box.Width},{box.Height}");
    }
    else
    {
        Console.WriteLine(tracker.State);
    }

    var key = ReadKey();
    if (key is (int)ConsoleKey.Escape or 'q' or 'Q')
    {
        break;
    }
    if (key is 'r' or 'R')
    {
        tracker.Reset();
        continue;
    }
    if (key == ' ')
    {
        var aimRect = Geometry.GetCenterRect(image.Width, image.Height, settings.AimWidth, settings.AimHeight);
        tracker.Capture(image, aimRect);
    }
}

static int ReadKey()
{
    if (!Console.KeyAvailable)
    {
        return -1;
    }
    var keyInfo = Console.ReadKey(true);
    return keyInfo.Key == ConsoleKey.Escape ? (int)ConsoleKey.Escape : keyInfo.KeyChar;
}
