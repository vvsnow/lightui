namespace LightUi.Controls;

/// <summary>
/// 开关：继承 ToggleButton，On / Off 两态，不做拖动动画。
///
/// 用法：
/// <code>
/// &lt;ui:ToggleSwitch OnContent="开" OffContent="关" /&gt;
/// </code>
/// </summary>
public class ToggleSwitch : ToggleButton
{
    public static readonly DependencyProperty OnContentProperty =
        DependencyProperty.Register(
            nameof(OnContent),
            typeof(object),
            typeof(ToggleSwitch),
            new PropertyMetadata(null));

    public object? OnContent
    {
        get => GetValue(OnContentProperty);
        set => SetValue(OnContentProperty, value);
    }

    public static readonly DependencyProperty OffContentProperty =
        DependencyProperty.Register(
            nameof(OffContent),
            typeof(object),
            typeof(ToggleSwitch),
            new PropertyMetadata(null));

    public object? OffContent
    {
        get => GetValue(OffContentProperty);
        set => SetValue(OffContentProperty, value);
    }
}