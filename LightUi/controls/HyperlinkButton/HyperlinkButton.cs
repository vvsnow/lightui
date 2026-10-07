using System.Diagnostics;

namespace LightUi.Controls;

/// <summary>
/// 按钮外观的超链接：有 <see cref="Content"/>，可选 <see cref="NavigateUri"/>。
/// </summary>
public class HyperlinkButton : Button
{
    public static readonly DependencyProperty NavigateUriProperty =
        DependencyProperty.Register(
            nameof(NavigateUri),
            typeof(Uri),
            typeof(HyperlinkButton),
            new PropertyMetadata(null));

    public Uri? NavigateUri
    {
        get => (Uri?)GetValue(NavigateUriProperty);
        set => SetValue(NavigateUriProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (NavigateUri is null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(NavigateUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // 设计期 / 没有 shell：吞掉异常，不崩 UI
        }
    }
}