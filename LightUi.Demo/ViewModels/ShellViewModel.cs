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
        FramelessHelperViewModel framelessHelperViewModel,
        VersionViewModel versionViewModel)
    {
        this.windowManager = windowManager;

        NavItems.Add(new NavItem("Home", homeViewModel));
        NavItems.Add(new NavItem("Button", buttonsViewModel));
        NavItems.Add(new NavItem("Common Control", commonControlsViewModel));
        NavItems.Add(new NavItem("Extended Control", extendedControlViewModel));
        NavItems.Add(new NavItem("Flyout Control", flyoutViewModel));
        NavItems.Add(new NavItem("Frameless Window", framelessHelperViewModel));
        NavItems.Add(new NavItem("Version Information", versionViewModel));

    }

    public void ToggleTheme()
    {
        ThemeManager.ToggleTheme();
    }


    protected override void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        ActiveItem = NavItems.First().ViewModel;
    }


}
