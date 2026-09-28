using Avalonia.Controls.ApplicationLifetimes;
using Caliburn.Micro;
using GroundCon.ViewModels;

namespace GroundCon;

class Bootstrapper : BootstrapperBase
{
    readonly SimpleContainer m_Container = new();

    public Bootstrapper()
    {
        Initialize();
    }

    protected override void Configure()
    {
        m_Container
            .Singleton<IWindowManager, WindowManager>()
            .PerRequest<MainViewModel>();
    }

    protected override async void OnStartup(object sender, ControlledApplicationLifetimeStartupEventArgs e)
    {
        try
        {
            await DisplayRootViewFor<MainViewModel>();
        }
        catch (Exception ex)
        {
            var error = new ErrorViewModel(ex.GetBaseException().Message);
            error.Deactivated += (_, e) =>
            {
                if (e.WasClosed && Application.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                return Task.CompletedTask;
            };
            var windowManager = m_Container.GetInstance<IWindowManager>();
            await windowManager.ShowWindowAsync(error);
        }
    }

    protected override object GetInstance(Type service, string key) => m_Container.GetInstance(service, key);

    protected override IEnumerable<object> GetAllInstances(Type service) => m_Container.GetAllInstances(service);

    protected override void BuildUp(object instance) => m_Container.BuildUp(instance);
}
