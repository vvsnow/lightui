using Caliburn.Micro;

using System.Diagnostics;

namespace LightUi.Demo.ViewModels.Pages;

internal class ButtonsViewModel : Screen
{
    public int Counter { get; set; } = 0;

    public void Increase() => Counter++;
    public void Decrease() => Counter--;

    public void DoSomeAction()
    {
        Debug.WriteLine("Do some action");
    }
}