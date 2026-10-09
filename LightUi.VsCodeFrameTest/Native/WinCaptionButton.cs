using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LightUi.VsCodeFrameTest.Native;

/// <summary>
/// 一个标题栏按钮。
///
/// 逐条对应 Electron <c>shell/browser/ui/views/win_caption_button.h</c> /
/// <c>win_caption_button.cc</c>：
/// <list type="bullet">
/// <item><c>base_width_ = WindowFrameUtil::kWindowsCaptionButtonWidth</c>、<c>height_ = kWindowsCaptionButtonHeightRestored</c></item>
/// <item><c>CalculatePreferredSize()</c> → <c>Size(base_width_ + GetBetweenButtonSpacing(), height_)</c></item>
/// <item><c>GetBetweenButtonSpacing()</c> → 第一个按钮为 0，其余 <c>kWindowsCaptionButtonVisualSpacing</c></item>
/// <item><c>GetButtonDisplayOrderIndex()</c> → minimize 0 / maximize·restore 1 / close 2，RTL 下 <c>2 - order</c></item>
/// <item><c>SetSize()</c> → 只接受正数；<c>GetSize()</c> 返回 <c>Size(base_width_, height_)</c></item>
/// <item>背景与符号的配色：普通按钮 hovered alpha <c>0x1A</c>、pressed <c>0x33</c>（叠在符号色上）；
/// 关闭键底色 <c>#E81123</c> 不透明、pressed alpha <c>0x98</c>，悬停/按下时符号转白</item>
/// </list>
///
/// 差异（WPF 没有 views，只能这么落）：Electron 的按钮是 <c>views::Button</c>，自己实现
/// <c>OnPaintBackground</c>/<c>PaintButtonContents</c> 去画；这里背景和符号由 WPF 模板画，
/// 但配色、alpha、符号尺寸（<c>ClampRound(10 * scale)</c>）和左内缩都按上面的值来。
/// </summary>
internal sealed class WinCaptionButton
{
    private WinCaptionButton(CaptionButton kind, bool isCloseButton, Canvas canvas, Path glyph, Border background)
    {
        Kind = kind;
        IsCloseButton = isCloseButton;
        Canvas = canvas;
        Glyph = glyph;
        Background = background;
    }

    public CaptionButton Kind { get; }

    public bool IsCloseButton { get; }

    /// <summary>布局容器：对应 views 里按钮的父 <c>View</c>。</summary>
    public Canvas Canvas { get; }

    /// <summary>符号（对应 <c>PaintSymbol()</c> 画的那个图形）。</summary>
    public Path Glyph { get; }

    /// <summary>背景块（对应 <c>OnPaintBackground()</c> 画的那个矩形）。</summary>
    public Border Background { get; }

    /// <summary>是否可见 / 可用 —— 对应容器 <c>UpdateButtons()</c> 里的 <c>SetVisible</c>/<c>SetEnabled</c>。</summary>
    public bool IsShown { get; set; } = true;

    public bool IsEnabledState { get; set; } = true;

    /// <summary>对应 <c>base_width_</c> / <c>height_</c>。</summary>
    public int BaseWidth { get; private set; } = WindowFrameUtil.KWindowsCaptionButtonWidth;

    public int Height { get; private set; } = WindowFrameUtil.KWindowsCaptionButtonHeightRestored;

    /// <summary>布局出来的矩形（容器坐标系，DIP）。命中判定读的就是它，不看 WPF 元素的实际位置。</summary>
    public Rect Bounds { get; set; }

    /// <summary>对应 <c>WinCaptionButton::GetSize()</c>。</summary>
    public Size GetSize() => new(BaseWidth, Height);

    /// <summary>对应 <c>WinCaptionButton::SetSize()</c>：只接受正数。</summary>
    public void SetSize(Size size)
    {
        if (size.Width > 0)
        {
            BaseWidth = (int)size.Width;
        }

        if (size.Height > 0)
        {
            Height = (int)size.Height;
        }
    }

    /// <summary>对应 <c>WinCaptionButton::CalculatePreferredSize()</c>。</summary>
    public Size CalculatePreferredSize() => new(BaseWidth + GetBetweenButtonSpacing(), Height);

    /// <summary>对应 <c>WinCaptionButton::GetBetweenButtonSpacing()</c>。</summary>
    public int GetBetweenButtonSpacing() =>
        GetButtonDisplayOrderIndex() == 0 ? 0 : WindowFrameUtil.KWindowsCaptionButtonVisualSpacing;

    /// <summary>
    /// 对应 Electron <c>WinCaptionButton::GetButtonDisplayOrderIndex()</c>：
    /// minimize 0 / maximize·restore 1 / close 2，RTL 下取 <c>2 - order</c>。
    /// </summary>
    public int GetButtonDisplayOrderIndex()
    {
        int order = Kind switch
        {
            CaptionButton.Minimize => 0,
            CaptionButton.Maximize or CaptionButton.Restore => 1,
            CaptionButton.Close => 2,
            _ => 0,
        };

        return FlowDirection == FlowDirection.RightToLeft ? 2 - order : order;
    }

    /// <summary>对应 Electron 里的 <c>base::i18n::IsRTL()</c>。</summary>
    private FlowDirection FlowDirection =>
        Canvas.FlowDirection;

    /// <summary>
    /// 对应容器匿名命名空间里的 <c>HitTestCaptionButton(button, point)</c>：
    /// <c>button && button->GetVisible() && button->GetMirroredBounds().Contains(point)</c>。
    /// </summary>
    public bool HitTest(Point containerPoint) => IsShown && Bounds.Contains(containerPoint);

    public static WinCaptionButton Create(CaptionButton kind, bool isCloseButton, Canvas canvas, string glyphKey)
    {
        var background = new Border
        {
            // 对应 OnPaintBackground 里的 GetContentsBounds().Inset(TLBR(0, spacing, 0, 0))
            Margin = new Thickness(WindowFrameUtil.KWindowsCaptionButtonVisualSpacing, 0, 0, 0),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(0),
        };

        var glyph = new Path
        {
            Data = (Geometry)Application.Current.FindResource(glyphKey),
            StrokeThickness = 1,
            // 对应 flags.setAntiAlias(false)
            SnapsToDevicePixels = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        canvas.Children.Add(background);
        canvas.Children.Add(glyph);

        return new WinCaptionButton(kind, isCloseButton, canvas, glyph, background);
    }
}
