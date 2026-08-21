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
Cv2.NamedWindow(windowName, WindowFlags.AutoSize);

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

    Cv2.ImShow(windowName, image);

    var key = Cv2.WaitKey(1);
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

Cv2.DestroyWindow(windowName);

static Scalar ToScalar(int[] bgr) => new(bgr[0], bgr[1], bgr[2]);
