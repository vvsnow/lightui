using LightUi.Controls.Primitives;

using System.Windows.Markup;

namespace LightUi.Controls;

/// <summary>
/// 轻量弹出层，承载任意 <see cref="Content"/>；挂在 DropDownButton.Flyout 或
/// FlyoutService.Flyout 上，不单独显示。
///
/// 用法：
/// <code>
/// &lt;ui:DropDownButton.Flyout&gt;
///     &lt;ui:Flyout Placement="Bottom"&gt;
///         &lt;TextBlock Text="内容" /&gt;
///     &lt;/ui:Flyout&gt;
/// &lt;/ui:DropDownButton.Flyout&gt;
/// </code>
/// </summary>
[ContentProperty(nameof(Content))]
public class Flyout : FlyoutBase
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(Flyout),
            new PropertyMetadata(null));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    protected override FrameworkElement CreatePresenter()
    {
        return new ContentControl
        {
            Content = Content,
            Style = (Style)Application.Current.TryFindResource("LightUiFlyoutPresenterStyle")
                       ?? new Style(typeof(ContentControl)),
        };
    }
}