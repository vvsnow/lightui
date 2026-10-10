using System.Diagnostics;

namespace LightUi.Controls;

/// <summary>
/// 超链接外观的按钮：点击后用系统 shell 打开 <see cref="NavigateUri"/>。
///
/// 用法：
/// <code>
/// &lt;ui:HyperlinkButton Content="了解详情" NavigateUri="https://example.com" /&gt;
/// </code>
/// 不设 NavigateUri 时就是个普通按钮，可以自己挂 Click。
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
            // 打不开（Uri 非法 / 没有 shell）不崩 UI，但要留痕，免得真问题被吞掉
            Debug.WriteLine($"HyperlinkButton 打开 \"{NavigateUri}\" 失败: {ex}");
        }
    }
}