namespace LightUi;

/// <summary>
/// 主题切换运行时：把主题 xaml 里的 brush 写进资源链<b>顶层</b>字典（<see cref="Application.Resources"/>）自己的键上。
///
/// 用法：
/// <code>
/// ThemeManager.ApplyTheme(ApplicationTheme.Dark);
/// ThemeManager.ApplyAccentColor(Colors.Orange);   // 可选：自定义强调色
/// ThemeManager.CurrentThemeChanged += (_, t) =&gt; { };
/// </code>
/// 必须写顶层键：Light / Dark 挂在三层字典深处（Application.Resources → ControlsResources.xaml →
/// Theme/ThemeResources.xaml），而 WPF 只在资源链顶层变更时才让全树 DynamicResource 失效重求值 ——
/// 改嵌套层会值变了而界面不重绘。不监听 UISettings、不探测 HighContrast。
/// </summary>
public static class ThemeManager
{
    private static readonly Dictionary<ApplicationTheme, Uri> _themeSources = new()
    {
        [ApplicationTheme.Light] = new Uri("/LightUi;component/Theme/Light.xaml", UriKind.Relative),
        [ApplicationTheme.Dark] = new Uri("/LightUi;component/Theme/Dark.xaml", UriKind.Relative),
        [ApplicationTheme.HighContrast] = new Uri("/LightUi;component/Theme/Light.xaml", UriKind.Relative), // TODO
    };

    /// <summary>用户自定义的强调色；为 null 表示用主题自带的那套。</summary>
    private static Color? _customAccent;

    /// <summary>YIQ 分界点：亮于此值配黑字，暗于此值配白字。调高会让更多中亮色落入白字分支。</summary>
    private const double BrightnessCrossover = 180;

    /// <summary>当前实际生效的主题。</summary>
    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Light;

    /// <summary>主题切换完成时触发。</summary>
    public static event EventHandler<ApplicationTheme>? CurrentThemeChanged;

    /// <summary>当前生效的强调色：直接读资源里的 AccentBrush，不另存状态（避免和资源不一致）。</summary>
    public static Color? CurrentAccentColor =>
        ( Application.Current?.TryFindResource("AccentBrush") as SolidColorBrush )?.Color;

    /// <summary>切到指定主题。</summary>
    public static void ApplyTheme(ApplicationTheme theme)
    {
        if (!_themeSources.TryGetValue(theme, out var source))
            return;

        var target = Application.Current?.Resources;
        if (target is null)
            return;

        var palette = new ResourceDictionary { Source = source };

        // 逐个键写进 target 自己的键表 —— 这才是会触发全树 DynamicResource
        // 失效重求值的变更位置。
        foreach (var key in palette.Keys)
        {
            target[key] = palette[key];
        }

        // 自定义强调色要盖过主题自带的，否则一切主题就丢了
        if (_customAccent is { } custom)
        {
            WriteAccent(target, custom);
        }

        CurrentTheme = theme;
        CurrentThemeChanged?.Invoke(null, theme);
    }

    /// <summary>
    /// 用自定义颜色当强调色：按钮 / 勾选框 / 开关 / 链接等跟着一起变。
    /// 其上文字色与 hover / pressed 由该色推导：亮的配黑字、暗的配白字，
    /// 底色朝远离文字色的方向走，两个分支的对比度都单调升高。
    /// 设置后 <see cref="ApplyTheme"/> 会继续保留它。
    /// </summary>
    public static void ApplyAccentColor(Color accent)
    {
        var target = Application.Current?.Resources;
        if (target is null)
            return;

        // 要的就是当前主题自带的色 → 等价于"重置"：主题那套 hover / pressed / 前景色是手写值，
        // 公式推导不出一样的结果，不做这个判断的话"再应用一次"会变成可见改动。
        if (TryGetThemeAccent(CurrentTheme, out var themeAccent) && themeAccent == accent)
        {
            ResetAccentColor();
            return;
        }

        _customAccent = accent;
        WriteAccent(target, accent);
    }

    /// <summary>取某个主题 xaml 里自己声明的强调色。</summary>
    private static bool TryGetThemeAccent(ApplicationTheme theme, out Color accent)
    {
        accent = default;

        if (!_themeSources.TryGetValue(theme, out var source))
        {
            return false;
        }

        var palette = new ResourceDictionary { Source = source };
        if (palette["AccentBrush"] is not SolidColorBrush brush)
        {
            return false;
        }

        accent = brush.Color;
        return true;
    }

    /// <summary>丢弃自定义强调色，回到当前主题自带的那套。</summary>
    public static void ResetAccentColor()
    {
        _customAccent = null;

        var target = Application.Current?.Resources;
        if (target is null || !_themeSources.TryGetValue(CurrentTheme, out var source))
            return;

        var palette = new ResourceDictionary { Source = source };
        foreach (var key in palette.Keys)
        {
            target[key] = palette[key];
        }
    }

    /// <summary>把强调色这一族写进 target 自己的键表（理由见类注释）。</summary>
    private static void WriteAccent(ResourceDictionary target, Color accent)
    {
        var onAccent = PickOnAccent(accent);

        target["AccentBrush"] = new SolidColorBrush(accent);
        target["AccentForegroundBrush"] = new SolidColorBrush(onAccent);

        // 底色朝"远离文字色"的方向走：白字压暗、黑字提亮，两个分支对比度都单调升高。
        // 固定往黑混只对白字成立：亮色配黑字会越悬停越糊（#FBBF24 是 12.58 → 9.70 → 7.08）。
        var toward = onAccent == Colors.White ? Colors.Black : Colors.White;
        target["AccentHoverBrush"] = new SolidColorBrush(Blend(accent, toward, 0.12));
        target["AccentPressedBrush"] = new SolidColorBrush(Blend(accent, toward, 0.25));
    }

    /// <summary>
    /// 强调色上该配白字还是黑字。
    ///
    /// 用 YIQ 而非 WCAG：WCAG 会低估高饱和色（尤其纯红）的"暗" —— 纯红按 WCAG 算
    /// 黑字 5.25:1 优于白字 4.00:1，但观感是黑字糊在红底上。YIQ 判纯红为暗色，配白字才对。
    ///
    /// 结果必须和两套主题手写的 AccentForegroundBrush 一致，否则"再应用一次自带色"会翻字色
    /// —— ApplyAccentColor 里那条"相等即 Reset"的短路靠的就是这个一致性。
    /// </summary>
    private static Color PickOnAccent(Color accent)
    {
        var brightness = ( ( 299 * accent.R ) + ( 587 * accent.G ) + ( 114 * accent.B ) ) / 1000.0;
        return brightness >= BrightnessCrossover ? Colors.Black : Colors.White;
    }

    /// <summary>按比例从 a 向 b 混合。</summary>
    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        255,
        (byte)Math.Round(( a.R * ( 1 - t ) ) + ( b.R * t )),
        (byte)Math.Round(( a.G * ( 1 - t ) ) + ( b.G * t )),
        (byte)Math.Round(( a.B * ( 1 - t ) ) + ( b.B * t )));

    // TODO: 监听 SystemParameters.HighContrast，自动切到 HighContrast 主题
}
