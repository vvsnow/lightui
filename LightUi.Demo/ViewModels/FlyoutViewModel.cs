using Caliburn.Micro;

using System.Diagnostics;

namespace LightUi.Demo.ViewModels;

internal class FlyoutViewModel : Screen
{
    public void NewFile()
    {
        Debug.WriteLine("NewFile Executed");
    }
}
