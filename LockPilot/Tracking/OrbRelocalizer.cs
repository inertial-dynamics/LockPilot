using OpenCvSharp;

namespace LockPilot.Tracking;

class OrbRelocalizer : IDisposable
{
    readonly ORB m_Orb;
    readonly BFMatcher m_Matcher;

    public OrbRelocalizer()
    {
        m_Orb = ORB.Create();
        m_Matcher = new(NormTypes.Hamming);
    }

    Mat m_Template;
    Mat m_TemplateDescriptors;
    KeyPoint[] m_TemplateKeyPoints;

    public void SetTemplate(Mat template)
    {
        m_Template?.Dispose();
        m_TemplateDescriptors?.Dispose();

        m_Template = new();
        Cv2.CvtColor(template, m_Template, ColorConversionCodes.BGR2GRAY);

        m_TemplateDescriptors = new();
        m_Orb.DetectAndCompute(m_Template, null, out m_TemplateKeyPoints, m_TemplateDescriptors);
    }

    public void Dispose()
    {
        m_Template?.Dispose();
        m_TemplateDescriptors?.Dispose();

        m_Orb.Dispose();
        m_Matcher.Dispose();
    }

    public bool Locate(Mat image, out Rect box)
    {
        box = new();
        if (m_TemplateKeyPoints.Length < 4)
        {
            return false;
        }

        using var grayImage = new Mat();
        Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);

        using var imageDescriptors = new Mat();
        m_Orb.DetectAndCompute(grayImage, null, out var imageKeyPoints, imageDescriptors);
        if (imageKeyPoints.Length < 4)
        {
            return false;
        }

        var matches = m_Matcher.KnnMatch(m_TemplateDescriptors, imageDescriptors, 2);
        var goodMatches = matches.Where(match => match.Length > 1 && match[0].Distance < 0.75 * match[1].Distance).Select(match => match[0]).ToList();
        if (goodMatches.Count < 8)
        {
            return false;
        }

        using var srcPoints = InputArray.Create(goodMatches.Select(match => m_TemplateKeyPoints[match.QueryIdx].Pt));
        using var dstPoints = InputArray.Create(goodMatches.Select(match => imageKeyPoints[match.TrainIdx].Pt));
        using var homographyMat = Cv2.FindHomography(srcPoints, dstPoints, HomographyMethods.Ransac, 5);
        if (homographyMat.Empty())
        {
            return false;
        }

        var corners = new[]
        {
            new Point2f(0, 0),
            new Point2f(m_Template.Width, 0),
            new Point2f(m_Template.Width, m_Template.Height),
            new Point2f(0, m_Template.Height)
        };
        var projectedCorners = Cv2.PerspectiveTransform(corners, homographyMat);
        box = Geometry.GetBoundingBox(projectedCorners, image.Width, image.Height, 2);
        return box.Width > 8 && box.Height > 8;
    }
}
