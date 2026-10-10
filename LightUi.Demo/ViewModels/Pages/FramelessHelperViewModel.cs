using Caliburn.Micro;

using LightUi.Demo.ViewModels.Dialogs;

namespace LightUi.Demo.ViewModels.Pages;

internal class FramelessHelperViewModel : Screen
{
    private readonly IWindowManager windowManager;
    public FramelessHelperViewModel(IWindowManager windowManager)
    {
        this.windowManager = windowManager;
    }

    public void ShowFramelessWindow()
    {
        windowManager.ShowWindowAsync(new FramelessWindowViewModel());
    }
}
