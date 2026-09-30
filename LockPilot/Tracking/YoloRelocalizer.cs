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

    public string ClassName { get; private set; }

    public float Confidence { get; private set; }

    public int? ElapsedMilliseconds { get; private set; }

    public void LockOn(Mat image, Rect aimRect)
    {
        ClassName = null;
        Confidence = 0;

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
            Keep(bestDetection);
        }
    }

    public bool Locate(Mat image, Rect hintRect, out Rect box)
    {
        box = new();
        if (ClassName == null || hintRect.Width <= 0 || hintRect.Height <= 0)
        {
            return false;
        }

        var hintCenter = Center(hintRect);
        Detection bestDetection = null;
        var bestDistance = double.MaxValue;
        foreach (var detection in Detect(image))
        {
            if (detection.Name.Name == ClassName)
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
            if (box.Width > 0 && box.Height > 0)
            {
                Keep(bestDetection);
                return true;
            }
        }
        return false;
    }

    public void Reset()
    {
        ClassName = null;
        Confidence = 0;
        ElapsedMilliseconds = null;
    }

    public void Dispose() => m_Predictor.Dispose();

    private void Keep(Detection detection)
    {
        ClassName = detection.Name.Name;
        Confidence = detection.Confidence;
    }

    private IEnumerable<Detection> Detect(Mat image)
    {
        if (Cv2.ImEncode(".bmp", image, out var buffer))
        {
            var result = m_Predictor.Detect(buffer);
            ElapsedMilliseconds = (int)Math.Round((result.Speed.Preprocess + result.Speed.Inference + result.Speed.Postprocess).TotalMilliseconds);
            return result;
        }
        return [];
    }

    private static Rect ToRect(Detection detection) => new(detection.Bounds.X, detection.Bounds.Y, detection.Bounds.Width, detection.Bounds.Height);

    private static Point Center(Rect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    private static Point Center(Detection detection) => new(detection.Bounds.X + detection.Bounds.Width / 2, detection.Bounds.Y + detection.Bounds.Height / 2);
}
