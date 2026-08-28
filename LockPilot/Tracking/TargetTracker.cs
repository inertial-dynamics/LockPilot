using OpenCvSharp;
using System.Diagnostics;

namespace LockPilot.Tracking;

class TargetTracker(AppSettings settings) : IDisposable
{
    readonly LucasKanadeTracker m_Lk = new(settings.MinLkPoints, settings.MaxLkError);
    readonly YoloRelocalizer m_Relocalizer = new(settings.Yolo);
    readonly Stopwatch m_RelocalizeTimer = new();
    Mat m_Image;

    public void Capture(Mat image, Rect aimRect)
    {
        m_Relocalizer.LockOn(image, aimRect);

        m_Image?.Dispose();
        m_Image = new();
        Cv2.CvtColor(image, m_Image, ColorConversionCodes.BGR2GRAY);

        m_Lk.Initialize(m_Image, aimRect);
        DetectionRect = aimRect;
        m_RelocalizeTimer.Restart();
        State = TargetTrackerState.Tracking;
    }

    public void Reset()
    {
        m_Image?.Dispose();
        m_Image = null;

        m_Lk.ClearPoints();
        m_Relocalizer.Reset();
        DetectionRect = new();
        m_RelocalizeTimer.Reset();
        State = TargetTrackerState.Idle;
    }

    public void Dispose()
    {
        m_Image?.Dispose();
        m_Relocalizer.Dispose();
    }

    public TargetTrackerState State { get; private set; }

    public Rect DetectionRect { get; private set; }

    public void Update(Mat image)
    {
        if (State == TargetTrackerState.Idle)
        {
            return;
        }

        var grayImage = new Mat();
        Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);

        var lkStatus = false;
        if (m_Image != null)
        {
            lkStatus = m_Lk.Track(m_Image, grayImage, out var lkRect);
            if (lkStatus)
            {
                DetectionRect = lkRect;
            }
        }
        var relocStatus = false;
        if (!m_RelocalizeTimer.IsRunning || m_RelocalizeTimer.Elapsed.TotalSeconds >= settings.RelocalizeIntervalSeconds || !lkStatus)
        {
            relocStatus = m_Relocalizer.Locate(image, DetectionRect, out var relocRect);
            if (relocStatus)
            {
                DetectionRect = relocRect;
                m_Lk.Initialize(grayImage, relocRect);
            }
            m_RelocalizeTimer.Restart();
        }
        State = lkStatus || relocStatus ? TargetTrackerState.Tracking : TargetTrackerState.Lost;

        m_Image?.Dispose();
        m_Image = grayImage;
    }
}
