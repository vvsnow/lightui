using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LightUi.VsCodeFrameTest.Native;

/// <summary>
/// 装着四个标题栏按钮（最小化 / 最大化 / 还原 / 关闭）的容器 —— 和 Electron
/// <c>shell/browser/ui/views/win_caption_button_container.cc</c> 一一对应。
///
/// <list type="bullet">
/// <item>构造时 <c>AddChildView(CreateCaptionButton(...))</c> 建四个按钮 —— 这里也是构造时建。</item>
/// <item>布局：FlexLayout 水平、主轴 <c>kEnd</c>（靠右）、交叉轴 <c>kStart</c>（靠上），
/// "This allows the container to expand to become a faux titlebar/drag handle."</item>
/// <item><c>NonClientHitTest()</c>：minimize → HTMINBUTTON，maximize → HTMAXBUTTON，
/// restore → HTMAXBUTTON，close → HTCLOSE，都不中 → HTCAPTION</item>
/// <item><c>UpdateButtons()</c>：maximize 与 restore 互斥显示（<c>restore = is_maximized &amp;&amp; maximizable</c>）</item>
/// <item>按钮矩形一律按容器自己算出来的 <see cref="WinCaptionButton.Bounds"/> 判，
/// 不看 WPF 元素实际在哪（对应 <c>GetMirroredBounds()</c>）。</item>
/// </list>
/// </summary>
internal sealed class WinCaptionButtonContainer
{
    private readonly Canvas _host;
    private readonly WinCaptionButton _minimize;
    private readonly WinCaptionButton _maximize;
    private readonly WinCaptionButton _restore;
    private readonly WinCaptionButton _close;

    public WinCaptionButtonContainer(Canvas host)
    {
        _host = host;

        // 对应 CreateCaptionButton(...) 那一串 AddChildView
        _minimize = WinCaptionButton.Create(CaptionButton.Minimize, isCloseButton: false, host, "GlyphMinimize");
        _maximize = WinCaptionButton.Create(CaptionButton.Maximize, isCloseButton: false, host, "GlyphMaximize");
        _restore = WinCaptionButton.Create(CaptionButton.Restore, isCloseButton: false, host, "GlyphRestore");
        _close = WinCaptionButton.Create(CaptionButton.Close, isCloseButton: true, host, "GlyphClose");
    }

    public IReadOnlyList<WinCaptionButton> Buttons => new[] { _minimize, _maximize, _restore, _close };

    /// <summary>容器矩形（父坐标系，DIP）。</summary>
    public Rect Bounds { get; private set; }

    /// <summary>
    /// 容器宽度 = 各**可见**按钮 preferred 之和（隐藏的视图不占位置，和 FlexLayout 一致 ——
    /// maximize / restore 是互斥的一对，只有一颗参与排版）。
    /// </summary>
    public Size CalculatePreferredSize()
    {
        double width = 0;
        double height = 0;
        foreach (WinCaptionButton button in Buttons)
        {
            if (!button.IsShown)
            {
                continue;
            }

            Size size = button.CalculatePreferredSize();
            width += size.Width;
            height = Math.Max(height, size.Height);
        }

        return new Size(width, height);
    }

    /// <summary>对应 <c>WinCaptionButtonContainer::SetButtonSize()</c>。</summary>
    public void SetButtonSize(Size size)
    {
        foreach (WinCaptionButton button in Buttons)
        {
            button.SetSize(size);
        }
    }

    /// <summary>
    /// 对应 <c>WinCaptionButtonContainer::UpdateButtons()</c>：
    /// 最大化与还原互斥；窗口不可最小化/最大化/关闭时对应按钮隐藏或禁用。
    /// </summary>
    public void UpdateButtons(bool minimizable, bool maximizable, bool isMaximized, bool closable)
    {
        _minimize.IsShown = minimizable;
        _minimize.IsEnabledState = minimizable;

        _maximize.IsShown = !isMaximized && maximizable;
        _maximize.IsEnabledState = maximizable;

        _restore.IsShown = isMaximized && maximizable;
        _restore.IsEnabledState = maximizable;

        _close.IsShown = true;
        _close.IsEnabledState = closable;
    }

    /// <summary>
    /// 摆好容器与四个按钮。等价于 FlexLayout 的一次 Layout：
    /// 主轴 <c>kEnd</c> → 整排**按声明顺序**靠右收拢（所以 minimize 在最左、close 在最右）；
    /// 交叉轴 <c>kStart</c> → 顶部对齐。
    /// </summary>
    public void Layout(Rect bounds)
    {
        Bounds = bounds;

        double totalWidth = CalculatePreferredSize().Width;
        double x = bounds.Right - totalWidth;

        foreach (WinCaptionButton button in Buttons)
        {
            if (!button.IsShown)
            {
                continue;
            }

            Size size = button.CalculatePreferredSize();
            button.Bounds = new Rect(x, bounds.Top, size.Width, size.Height);
            Place(button, button.Bounds);
            x += size.Width;
        }
    }

    /// <summary>对应 <c>WinCaptionButtonContainer::HitTestPoint()</c>。</summary>
    public bool HitTestPoint(Point point) => Bounds.Contains(point);

    /// <summary>对应 <c>WinCaptionButtonContainer::NonClientHitTest()</c>，分支顺序与源码一致。</summary>
    public int NonClientHitTest(Point point)
    {
        if (_minimize.HitTest(point))
        {
            return NativeMethods.HTMINBUTTON;
        }

        if (_maximize.HitTest(point))
        {
            return NativeMethods.HTMAXBUTTON;
        }

        if (_restore.HitTest(point))
        {
            return NativeMethods.HTMAXBUTTON;
        }

        if (_close.HitTest(point))
        {
            return NativeMethods.HTCLOSE;
        }

        return NativeMethods.HTCAPTION;
    }

    /// <summary>点落在哪个按钮上（驱动悬停视觉用；和命中判定读同一份 Bounds）。</summary>
    public CaptionButton HitTestButton(Point point)
    {
        foreach (WinCaptionButton button in Buttons)
        {
            if (button.HitTest(point))
            {
                return button.Kind;
            }
        }

        return CaptionButton.None;
    }

    /// <summary>
    /// 按当前悬停/按下状态刷按钮外观。配色照 Electron 的
    /// <c>WinCaptionButton::OnPaintBackground()</c> / <c>PaintSymbol()</c>：
    /// 普通按钮 hovered alpha <c>0x1A</c>、pressed <c>0x33</c>；关闭键底色 <c>#E81123</c>、
    /// pressed alpha <c>0x98</c>，悬停/按下时符号转白。
    /// </summary>
    public void ApplyVisualState(CaptionButton hovered, CaptionButton pressed)
    {
        foreach (WinCaptionButton button in Buttons)
        {
            bool isHovered = hovered == button.Kind;
            bool isPressed = pressed == button.Kind;

            Color baseColor = button.IsCloseButton
                ? Color.FromRgb(0xE8, 0x11, 0x23)      // base_color = SkColorSetRGB(0xE8, 0x11, 0x23)
                : Color.FromRgb(0x1F, 0x1F, 0x1F);     // GetBaseForegroundColor()

            byte alpha;
            if (isPressed)
            {
                alpha = button.IsCloseButton ? (byte)0x98 : (byte)0x33;
            }
            else if (isHovered)
            {
                alpha = button.IsCloseButton ? (byte)0xFF : (byte)0x1A;
            }
            else
            {
                alpha = 0x00;
            }

            button.Background.Background = alpha == 0
                ? Brushes.Transparent
                : new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));

            // 关闭键在悬停/按下时符号转白（PaintSymbol 里那段）
            bool whiteSymbol = button.IsCloseButton && (isHovered || isPressed);
            button.Glyph.Stroke = whiteSymbol ? Brushes.White : new SolidColorBrush(baseColor);

            button.Glyph.Opacity = button.IsEnabledState ? 1.0 : 0.5;

            // 可见性按按钮自己生效（四个按钮共用宿主 Canvas，不能动宿主的可见性）。
            // 对应 views 里 SetVisible(false) 的视图不参与绘制也不占布局。
            Visibility visibility = button.IsShown ? Visibility.Visible : Visibility.Hidden;
            button.Background.Visibility = visibility;
            button.Glyph.Visibility = visibility;
        }
    }

    private static void Place(WinCaptionButton button, Rect rect)
    {
        Canvas.SetLeft(button.Background, rect.Left);
        Canvas.SetTop(button.Background, rect.Top);
        button.Background.Width = rect.Width;
        button.Background.Height = rect.Height;

        // 符号：对应 symbol_size_pixels = ClampRound(10 * scale)，居中贴在按钮里。
        // WPF 坐标本来就是 DIP，所以直接用 10（ClampRound(10 * 1)）。
        const double symbolSize = WindowFrameUtil.KSymbolSizeAt1x;
        Canvas.SetLeft(button.Glyph, rect.Left + ((rect.Width - symbolSize) / 2));
        Canvas.SetTop(button.Glyph, rect.Top + ((rect.Height - symbolSize) / 2));
        button.Glyph.Width = symbolSize;
        button.Glyph.Height = symbolSize;
    }
}
