using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LightUi.VsCodeFrameTest;

/// <summary>测试窗口。窗口行为全在基类 <see cref="NativeFrameWindow"/> 里，这里只刷诊断面板。</summary>
public partial class MainWindow : NativeFrameWindow
{
    private const long WS_CAPTION = 0x00C00000;
    private const long WS_THICKFRAME = 0x00040000;

    /// <summary>给验收脚本读的机器可读诊断（按钮是自绘的，UIAutomation 看不见）。</summary>
    private static readonly string DiagnosticsDumpPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lightui-vscode-frametest.txt");

    private readonly DispatcherTimer _refresh;

    public MainWindow()
    {
        InitializeComponent();

        // 定时刷新而不是每次 WM_NCHITTEST 刷：鼠标划过时命中测试每秒上百次，每来一次都重排文本纯属浪费。
        _refresh = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _refresh.Tick += (_, _) => RefreshDiagnostics();

        Loaded += (_, _) =>
        {
            RefreshDiagnostics();
            _refresh.Start();
        };

        Closed += (_, _) => _refresh.Stop();
    }

    private void RefreshDiagnostics()
    {
        WriteDiagnosticsDump(DiagnosticsDumpPath);

        NativeGeometry g = ReadGeometry();
        if (!g.Available)
        {
            DiagnosticsText.Text = "还没有 HWND（窗口尚未创建）。";
            return;
        }

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        int left = g.VisibleFrameRect.Left - g.WindowRect.Left;
        int top = g.VisibleFrameRect.Top - g.WindowRect.Top;
        int right = g.WindowRect.Right - g.VisibleFrameRect.Right;
        int bottom = g.WindowRect.Bottom - g.VisibleFrameRect.Bottom;

        bool hasInvisibleBorder = left > 0 || right > 0 || bottom > 0;

        DiagnosticsText.Text = string.Join(
            Environment.NewLine,
            $"结论：{(hasInvisibleBorder ? "有不可见边框 ✓（和 VS Code / Visual Studio 一致）" : "没有不可见边框 ✗")}",
            string.Empty,
            $"窗口矩形      {g.WindowRect.Left},{g.WindowRect.Top} - {g.WindowRect.Right},{g.WindowRect.Bottom}   {g.WindowRect.Width} x {g.WindowRect.Height}",
            $"可见边框      {g.VisibleFrameRect.Left},{g.VisibleFrameRect.Top} - {g.VisibleFrameRect.Right},{g.VisibleFrameRect.Bottom}",
            $"不可见边框    左 {left}px / 上 {top}px / 右 {right}px / 下 {bottom}px   （DIP：{left / scale:F1} / {top / scale:F1} / {right / scale:F1} / {bottom / scale:F1}）",
            $"客户区        {g.ClientWidth} x {g.ClientHeight}",
            $"frame 厚度    {g.FrameThickness}px = SM_CXSIZEFRAME + SM_CXPADDEDBORDER（ui::GetFrameThicknessFromWindow 的等价物）",
            $"窗口样式      0x{g.StyleBits:X8}   WS_CAPTION={Has(g.StyleBits, WS_CAPTION)}  WS_THICKFRAME={Has(g.StyleBits, WS_THICKFRAME)}",
            $"最大化        {g.IsZoomed}    当前悬停按钮 {HoveredCaptionButton}",
            string.Empty,
            $"DWM frame     {DwmFrameReport}",
            $"首帧隐身      {CloakReport}",
            $"命中追踪      {HitTestTrace}",
            $"按钮布局      {CaptionButtonReport()}",
            $"诊断旁路      {DiagnosticsDumpStatus}",
            string.Empty,
            "期望：不可见边框 左/右/下 ≈ 9px（7.2 DIP，本机 125%），上 = 0（WinFrameView::RestoredFrameBorderInsets 顶 0）。");
    }

    private static bool Has(long bits, long flag) => (bits & flag) == flag;
}
