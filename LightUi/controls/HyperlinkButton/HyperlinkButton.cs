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
        catch (Exception ex)
        {
            // 设计期 / 没有 shell 时不崩 UI，但要留痕：裸 catch 会把 Uri 非法、
            // Process.Start 失败之类的真问题一起吞掉，事后无从排查。
            Debug.WriteLine($"HyperlinkButton 打开 \"{NavigateUri}\" 失败: {ex}");
        }
    }
}