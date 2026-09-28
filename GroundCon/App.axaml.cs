using Avalonia;
using Avalonia.Markup.Xaml;

namespace GroundCon;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _ = new Bootstrapper();
    }
}
