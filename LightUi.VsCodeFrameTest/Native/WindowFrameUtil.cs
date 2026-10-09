namespace LightUi.VsCodeFrameTest.Native;

/// <summary>
/// 标题栏按钮的度量常量。
///
/// 对应 Chromium / Electron 的 <c>chrome/browser/ui/frame/window_frame_util.h</c>（旧名
/// <c>kWindows10GlassCaptionButton*</c>）。那边就是一组写死的 1x 缩放值，
/// 源码里还留着 <c>TODO(bsep)</c> 承认它在高 DPI 下跟系统按钮尺寸对不上 —— 这里照抄，不改。
/// </summary>
internal static class WindowFrameUtil
{
    /// <summary>
    /// 单个标题栏按钮的基准宽度（DIP）。参考里是 <b>45</b>，这里改成 <b>60</b> 试。
    ///
    /// 背景数字（本机 125% 实测）：系统那块标题栏按钮区 = 148.0 DIP 宽（距窗口右缘 6.4..154.4），
    /// 三等分每格 49.3 DIP。所以：
    /// · 总宽 = 3 × 本值 + 2 × 间距。60 → <b>182 DIP</b>，比系统的 148 宽 34 DIP；
    ///   （45 → 137 窄 11；48 → 146；49 → 149 —— `3b + 2` 永远凑不出 148）
    /// · 容器整体贴在客户区右缘，所以**加宽是往左长**：右缘位置不变（距窗口右缘 54.4 DIP），左缘一路左移。
    ///   60 宽时最大化按钮会横跨到系统"最小化格"里 → 那部分预期不弹 Snap（正好可以拿来做对照实验）。
    /// </summary>
    public const int KWindowsCaptionButtonWidth = 100;

    /// <summary>非最大化时标题栏按钮的高度（DIP）。（对应 kWindowsCaptionButtonHeightRestored）</summary>
    public const int KWindowsCaptionButtonHeightRestored = 29;

    /// <summary>相邻两个按钮之间的间距（DIP）。第一个按钮间距为 0。</summary>
    public const int KWindowsCaptionButtonVisualSpacing = 1;

    /// <summary>按钮上符号的基准边长，实际按 <c>ClampRound(10 * scale)</c> 缩放（同 Electron）。</summary>
    public const double KSymbolSizeAt1x = 10;
}
