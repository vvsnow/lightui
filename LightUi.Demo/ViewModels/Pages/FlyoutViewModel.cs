using Caliburn.Micro;

using System.Diagnostics;

namespace LightUi.Demo.ViewModels.Pages;

internal class FlyoutViewModel : Screen
{
    public bool IsFlyout { get; set; }

    /// <summary>弹层开关的状态文字，跟 IsFlyout 联动，给页面上那个 TextBlock 看。</summary>
    public string Status { get; set; } = "弹层未打开";

    public void OnIsFlyoutChanged(bool oldValue, bool newValue)
    {
        Status = newValue ? "弹层已打开" : "弹层已关闭";
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
