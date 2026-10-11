using Caliburn.Micro;

using System.Diagnostics;

namespace LightUi.Demo.ViewModels.Pages;

internal class FlyoutViewModel : Screen
{
    public bool IsFlyout { get; set; }

    public void OnIsFlyoutChanged(bool oldValue, bool newValue)
    {

    }

    public void CloseFlyout()
    {
        Debug.WriteLine("Flyout Closed");
    }

    public void NewFile()
    {
        Debug.WriteLine("NewFile Executed");
    }
}
