using GroundCon;
using LockPilot.Shared;
using NetMQ;

var settings = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

try
{
    using var overlayToken = new CancellationTokenSource();
    using var overlayReader = new OverlayReader(settings.OverlayPort);
    using var commandClient = new CommandClient(settings.LockPilot.Host, settings.LockPilot.CommandPort);

    Console.WriteLine($"Overlay JSON on UDP {settings.OverlayPort}");
    Console.WriteLine($"Commands TCP to {settings.LockPilot.Host}:{settings.LockPilot.CommandPort}");
    Console.WriteLine("Controls: Space = capture/re-acquire, R = reset, Esc/Q = quit");

    var overlayTask = Task.Run(ReceiveOverlay);

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
    overlayTask.Wait();

    async Task ReceiveOverlay()
    {
        var textLength = 0;
        while (!overlayToken.IsCancellationRequested)
        {
            try
            {
                var text = await overlayReader.ReceiveAsync(overlayToken.Token);
                Console.Write($"\r{DateTime.Now:HH:mm:ss} => {text.PadRight(Math.Max(textLength, text.Length))}");
                textLength = text.Length;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
finally
{
    NetMQConfig.Cleanup();
}

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
