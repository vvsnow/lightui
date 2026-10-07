using Caliburn.Micro;

using LightUi.Demo.Models;
using LightUi.Demo.ViewModels.Sections;

namespace LightUi.Demo.ViewModels;

internal class ShellViewModel : Conductor<IScreen>.Collection.OneActive
{
    public ThemeViewModel ThemeViewModel { get; }
    public List<NavItem> NavItems { get; } = [];

    public ShellViewModel(
        HomeViewModel homeViewModel,
        ButtonsViewModel buttonsViewModel,
        CommonControlsViewModel commonControlsViewModel,
        CustomTitleBarViewModel customTitleBarViewModel,
        ExtendedControlsViewModel extendedControlViewModel,
        ThemeViewModel themeViewModel)
    {
        NavItems.Add(new NavItem("Home", homeViewModel));
        NavItems.Add(new NavItem("Buttons", buttonsViewModel));
        NavItems.Add(new NavItem("Common Controls", commonControlsViewModel));
        NavItems.Add(new NavItem("Extended Control", extendedControlViewModel));
        NavItems.Add(new NavItem("Custom Title Bar", customTitleBarViewModel));

        ThemeViewModel = themeViewModel;
    }

    protected override void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        ActiveItem = NavItems.First().ViewModel;
    }
}
