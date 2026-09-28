using Avalonia.Controls;
using GroundCon.ViewModels;

namespace GroundCon.Views;

public partial class MainView : Window, IMainView
{
    public MainView()
    {
        InitializeComponent();
    }

    public void InvalidateVideo() => Video.InvalidateVisual();

    private void OnVideoSizeChanged(object sender, SizeChangedEventArgs e) => ((MainViewModel)DataContext).OnVideoSizeChanged(e.NewSize);
}
