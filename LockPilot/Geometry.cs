using OpenCvSharp;

namespace LockPilot;

static class Geometry
{
    public static Rect GetBoundingBox(IReadOnlyCollection<Point2f> points, int frameWidth, int frameHeight, int padding = 10)
    {
        if (points.Count == 0)
        {
            return new();
        }

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var left = Math.Clamp((int)Math.Floor(minX) - padding, 0, frameWidth - 1);
        var top = Math.Clamp((int)Math.Floor(minY) - padding, 0, frameHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(maxX) + padding, 0, frameWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(maxY) + padding, 0, frameHeight);
        return Rect.FromLTRB(left, top, right, bottom);
    }

    public static Rect GetCenterRect(int imageWidth, int imageHeight, int width, int height)
    {
        width = Math.Min(width, imageWidth);
        height = Math.Min(height, imageHeight);
        var x = (imageWidth - width) / 2;
        var y = (imageHeight - height) / 2;
        return new(x, y, width, height);
    }
}
