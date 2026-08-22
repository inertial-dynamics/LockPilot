using LockPilot;
using LockPilot.Tracking;
using OpenCvSharp;

var settings = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
var aimColor = ToScalar(settings.AimColorBgr);
var detectionColor = ToScalar(settings.DetectionColorBgr);

using var capture = new VideoCapture(settings.CameraIndex);
if (!capture.IsOpened())
{
    Console.WriteLine($"Cannot open camera {settings.CameraIndex}");
    return;
}

using var tracker = new TargetTracker(settings);
using var image = new Mat();

var windowName = nameof(LockPilot);
var writer = args.Length > 0 ? new UdpWriter(args[0]) : null;
if (writer != null)
{
    Console.WriteLine("On the receiver run: ffplay -fflags nobuffer -flags low_delay -framedrop -probesize 32 -analyzeduration 0 -sync ext -f mjpeg \"udp://0.0.0.0:5000?fifo_size=500&overrun_nonfatal=1\"");
}
else
{
    Cv2.NamedWindow(windowName, WindowFlags.AutoSize);
}

Console.WriteLine("Controls: Space = capture/re-acquire, R = reset, Esc/Q = quit");
while (true)
{
    if (!capture.Read(image))
    {
        Console.WriteLine("Failed to read frame from camera");
        break;
    }

    tracker.Update(image);

    var aimRect = Geometry.GetCenterRect(image.Width, image.Height, settings.AimWidth, settings.AimHeight);
    Cv2.Rectangle(image, aimRect, aimColor, 2);
    if (tracker.State == TargetTrackerState.Tracking)
    {
        Cv2.Rectangle(image, tracker.DetectionRect, detectionColor, 2);
    }
    Cv2.PutText(image, $"{tracker.State}", new(10, 28), HersheyFonts.HersheySimplex, 0.7, new(0xff, 0, 0), 2);

    if (writer != null)
    {
        writer.Write(image, 80);
    }
    else
    {
        Cv2.ImShow(windowName, image);
    }

    var key = ReadKey(writer != null);
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
        tracker.Capture(image, aimRect);
        continue;
    }
}

if (writer != null)
{
    writer.Dispose();
}
else
{
    Cv2.DestroyWindow(windowName);
}

static Scalar ToScalar(int[] bgr) => new(bgr[0], bgr[1], bgr[2]);

static int ReadKey(bool fromConsole)
{
    if (!fromConsole)
    {
        return Cv2.WaitKey(1);
    }
    if (!Console.KeyAvailable)
    {
        return -1;
    }
    var keyInfo = Console.ReadKey(true);
    return keyInfo.Key == ConsoleKey.Escape ? (int)ConsoleKey.Escape : keyInfo.KeyChar;
}
