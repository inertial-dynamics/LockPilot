using System.Text.Json;
using System.Text.Json.Serialization;

namespace LockPilot.Shared;

public class CommandMessage
{
    public Command Cmd { get; init; }

    static readonly JsonSerializerOptions m_JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(Command command) => JsonSerializer.Serialize(new CommandMessage { Cmd = command }, m_JsonOptions);

    public static Command Deserialize(string json) => JsonSerializer.Deserialize<CommandMessage>(json, m_JsonOptions).Cmd;
}
