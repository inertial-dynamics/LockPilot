using System.Text.Json;

namespace GroundCon;

class AppSettings
{
    public required int AimWidth { get; init; }

    public required int AimHeight { get; init; }

    public required int OverlayPort { get; init; }

    public required int RtpPort { get; init; }

    public required LockPilotSettings LockPilot { get; init; }

    public static AppSettings Load(string path) => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));

    public class LockPilotSettings
    {
        public required string Host { get; init; }

        public required int CommandPort { get; init; }
    }
}
