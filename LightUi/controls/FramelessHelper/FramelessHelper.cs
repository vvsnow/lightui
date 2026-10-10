using LightUi.Controls.Frameless;

using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Shell;

namespace LightUi.Controls;

/// <summary>
/// 无边框窗口的「逻辑」那一半：**不用 WPF 的 WindowChrome**，改由自己处理非客户区
/// （照 Electron / Chromium 的做法：自己答 <c>WM_NCCALCSIZE</c>/<c>WM_NCHITTEST</c>、自己进 DWM frame 通道）。
/// 「样式」那一半是 <c>FramelessWindowStyle</c> + 两个按钮样式。
///
/// 用法（两步，缺一不可）：
/// <code>&lt;Window ui:FramelessHelper.IsFrameless="True"
///         Style="{DynamicResource FramelessWindowStyle}" /&gt;</code>
///
/// 与旧的 <see cref="WindowHelper"/> 的关系：**并存但互斥** —— 同一窗口只能挂一套。
/// 两个都开会打架（WindowChrome 自己也要管 <c>WM_NCCALCSIZE</c>/<c>WM_NCHITTEST</c>），
/// 所以本类挂载时若发现窗口已经挂了 WindowChrome，会打一条 Debug 警告并**让位不挂**。
///
/// ★ 宿主 exe 的 DPI 感知（PerMonitorV2）**不由本库负责** —— 那是 exe 的 app.manifest 的事。
/// 进程是 SYSTEM_AWARE 时显示缩放一变就会整窗被位图拉伸（字发糊），库里只能探测并警告，
/// 见 <see cref="WarnIfDpiAwarenessMismatch"/>。
/// </summary>
public static class FramelessHelper
{
    #region IsFrameless

    /// <summary>挂上这套无边框逻辑；默认 <c>false</c>。</summary>
    public static readonly DependencyProperty IsFramelessProperty =
        DependencyProperty.RegisterAttached(
            "IsFrameless",
            typeof(bool),
            typeof(FramelessHelper),
            new PropertyMetadata(false, OnIsFramelessChanged));

    public static bool GetIsFrameless(Window window) => (bool)window.GetValue(IsFramelessProperty);

    public static void SetIsFrameless(Window window, bool value) => window.SetValue(IsFramelessProperty, value);

    #endregion

    #region CaptionHeight

    /// <summary>caption 条带的**默认**高度（DIP），见 <see cref="CaptionHeightProperty"/>。</summary>
    public const double DefaultCaptionHeight = 30;

    /// <summary>
    /// caption（标题栏）条带高度，单位 DIP。默认 <see cref="DefaultCaptionHeight"/>（30）。
    ///
    /// 设成 <c>0</c>（或负数）才回到「按系统度量算」：
    /// <c>SM_CYCAPTION + 顶部边框厚度 − WindowTopY</c>（本机 125% 下 29 DIP）。
    ///
    /// ★ 这个值**只决定「多高算标题栏 / 拖动区」**，与系统按钮的高度**无关** ——
    /// 按钮尺寸归样式（<c>FramelessSystemButtonStyle</c>）。这就是这一版特意做掉的解耦。
    /// </summary>
    public static readonly DependencyProperty CaptionHeightProperty =
        DependencyProperty.RegisterAttached(
            "CaptionHeight",
            typeof(double),
            typeof(FramelessHelper),
            new PropertyMetadata(DefaultCaptionHeight, OnCaptionHeightChanged));

    public static double GetCaptionHeight(Window window) => (double)window.GetValue(CaptionHeightProperty);

    public static void SetCaptionHeight(Window window, double value) => window.SetValue(CaptionHeightProperty, value);

    #endregion

    #region CloakUntilFirstFrame

    /// <summary>
    /// 启动不闪：窗口先被 DWM 隐身（照常渲染、用户看不见），等第一帧画完（<c>ContentRendered</c>）
    /// 才现身 —— 对应 Electron 的 <c>show:false</c> + <c>ready-to-show</c>。默认 <c>true</c>。
    ///
    /// 关掉它就退回 WPF 原生行为：<c>Show()</c> 立刻可见，可能闪一下「无边框但内容是空的」。
    /// </summary>
    public static readonly DependencyProperty CloakUntilFirstFrameProperty =
        DependencyProperty.RegisterAttached(
            "CloakUntilFirstFrame",
            typeof(bool),
            typeof(FramelessHelper),
            new PropertyMetadata(true));

    public static bool GetCloakUntilFirstFrame(Window window) =>
        (bool)window.GetValue(CloakUntilFirstFrameProperty);

    public static void SetCloakUntilFirstFrame(Window window, bool value) =>
        window.SetValue(CloakUntilFirstFrameProperty, value);

    #endregion

    #region 只读状态：悬停 / 按下（样式触发器消费）

    private static readonly DependencyPropertyKey HoveredCaptionButtonKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "HoveredCaptionButton", typeof(CaptionButton), typeof(FramelessHelper), new PropertyMetadata(CaptionButton.None));

    public static readonly DependencyProperty HoveredCaptionButtonProperty = HoveredCaptionButtonKey.DependencyProperty;

    private static readonly DependencyPropertyKey PressedCaptionButtonKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "PressedCaptionButton", typeof(CaptionButton), typeof(FramelessHelper), new PropertyMetadata(CaptionButton.None));

    public static readonly DependencyProperty PressedCaptionButtonProperty = PressedCaptionButtonKey.DependencyProperty;

    /// <summary>鼠标当前停在哪颗标题栏按钮上（<see cref="CaptionButton.None"/> = 没有）。</summary>
    public static CaptionButton GetHoveredCaptionButton(DependencyObject element) =>
        (CaptionButton)element.GetValue(HoveredCaptionButtonProperty);

    /// <summary>鼠标当前按在哪颗标题栏按钮上。</summary>
    public static CaptionButton GetPressedCaptionButton(DependencyObject element) =>
        (CaptionButton)element.GetValue(PressedCaptionButtonProperty);

    /// <summary>写悬停状态。**只有窗口过程该调** —— 返回 HT* 之后那块对 WPF 是非客户区，WPF 自己判不出来。</summary>
    internal static void SetHoveredCaptionButtonValue(Window window, CaptionButton button) =>
        window.SetValue(HoveredCaptionButtonKey, button);

    /// <summary>写按下状态。同上。</summary>
    internal static void SetPressedCaptionButtonValue(Window window, CaptionButton button) =>
        window.SetValue(PressedCaptionButtonKey, button);

    #endregion

    #region 每窗口状态

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State", typeof(State), typeof(FramelessHelper), new PropertyMetadata(null));

    private sealed class State
    {
        public FramelessWindowAdapter? Adapter;
        public HwndMessageHandler? Handler;
        public HwndSource? Source;
        public bool Cloaked;
        public DispatcherTimer? SafetyTimer;
        public readonly List<CommandBinding> CommandBindings = new();
    }

    #endregion

    #region 挂载 / 卸载

    private static void OnIsFramelessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
        {
            // 附加属性只对 Window 有意义；不抛异常（抛出会毁掉整份 XAML 加载），只提示
            Debug.WriteLine("[LightUi] FramelessHelper.IsFrameless 只能用在 Window 上。");
            return;
        }

        if (e.NewValue is true)
        {
            window.SourceInitialized += OnSourceInitialized;
            window.Closed += OnClosed;
            window.SizeChanged += OnWindowMetricsChanged;
            window.StateChanged += OnWindowMetricsChanged;

            if (window.IsInitialized)
            {
                // 属性是在窗口已经初始化之后才设的（少见但合法）
                Attach(window);
            }
        }
        else
        {
            Detach(window);
        }
    }

    private static void OnSourceInitialized(object? sender, EventArgs e) => Attach((Window)sender!);

    private static void OnClosed(object? sender, EventArgs e) => Detach((Window)sender!);

    private static void OnCaptionHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        RelayoutIfAttached(d as Window);

    private static void OnWindowMetricsChanged(object? sender, EventArgs e) =>
        RelayoutIfAttached(sender as Window);

    /// <summary>尺寸 / 状态 / caption 高度变化后都重算一遍（对应移植前窗口上那几个订阅）。</summary>
    private static void RelayoutIfAttached(Window? window) =>
        ( window?.GetValue(StateProperty) as State )?.Handler?.Relayout();

    private static void Attach(Window window)
    {
        if (window.GetValue(StateProperty) is State)
        {
            return;
        }

        // 互斥：窗口上已经有 WindowChrome（旧 WindowHelper 挂的）就别插手 ——
        // 两套都要管 WM_NCCALCSIZE / WM_NCHITTEST，同时生效结果不可预期。
        if (WindowChrome.GetWindowChrome(window) is not null)
        {
            Debug.WriteLine(
                "[LightUi] FramelessHelper 放弃挂载：这个窗口已经挂了 WindowChrome（WindowHelper）。" +
                "无边框逻辑同一窗口只能选一套。");
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        HwndSource? source = hwnd == IntPtr.Zero ? null : HwndSource.FromHwnd(hwnd);
        if (hwnd == IntPtr.Zero || source is null)
        {
            return;
        }

        var state = new State();
        var adapter = new FramelessWindowAdapter(window);
        var handler = new HwndMessageHandler(adapter);

        state.Adapter = adapter;
        state.Handler = handler;
        state.Source = source;

        window.SetValue(StateProperty, state);

        AddCommandBindings(window, state);

        // ① 挂钩子 + 接 DWM frame 通道 + 逼一次非客户区重算 + 同步 caption 高度
        handler.Attach(hwnd, source);

        // ② 启动不闪（可关）：先隐身，等第一帧画完再露面
        if (GetCloakUntilFirstFrame(window))
        {
            state.Cloaked = true;
            handler.SetCloaked(true);
            window.ContentRendered += OnContentRendered;

            // 兜底：万一 ContentRendered 不来，3 秒后无条件解除 —— 宁可闪一下，
            // 也不能让用户面对一个永远隐身的窗口
            state.SafetyTimer = new DispatcherTimer(DispatcherPriority.Send, window.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(3),
            };
            state.SafetyTimer.Tick += (_, _) => Reveal(window);
            state.SafetyTimer.Start();
        }

        WarnIfDpiAwarenessMismatch(window, hwnd);
    }

    private static void Detach(Window window)
    {
        if (window.GetValue(StateProperty) is not State state)
        {
            window.SourceInitialized -= OnSourceInitialized;
            window.Closed -= OnClosed;
            window.SizeChanged -= OnWindowMetricsChanged;
            window.StateChanged -= OnWindowMetricsChanged;
            return;
        }

        window.SetValue(StateProperty, null);

        window.SourceInitialized -= OnSourceInitialized;
        window.Closed -= OnClosed;
        window.SizeChanged -= OnWindowMetricsChanged;
        window.StateChanged -= OnWindowMetricsChanged;
        window.ContentRendered -= OnContentRendered;

        state.SafetyTimer?.Stop();

        foreach (CommandBinding binding in state.CommandBindings)
        {
            window.CommandBindings.Remove(binding);
        }

        state.Handler?.Detach();
    }

    /// <summary>第一帧画完了 —— 解除隐身（对应 Electron 的 ready-to-show）。</summary>
    private static void OnContentRendered(object? sender, EventArgs e) => Reveal((Window)sender!);

    private static void Reveal(Window window)
    {
        if (window.GetValue(StateProperty) is not State state || !state.Cloaked)
        {
            return;
        }

        state.Cloaked = false;
        state.SafetyTimer?.Stop();
        window.ContentRendered -= OnContentRendered;
        state.Handler?.SetCloaked(false);
    }

    /// <summary>
    /// 模式里的按钮用 <c>SystemCommands.*WindowCommand</c>。窗口自己不带这些 CommandBinding，
    /// 平时按钮走 HT* 码、动作由我们自己执行，用不到它们；但**布局完成前**（按钮实测尺寸还是 0）
    /// 命中会回落到 WPF 输入，那时就得靠这条命令兜底，所以照样注册上。
    /// </summary>
    private static void AddCommandBindings(Window window, State state)
    {
        Add(SystemCommands.MinimizeWindowCommand, () => SystemCommands.MinimizeWindow(window));
        Add(SystemCommands.MaximizeWindowCommand, () => SystemCommands.MaximizeWindow(window));
        Add(SystemCommands.RestoreWindowCommand, () => SystemCommands.RestoreWindow(window));
        Add(SystemCommands.CloseWindowCommand, () => SystemCommands.CloseWindow(window));

        void Add(RoutedCommand command, Action action)
        {
            var binding = new CommandBinding(command, (_, _) => action());
            state.CommandBindings.Add(binding);
            window.CommandBindings.Add(binding);
        }
    }

    /// <summary>
    /// 库里**改不了**进程的 DPI 感知（那是宿主 exe 的 app.manifest 的事），但可以探一下：
    /// 进程不是 Per-Monitor 感知时，显示缩放一变就会整窗被系统位图拉伸（字发糊）。
    /// 这时只打一条 Debug 警告 —— 绝不抛异常（附加属性回调里抛会毁掉整份 XAML 加载）。
    /// </summary>
    private static void WarnIfDpiAwarenessMismatch(Window window, IntPtr hwnd)
    {
        uint windowDpi = NativeMethods.GetDpiForWindow(hwnd);
        if (windowDpi == 0)
        {
            return;
        }

        double visualScale = System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
        double windowScale = windowDpi / 96.0;

        if (Math.Abs(visualScale - windowScale) > 0.01)
        {
            Debug.WriteLine(
                $"[LightUi] DPI 感知不一致：窗口 DPI={windowDpi}（{windowScale:0.##}x），" +
                $"而 WPF 渲染尺度={visualScale:0.##}x。多半是宿主 exe 没有声明 PerMonitorV2 —— " +
                "请在 exe 工程加 app.manifest（见 LightUi.VsCodeFrameTest\\app.manifest），" +
                "否则显示缩放变化后整窗会被位图拉伸、字体发糊。");
        }
    }

    #endregion
}
