using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using OpenCvSharp;

namespace LockPilot.Tracking;

class YoloRelocalizer : IDisposable
{
    readonly YoloPredictor m_Predictor;

    public YoloRelocalizer(AppSettings.YoloSettings settings)
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", settings.ModelName);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"YOLO model not found", modelPath);
        }
        m_Predictor = new(modelPath, new()
        {
            Configuration = new()
            {
                Confidence = settings.Confidence,
                IoU = settings.IoU
            }
        });
    }

    int? m_ClassId;

    public void LockOn(Mat image, Rect aimRect)
                                                                        {
        m_ClassId = null;

        var aimCenter = Center(aimRect);
        Detection bestDetection = null;
        var bestDistance = double.MaxValue;
        foreach (var detection in Detect(image))
        {
            var detectionCenter = Center(detection);
            if (aimRect.Contains(detectionCenter))
            {
                var distance = detectionCenter.DistanceTo(aimCenter);
                if (bestDetection == null || distance < bestDistance)
                {
                    bestDetection = detection;
                    bestDistance = distance;
                }
            }
        }

        if (bestDetection != null)
        {
            m_ClassId = bestDetection.Name.Id;
        }
    }

    public bool Locate(Mat image, Rect hintRect, out Rect box)
    {
        box = new();
        if (m_ClassId == null || hintRect.Width <= 0 || hintRect.Height <= 0)
        {
            return false;
        }

        var hintCenter = Center(hintRect);
        Detection bestDetection = null;
        var bestDistance = double.MaxValue;
        foreach (var detection in Detect(image))
        {
            if (detection.Name.Id == m_ClassId.Value)
            {
                var detectionCenter = Center(detection);
                var distance = detectionCenter.DistanceTo(hintCenter);
                if (bestDetection == null || distance < bestDistance)
                {
                    bestDetection = detection;
                    bestDistance = distance;
                }
            }
        }

        if (bestDetection != null)
        {
            box = ToRect(bestDetection);
            return box.Width > 0 && box.Height > 0;
        }
        return false;
    }

    public void Reset() => m_ClassId = null;

    public void Dispose() => m_Predictor.Dispose();

    private IEnumerable<Detection> Detect(Mat image) => Cv2.ImEncode(".bmp", image, out var buffer) ? m_Predictor.Detect(buffer) : [];

    private static Rect ToRect(Detection detection) => new(detection.Bounds.X, detection.Bounds.Y, detection.Bounds.Width, detection.Bounds.Height);

    private static Point Center(Rect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    private static Point Center(Detection detection) => new(detection.Bounds.X + detection.Bounds.Width / 2, detection.Bounds.Y + detection.Bounds.Height / 2);
}
