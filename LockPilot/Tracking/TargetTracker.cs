using OpenCvSharp;
using System.Diagnostics;

namespace LockPilot.Tracking;

class TargetTracker(AppSettings settings) : IDisposable
{
    readonly LucasKanadeTracker m_Lk = new(settings.MinLkPoints, settings.MaxLkError);
    readonly OrbRelocalizer m_Orb = new();
    readonly Stopwatch m_OrbTimer = new();
    Mat m_Image;

    public void Capture(Mat image, Rect aimRect)
    {
        using var roiImage = new Mat(image, aimRect);
        m_Orb.SetTemplate(roiImage.Clone());

        m_Image?.Dispose();
        m_Image = new();
        Cv2.CvtColor(image, m_Image, ColorConversionCodes.BGR2GRAY);

        m_Lk.Initialize(m_Image, aimRect);
        DetectionRect = aimRect;
        m_OrbTimer.Restart();
        State = TargetTrackerState.Tracking;
    }

    public void Reset()
    {
        m_Image?.Dispose();
        m_Image = null;

        m_Lk.ClearPoints();
        DetectionRect = new();
        m_OrbTimer.Reset();
        State = TargetTrackerState.Idle;
    }

    public void Dispose()
    {
        m_Image?.Dispose();
        m_Orb.Dispose();
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
        var orbStatus = false;
        if (!m_OrbTimer.IsRunning || m_OrbTimer.Elapsed.TotalSeconds >= settings.OrbIntervalSeconds || !lkStatus)
        {
            orbStatus = m_Orb.Locate(image, out var orbRect);
            if (orbStatus)
            {
                DetectionRect = orbRect;
                m_Lk.Initialize(grayImage, orbRect);
            }
            m_OrbTimer.Restart();
        }
        State = lkStatus || orbStatus ? TargetTrackerState.Tracking : TargetTrackerState.Lost;

        m_Image?.Dispose();
        m_Image = grayImage;
    }
}
