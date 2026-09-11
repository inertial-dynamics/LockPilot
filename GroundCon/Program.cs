using GroundCon;
using LockPilot.Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;

var settings = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

using var overlayToken = new CancellationTokenSource();
using var overlayClient = new UdpClient(new IPEndPoint(IPAddress.Any, settings.OverlayPort));
using var commandClient = new CommandClient(settings.LockPilot.Host, settings.LockPilot.CommandPort);

Console.WriteLine($"Overlay JSON on UDP {settings.OverlayPort}");
Console.WriteLine($"Commands TCP to {settings.LockPilot.Host}:{settings.LockPilot.CommandPort}");
Console.WriteLine("Controls: Space = capture/re-acquire, R = reset, Esc/Q = quit");

_ = Task.Run(ReceiveOverlay);

while (true)
{
    if (Console.KeyAvailable)
    {
        var command = ReadCommand();
        if (command != null)
        {
            commandClient.Send(command.Value);
            if (command == Command.Quit)
            {
                break;
            }
        }
    }
    else
    {
        Thread.Sleep(1);
    }
}

overlayToken.Cancel();

static Command? ReadCommand()
{
    var keyInfo = Console.ReadKey(true);
    return char.ToLower(keyInfo.KeyChar) switch
    {
        'q' => Command.Quit,
        'r' => Command.Reset,
        ' ' => Command.Capture,
        _ => keyInfo.Key == ConsoleKey.Escape ? Command.Quit : null
    };
}

async Task ReceiveOverlay()
{
    while (!overlayToken.IsCancellationRequested)
    {
        try
        {
            var result = await overlayClient.ReceiveAsync(overlayToken.Token);
            Console.WriteLine(Encoding.UTF8.GetString(result.Buffer));
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}
