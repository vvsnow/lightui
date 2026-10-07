using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace LightUi;

/// <summary>
/// 主题切换运行时：
///   - 切主题：把目标主题 xaml 里的 brush 键值写进资源链<b>顶层</b>字典（默认
///     <see cref="Application.Resources"/>）自己的键上。
///   - 不做 UISettings 监听、不做 HighContrast 自动探测——保留 TODO 钩子即可。
///
/// 为什么是“写顶层键”而不是“替换嵌套字典”：
///   Light/Dark 挂在 Application.Resources → ControlsResources.xaml → Theme/ThemeResources.xaml
///   三层深处。WPF 只在资源链顶层发生变更时才对全树做 DynamicResource 失效重求值；
///   改动嵌套层字典（不管是换它的 MergedDictionaries 还是改它自己的键）都不会通知元素，
///   结果是资源值确实变了、但界面一个像素都不重绘。早期实现正是在 ThemeRoot 上
///   清空 MergedDictionaries 再加新 Source，因此切主题后界面完全不动。
/// </summary>
public static class ThemeManager
{
    private static readonly Dictionary<ApplicationTheme, Uri> _themeSources = new()
    {
        [ApplicationTheme.Light]        = new Uri("/LightUi;component/Theme/Light.xaml", UriKind.Relative),
        [ApplicationTheme.Dark]         = new Uri("/LightUi;component/Theme/Dark.xaml",  UriKind.Relative),
        [ApplicationTheme.HighContrast] = new Uri("/LightUi;component/Theme/Light.xaml", UriKind.Relative), // TODO
    };

    /// <summary>主题键值写入的目标字典；为 null 时用 <see cref="Application.Resources"/>。</summary>
    private static ResourceDictionary? _themeTarget;

    /// <summary>用户自定义的强调色；为 null 表示用主题自带的那套。</summary>
    private static Color? _customAccent;

    /// <summary>感知亮度（YIQ）的分界点：亮于此值配黑字，暗于此值配白字。
    /// 提高此值让更多"中等到亮"的 accent 进入"白字"分支（解决浅蓝 + 黑字看着不清晰的问题）。</summary>
    private const double BrightnessCrossover = 180;

    /// <summary>当前实际生效的主题。</summary>
    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Light;

    /// <summary>主题切换完成时触发。</summary>
    public static event EventHandler<ApplicationTheme>? CurrentThemeChanged;

    /// <summary>
    /// 当前实际生效的强调色 —— 直接读资源里的 AccentBrush，所以自定义过就是自定义值，
    /// 没自定义过就是主题自带值（不另存一份状态，避免和资源不一致）。
    /// </summary>
    public static Color? CurrentAccentColor =>
        (Application.Current?.TryFindResource("AccentBrush") as SolidColorBrush)?.Color;

    /// <summary>切到指定主题。</summary>
    public static void ApplyTheme(ApplicationTheme theme)
    {
        if (!_themeSources.TryGetValue(theme, out var source)) return;

        var target = _themeTarget ?? Application.Current?.Resources;
        if (target is null) return;

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
    /// hover / pressed 以及压在强调色上的文字色由该颜色自动推导 ——
    /// 亮的配黑字、暗的配白字，hover / pressed 再往文字色方向靠一点，
    /// 所以悬停 / 按下时对比度只会更高。
    /// 设置之后 <see cref="ApplyTheme"/> 会继续保留这个强调色。
    /// </summary>
    public static void ApplyAccentColor(Color accent)
    {
        var target = _themeTarget ?? Application.Current?.Resources;
        if (target is null) return;

        // 要的就是当前主题自带的强调色 → 等价于"重置"。
        // 主题那套 hover / pressed / 其上文字色是手写值，公式推导不出一样的结果；
        // 不做这个判断的话，"把默认色再应用一次"会变成一次可见的改动。
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

        var target = _themeTarget ?? Application.Current?.Resources;
        if (target is null || !_themeSources.TryGetValue(CurrentTheme, out var source)) return;

        var palette = new ResourceDictionary { Source = source };
        foreach (var key in palette.Keys)
        {
            target[key] = palette[key];
        }
    }

    /// <summary>
    /// 把强调色这一族写进 target 自己的键表（顶层键才会触发全树重绘，理由见类注释）。
    /// </summary>
    private static void WriteAccent(ResourceDictionary target, Color accent)
    {
        var onAccent = PickOnAccent(accent);

        target["AccentBrush"] = new SolidColorBrush(accent);
        target["AccentForegroundBrush"] = new SolidColorBrush(onAccent);

        // hover / pressed 一律变暗 —— 和两套主题里手写的方向一致
        // （之前是往"前景色"方向混，白字就会越悬停越亮，方向反了）
        target["AccentHoverBrush"] = new SolidColorBrush(Blend(accent, Colors.Black, 0.12));
        target["AccentPressedBrush"] = new SolidColorBrush(Blend(accent, Colors.Black, 0.25));
    }

    /// <summary>
    /// 强调色上该配白字还是黑字。
    ///
    /// 用感知亮度（YIQ）而不是 WCAG 对比度 —— WCAG 的相对亮度公式会严重低估
    /// 高饱和色（尤其纯红）的"暗"：纯红 #FF0000 按 WCAG 算，黑字 5.25:1、
    /// 白字 4.00:1，结论是"黑字更好"，但实际观感是黑字糊在红底上。
    /// 按 YIQ，纯红属于暗色，配白字才是对的。
    ///
    /// 这个判据和两套主题手写的值一致：Light / Dark 自带的 AccentBrush 都是 #2563EB，
    /// YIQ ≈ 96 < BrightnessCrossover，判为白字，与两个主题里手写的
    /// AccentForegroundBrush (#FFFFFF) 相同 —— 所以"把主题自带的强调色再应用一次"
    /// 不会出现可见变化（ApplyAccentColor 里那条相等就 Reset 的短路，靠的就是这个一致性）。
    /// </summary>
    private static Color PickOnAccent(Color accent)
    {
        var brightness = ((299 * accent.R) + (587 * accent.G) + (114 * accent.B)) / 1000.0;
        return brightness >= BrightnessCrossover ? Colors.Black : Colors.White;
    }

    /// <summary>按比例从 a 向 b 混合。</summary>
    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        255,
        (byte)Math.Round((a.R * (1 - t)) + (b.R * t)),
        (byte)Math.Round((a.G * (1 - t)) + (b.G * t)),
        (byte)Math.Round((a.B * (1 - t)) + (b.B * t)));

    /// <summary>
    /// 主动指定主题键值的写入目标（便于测试或在 App 启动时显式注册）。
    /// 传入的字典应处在资源链顶层（如 <see cref="Application.Resources"/>），
    /// 否则改它不会触发界面重绘。
    /// </summary>
    public static void RegisterThemeTarget(ResourceDictionary themeTarget)
    {
        _themeTarget = themeTarget ?? throw new ArgumentNullException(nameof(themeTarget));
    }

    // TODO: 监听 SystemParameters.HighContrast，自动切到 HighContrast 主题
}
