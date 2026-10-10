namespace LightUi.Controls;

/// <summary>
/// 用图标字体渲染字符图标；字体随程序集打包（Assets/Fonts/SegoeIcons.TTF），
/// 不依赖系统装没装 Segoe Fluent Icons。
///
/// 用法：
/// <code>
/// &lt;ui:FontIcon Glyph="&amp;#xE711;" /&gt;
/// &lt;ui:FontIcon Glyph="&amp;#xE711;" FontSize="16" /&gt;
/// </code>
/// 默认样式刻意不设 Foreground：让前景色跟随父级（写了具体 brush 会盖掉继承）。
/// </summary>
public class FontIcon : TextBlock
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph),
            typeof(string),
            typeof(FontIcon),
            new PropertyMetadata(string.Empty));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}