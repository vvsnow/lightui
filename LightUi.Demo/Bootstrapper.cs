using Caliburn.Micro;

using LightUi.Demo.ViewModels;
using LightUi.Demo.ViewModels.Sections;

using System.Windows;

namespace LightUi.Demo;

internal class Bootstrapper : BootstrapperBase
{
    private readonly SimpleContainer container = new();

    public Bootstrapper()
    {
        Initialize();
    }

    protected override void Configure()
    {
        container.Singleton<IWindowManager, WindowManager>();
        container.Singleton<IEventAggregator, EventAggregator>();

        container.PerRequest<ShellViewModel>();
        container.PerRequest<HomeViewModel>();
        container.PerRequest<ButtonsViewModel>();
        container.PerRequest<CommonControlsViewModel>();
        container.PerRequest<ExtendedControlsViewModel>();
        container.PerRequest<FlyoutViewModel>();

        container.PerRequest<ThemeViewModel>();
    }

    protected override void BuildUp(object instance)
    {
        container.BuildUp(instance);
    }

    protected override IEnumerable<object> GetAllInstances(Type service)
    {
        return container.GetAllInstances(service);
    }

    protected override object GetInstance(Type service, string key)
    {
        return container.GetInstance(service, key);
    }

    protected override void OnStartup(object sender, StartupEventArgs e)
    {
        DisplayRootViewForAsync<ShellViewModel>();
    }
}
