using System.Text.Json;

namespace GroundCon;

class AppSettings
{
    public required int AimWidth { get; init; }

    public required int AimHeight { get; init; }

    public required int OverlayPort { get; init; }

    public required int RtpPort { get; init; }

    public required LockPilotSettings LockPilot { get; init; }

    public required MavlinkSettings Mavlink { get; init; }

    public static AppSettings Load(string path) => JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));

    public class LockPilotSettings
    {
        public required string Host { get; init; }

        public required int CommandPort { get; init; }
    }

    public class MavlinkSettings
    {
        public required int Port { get; init; }

        public required byte SystemId { get; init; }

        public required byte ComponentId { get; init; }

        public required int PitchStickPwm { get; init; }

        public required int YawStickPwm { get; init; }

        public required int SendPeriodMs { get; init; }
    }
}
