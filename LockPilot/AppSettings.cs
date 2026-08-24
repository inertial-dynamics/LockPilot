using System.Text.Json;

namespace LockPilot;

class AppSettings
{
    public required int CameraIndex { get; init; }

    public PiCameraSettings PiCamera { get; init; }

    public required int AimWidth { get; init; }

    public required int AimHeight { get; init; }

    public required int[] AimColorBgr { get; init; }

    public required int[] DetectionColorBgr { get; init; }

    public required double OrbIntervalSeconds { get; init; }

    public required int MinLkPoints { get; init; }

    public required double MaxLkError { get; init; }

    public static AppSettings Load(string path) => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));

    public class PiCameraSettings
    {
        public required int Width { get; init; }

        public required int Height { get; init; }
    }
}
