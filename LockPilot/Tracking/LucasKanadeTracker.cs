using OpenCvSharp;

namespace LockPilot.Tracking;

class LucasKanadeTracker(int minLkPoints, double maxLkError)
{
    readonly List<Point2f> m_Points = [];

    public void Initialize(Mat image, Rect rect)
    {
        using var mask = new Mat(image.Size(), MatType.CV_8UC1, new(0));
        Cv2.Rectangle(mask, rect, new(0xff), -1);
        m_Points.Clear();
        m_Points.AddRange(Cv2.GoodFeaturesToTrack(image, 100, 0.01, 5, mask, 7, false, 0));
    }

    public void ClearPoints() => m_Points.Clear();

    public bool Track(Mat prevImage, Mat image, out Rect box)
    {
        box = new();
        if (m_Points.Count == 0)
        {
            return false;
        }

        using var prevPointsMat = Mat.FromPixelData(m_Points.Count, 1, MatType.CV_32FC2, m_Points.ToArray());
        using var pointsMat = new Mat();
        using var statusMat = new Mat();
        using var errorMat = new Mat();
        Cv2.CalcOpticalFlowPyrLK(prevImage, image, prevPointsMat, pointsMat, statusMat, errorMat, new(21, 21), 3, new(CriteriaTypes.Count | CriteriaTypes.Eps, 30, 0.01));
        if (pointsMat.Empty() || statusMat.Empty() || errorMat.Empty())
        {
            m_Points.Clear();
            return false;
        }
        pointsMat.GetArray(out Point2f[] points);
        statusMat.GetArray(out byte[] statuses);
        errorMat.GetArray(out float[] errors);

        m_Points.Clear();
        for (var i = 0; i < points.Length; ++i)
        {
            if (statuses[i] != 0 && errors[i] <= maxLkError)
            {
                var point = points[i];
                if (point.X >= 0 && point.Y >= 0 && point.X < image.Width && point.Y < image.Height)
                {
                    m_Points.Add(point);
                }
            }
        }
        if (m_Points.Count < minLkPoints)
        {
            return false;
        }

        box = Geometry.GetBoundingBox(m_Points, image.Width, image.Height);
        return true;
    }
}
