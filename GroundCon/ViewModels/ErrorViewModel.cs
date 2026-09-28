using Caliburn.Micro;

namespace GroundCon.ViewModels;

class ErrorViewModel(string message) : Screen
{
    public string Message { get; } = message;

    public Task Ok() => TryCloseAsync();
}
