using Caliburn.Micro;

using LightUi.Demo.Models;
using LightUi.Demo.ViewModels.Sections;

namespace LightUi.Demo.ViewModels;

internal class ShellViewModel : Conductor<IScreen>.Collection.OneActive
{
    private readonly IWindowManager windowManager;

    public ThemeViewModel ThemeViewModel { get; }
    public List<NavItem> NavItems { get; } = [];

    public ShellViewModel(
        IWindowManager windowManager,
        HomeViewModel homeViewModel,
        ButtonsViewModel buttonsViewModel,
        CommonControlsViewModel commonControlsViewModel,
        ExtendedControlsViewModel extendedControlViewModel,
        FlyoutViewModel flyoutViewModel,
        ThemeViewModel themeViewModel)
    {
        this.windowManager = windowManager;

        NavItems.Add(new NavItem("Home", homeViewModel));
        NavItems.Add(new NavItem("Buttons", buttonsViewModel));
        NavItems.Add(new NavItem("Common Controls", commonControlsViewModel));
        NavItems.Add(new NavItem("Extended Control", extendedControlViewModel));
        NavItems.Add(new NavItem("Flyout 控件", flyoutViewModel));

        ThemeViewModel = themeViewModel;
    }

    /// <summary>弹一个 XAML 声明的无边框子窗口：它没写 Style，样式由 FramelessHelper 挂载时补上。</summary>
    public void ShowFramelessWindow()
    {
        // 每次弹新的：复用已关闭的 Screen 会带着旧的生命周期状态
        _ = windowManager.ShowWindowAsync(new FramelessWindowViewModel());
    }

    protected override void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        ActiveItem = NavItems.First().ViewModel;
    }
}
