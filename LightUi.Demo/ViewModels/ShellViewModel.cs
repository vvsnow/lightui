using Caliburn.Micro;

using LightUi.Demo.Models;
using LightUi.Demo.ViewModels.Pages;

namespace LightUi.Demo.ViewModels;

internal class ShellViewModel : Conductor<IScreen>.Collection.OneActive
{
    private readonly IWindowManager windowManager;

    public List<NavItem> NavItems { get; } = [];

    public ShellViewModel(
        IWindowManager windowManager,
        HomeViewModel homeViewModel,
        ButtonsViewModel buttonsViewModel,
        CommonControlsViewModel commonControlsViewModel,
        ExtendedControlsViewModel extendedControlViewModel,
        FlyoutViewModel flyoutViewModel,
        FramelessHelperViewModel framelessHelperViewModel)
    {
        this.windowManager = windowManager;

        NavItems.Add(new NavItem("Home", homeViewModel));
        NavItems.Add(new NavItem("Button", buttonsViewModel));
        NavItems.Add(new NavItem("Common Control", commonControlsViewModel));
        NavItems.Add(new NavItem("Extended Control", extendedControlViewModel));
        NavItems.Add(new NavItem("Flyout Control", flyoutViewModel));
        NavItems.Add(new NavItem("Frameless Window", framelessHelperViewModel));

    }

    public void ToggleTheme()
    {
        ThemeManager.ApplyTheme(
            ThemeManager.Current.CurrentTheme == ApplicationTheme.Dark
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark);
    }


    protected override void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        ActiveItem = NavItems.First().ViewModel;
    }
}
