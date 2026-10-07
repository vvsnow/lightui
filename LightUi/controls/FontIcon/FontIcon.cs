using System.Windows;
using System.Windows.Controls;

namespace LightUi.Controls;

/// <summary>
/// 用图标字体渲染字符图标。Glyph 是字体码位（string）。
/// 字体是随程序集打包的 Assets/Fonts/SegoeIcons.TTF（默认样式里用 pack URI 引用），
/// 不依赖系统是否安装了 Segoe Fluent Icons。
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