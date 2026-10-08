using System.Windows.Interop;
using System.Windows.Shell;

namespace LightUi.Controls;

/// <summary>
/// 一行开关，把「无系统边框的自绘标题栏窗口」需要的三件事一次做完：
///   1. 给窗口挂上 <see cref="WindowChrome"/>（标题栏 28px、整窗玻璃、6px 缩放边框、不让系统再画按钮）
///   2. 让 <c>LightUiWindowStyle</c> 模板里的三个系统按钮（最小化 / 最大化 / 关闭）显形并接上行为
///   3. 钩 WM_NCHITTEST，让最大化按钮获得 Windows 11 的 Snap Layouts（悬停弹出贴边布局）
///
/// 只能用在 <see cref="Window"/> 上，原因见 <see cref="OnIsFramelessChanged"/>：
/// <code>
/// &lt;Window Style="{DynamicResource LightUiWindowStyle}" ui:WindowHelper.IsFrameless="True"&gt;
/// </code>
/// </summary>
public static class WindowHelper
{
    // ---- Win32 消息 ----
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONUP = 0x00A2;
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int WM_NCMOUSELEAVE = 0x02A2;

    // WinUser.h 里没有公开导出这个常量（WPF 内部那个 Standard.HT 枚举是 internal，引用不到），
    // 只能写字面量。它就是 Windows 11 弹出 Snap Layouts 的唯一开关。
    private const int HTMAXBUTTON = 9;

    // Windows 11 首个内部版本号。Snap Layouts 从它开始有。
    private const int Windows11Build = 22000;

    /// <summary>
    /// 标题栏高度（DIP）。同时决定了窗口顶部哪一段算「可拖动标题栏」。
    /// 标题栏那三个系统按钮的高度也用它（<c>styles/Button.xaml</c> 的 SystemButtonStyle 用 {x:Static} 引这里）。
    /// </summary>
    public const double CaptionHeight = 28;

    /// <summary>四周缩放边框厚度（DIP）。</summary>
    public const double ResizeBorderThickness = 6;

    /// <summary>
    /// 窗口最大化时内容区与标题栏按钮要内缩的量（= <see cref="ResizeBorderThickness"/>）：
    /// WindowChrome 最大化后客户区比可见区域大出一圈边框宽，不内缩四边内容会被切掉。
    /// 必须是 Thickness —— XAML 那边 <c>Padding</c> 是 Thickness，{x:Static} 不做标量转换。
    /// </summary>
    public static readonly Thickness MaximizedPadding = new(ResizeBorderThickness);

    #region IsFrameless

    public static readonly DependencyProperty IsFramelessProperty =
        DependencyProperty.RegisterAttached(
            "IsFrameless",
            typeof(bool),
            typeof(WindowHelper),
            new PropertyMetadata(false, OnIsFramelessChanged));

    public static bool GetIsFrameless(Window window) => (bool)window.GetValue(IsFramelessProperty);

    public static void SetIsFrameless(Window window, bool value) => window.SetValue(IsFramelessProperty, value);

    private static void OnIsFramelessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 设计期直接跳过：VS 设计器 / 热重载宿主里同一个名字的类型未必是同一份类型标识，
        // d 可能不是我们认识的那个 Window。WPF 的 WindowChrome 回调开头也是这个判断。
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(d))
        {
            return;
        }

        // 「只能用在 Window 上」真正靠 Get/Set 的形参类型在编译期拦住，这里只兜底，
        // 且故意不抛异常：DP 回调里抛异常会打断整份 XAML 加载（窗口直接出不来，
        // 异常还可能被上层未 await 的 Task 吞掉，只剩白屏），而 XAML 端本来就校验不了
        // getter/setter 签名、写错了也走不到这里。代价不对等，静默忽略 + Debug 提示即可。
        if (d is not Window window)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LightUi] WindowHelper.IsFrameless 只能用在 Window 上，已忽略目标：" +
                $"{d.GetType().AssemblyQualifiedName}");
            return;
        }

        if (e.NewValue is true)
        {
            Attach(window);
        }
        else
        {
            Detach(window);
        }
    }

    #endregion

    #region 供模板触发器消费的只读状态

    // 这两个必须是 public 且字段名以 Property 结尾：XAML 的 <Trigger Property="..."> 是按
    // 「名称 + Property 后缀」去反射找静态字段的。写成 private 不会报错，触发器只是永远不触发。
    private static readonly DependencyPropertyKey IsMaximizeButtonHoveredPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsMaximizeButtonHovered", typeof(bool), typeof(WindowHelper), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey IsMaximizeButtonPressedPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsMaximizeButtonPressed", typeof(bool), typeof(WindowHelper), new PropertyMetadata(false));

    /// <summary>最大化/还原按钮是否处于悬停态。由 WM_NCHITTEST 驱动，模板触发器消费。</summary>
    public static readonly DependencyProperty IsMaximizeButtonHoveredProperty =
        IsMaximizeButtonHoveredPropertyKey.DependencyProperty;

    /// <summary>最大化/还原按钮是否处于按下态。</summary>
    public static readonly DependencyProperty IsMaximizeButtonPressedProperty =
        IsMaximizeButtonPressedPropertyKey.DependencyProperty;

    public static bool GetIsMaximizeButtonHovered(DependencyObject d) =>
        (bool)d.GetValue(IsMaximizeButtonHoveredProperty);

    public static bool GetIsMaximizeButtonPressed(DependencyObject d) =>
        (bool)d.GetValue(IsMaximizeButtonPressedProperty);

    #endregion

    #region 每实例状态

    // 私有附加属性当状态槽，套路同 FlyoutService 的 ClickHandlerProperty
    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(FramelessState), typeof(WindowHelper));

    private sealed class FramelessState
    {
        public FramelessState(Window window) => Window = window;

        public Window Window { get; }

        public HwndSource? Source { get; set; }

        /// <summary>必须缓存同一个委托实例：RemoveHook 是按引用匹配的。</summary>
        public HwndSourceHook? Hook { get; set; }

        public Button? MaximizeButton { get; set; }

        public bool MaximizeButtonResolveFailed { get; set; }

        /// <summary>窗口原本的 WindowChrome，关掉 IsFrameless 时还原回去。</summary>
        public WindowChrome? PreviousChrome { get; set; }

        /// <summary>
        /// 挂载是否已经完整做完。不能拿 Hook / PreviousChrome 是否非空来当「已挂载」的标志 ——
        /// 那几个字段是在挂载过程中逐个填的，中途抛异常会留下「半挂载」状态，让重试直接短路。
        /// </summary>
        public bool IsAttached { get; set; }

        public List<CommandBinding> CommandBindings { get; } = new();
    }

    private static FramelessState GetOrCreateState(Window window)
    {
        if (window.GetValue(StateProperty) is FramelessState existing)
        {
            return existing;
        }

        var state = new FramelessState(window);
        window.SetValue(StateProperty, state);
        return state;
    }

    private static FramelessState? GetState(Window window) => window.GetValue(StateProperty) as FramelessState;

    #endregion

    #region 挂载 / 卸载

    private static void Attach(Window window)
    {
        FramelessState state = GetOrCreateState(window);
        if (state.IsAttached)
        {
            return; // 已经挂着
        }

        // ★ 下面两段的顺序不能换。
        // WindowChromeWorker 就是在 SetWindowChrome 这一刻决定「立刻挂钩子」还是「等 SourceInitialized」，
        // 而 HwndSource 的钩子是「后注册的先跑」、且先返回 handled=true 的会截断后续。
        // 所以我们必须排在它之后注册，才能抢到 WM_NCHITTEST —— 否则我们的 HTMAXBUTTON 永远轮不到，
        // 表现是「Snap Layouts 静默不出现」，一点报错都没有。
        state.PreviousChrome = WindowChrome.GetWindowChrome(window);
        WindowChrome.SetWindowChrome(window, BuildChrome());

        AddCommandBindings(state);
        window.Loaded += OnWindowLoaded;
        window.Closed += OnWindowClosed;

        // HwndSource.FromHwnd(IntPtr.Zero) 不是返回 null，而是抛 ArgumentException("零的 Hwnd 无效")。
        // XAML 解析阶段窗口还没有 HWND，handle 就是零，所以必须先判零再调。
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized += OnWindowSourceInitialized; // ← 必须晚于上面的 SetWindowChrome
        }
        else
        {
            AddHook(state, HwndSource.FromHwnd(handle)!); // ← 同上
        }

        state.IsAttached = true;
    }

    private static void Detach(Window window)
    {
        FramelessState? state = GetState(window);
        if (state is null)
        {
            return;
        }

        // 先摘自己的钩子，再还原 chrome：还原 chrome 会让 WindowChromeWorker 重新注册它自己的钩子
        RemoveHook(state);
        Unsubscribe(window);
        RemoveCommandBindings(window, state);

        WindowChrome.SetWindowChrome(window, state.PreviousChrome);
        window.ClearValue(StateProperty);
    }

    private static void Unsubscribe(Window window)
    {
        window.SourceInitialized -= OnWindowSourceInitialized;
        window.Loaded -= OnWindowLoaded;
        window.Closed -= OnWindowClosed;
    }

    /// <summary>
    /// 窗口原本没有 WindowChrome 时，<see cref="WindowChrome.GlassFrameThickness"/> 的默认值来自系统参数
    /// （Win11 实测约 3.2,26.4,3.2,3.2），非零 —— 所以玻璃路径默认就是开的。
    /// </summary>
    private static WindowChrome BuildChrome() => new()
    {
        CaptionHeight = CaptionHeight,
        ResizeBorderThickness = new Thickness(ResizeBorderThickness),
        GlassFrameThickness = WindowChrome.GlassFrameCompleteThickness, // = new Thickness(-1)，整窗玻璃
        UseAeroCaptionButtons = false,                                  // 不让 DWM 再画一套系统按钮
        NonClientFrameEdges = NonClientFrameEdges.Right,                // 剔除右侧系统边框
    };

    private static void AddHook(FramelessState state, HwndSource source)
    {
        if (state.Hook is not null)
        {
            return;
        }

        // Win10 及更早：HTMAXBUTTON 会被 DefWindowProc 当成它自己算的标题栏按钮，
        // 与手动接管撞成「最大化两次」。这些系统上整个不接管，按钮退回靠 Command 工作。
        if (Environment.OSVersion.Version.Build < Windows11Build)
        {
            return;
        }

        // 用 lambda 存成一个变量：这样只生成一个委托实例，RemoveHook 才认得出来。
        // 写成 AddHook(OnMessage) / RemoveHook(OnMessage) 会各生成一个新委托，钩子摘不掉（还会吊住整个窗口）。
        HwndSourceHook hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            HookProc(state, msg, lParam, ref handled);

        state.Source = source;
        state.Hook = hook;
        source.AddHook(hook);
    }

    private static void RemoveHook(FramelessState state)
    {
        if (state.Hook is null)
        {
            return;
        }

        state.Source?.RemoveHook(state.Hook);
        state.Source = null;
        state.Hook = null;
    }

    private static void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window || GetState(window) is not FramelessState state)
        {
            return;
        }

        // 不缓存 HwndSource：WindowChromeWorker 自己也特意不缓存（换系统主题时它可能消失），要用时重新取
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            AddHook(state, HwndSource.FromHwnd(handle)!);
        }
    }

    private static void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Window window || GetState(window) is not FramelessState state)
        {
            return;
        }

        // 模板部件要等模板套用之后才拿得到，Loaded 是第一个安全时机
        ResolveMaximizeButton(state);
    }

    private static void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not Window window || GetState(window) is not FramelessState state)
        {
            return;
        }

        RemoveHook(state);
        Unsubscribe(window);

        // ★ 这里绝不能碰 WindowChrome。WindowChromeWorker 先于我们订阅 Closed，它已经做过
        // _RestoreStandardChromeState(true)；我们再 SetWindowChrome 会走到 _UnhookCustomChrome，
        // 去 RemoveHook 一个正在销毁的 HwndSource。
        window.ClearValue(StateProperty);
    }

    #endregion

    #region 模板里的三个系统按钮

    private static void AddCommandBindings(FramelessState state)
    {
        // SystemCommands 那几个 RoutedCommand，WPF 自己不注册任何 CommandBinding
        // （Window.cs 只给 DialogCancelCommand 注册过），不自己挂的话模板里的按钮就是灰的。
        // 只给 Executed、不给 CanExecute：这种组合下 OnCanExecute 会自动置 e.CanExecute = true。
        var registrations = new (RoutedCommand Command, Action<Window> Execute)[]
        {
            (SystemCommands.MinimizeWindowCommand, SystemCommands.MinimizeWindow),
            (SystemCommands.MaximizeWindowCommand, SystemCommands.MaximizeWindow),
            (SystemCommands.RestoreWindowCommand, SystemCommands.RestoreWindow),
            (SystemCommands.CloseWindowCommand, SystemCommands.CloseWindow),
        };

        foreach ((RoutedCommand command, Action<Window> execute) in registrations)
        {
            Window window = state.Window;
            var binding = new CommandBinding(command, (_, _) => execute(window));
            state.CommandBindings.Add(binding);
            window.CommandBindings.Add(binding);
        }
    }

    private static void RemoveCommandBindings(Window window, FramelessState state)
    {
        foreach (CommandBinding binding in state.CommandBindings)
        {
            window.CommandBindings.Remove(binding);
        }

        state.CommandBindings.Clear();
    }

    #endregion

    #region WM_NCHITTEST 钩子

    private static IntPtr HookProc(FramelessState state, int msg, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                if (HitMaximizeButton(state, lParam))
                {
                    SetFlag(state.Window, IsMaximizeButtonHoveredPropertyKey, true);
                    handled = true; // 必须抢在 WindowChromeWorker 前面，它三条出口都会 handled = true
                    return new IntPtr(HTMAXBUTTON);
                }

                // 不落在按钮上就把悬停清掉 —— 实际大部分时候是靠这条清，而不是靠 WM_NCMOUSELEAVE
                SetFlag(state.Window, IsMaximizeButtonHoveredPropertyKey, false);
                break;

            case WM_NCLBUTTONDOWN:
                if (HitMaximizeButton(state, lParam))
                {
                    SetFlag(state.Window, IsMaximizeButtonPressedPropertyKey, true);
                    handled = true; // 吃掉：别让系统按它自己算出来的标题栏按钮位置再处理一遍
                }
                break;

            case WM_NCLBUTTONUP:
                if (GetIsMaximizeButtonPressed(state.Window))
                {
                    SetFlag(state.Window, IsMaximizeButtonPressedPropertyKey, false);
                    if (HitMaximizeButton(state, lParam))
                    {
                        ToggleMaximize(state.Window);
                    }

                    handled = true;
                }
                break;

            case WM_NCLBUTTONDBLCLK:
                if (HitMaximizeButton(state, lParam))
                {
                    handled = true; // 否则 DefWindowProc 还会再发一次 SC_MAXIMIZE
                }
                break;

            case WM_NCMOUSELEAVE:
                SetFlag(state.Window, IsMaximizeButtonHoveredPropertyKey, false);
                SetFlag(state.Window, IsMaximizeButtonPressedPropertyKey, false);
                break;
        }

        return IntPtr.Zero;
    }

    private static bool HitMaximizeButton(FramelessState state, IntPtr lParam)
    {
        Button? button = state.MaximizeButton;
        if (button is null)
        {
            // 窗口没套 LightUiWindowStyle 就永远找不到这个部件，标记一下别在每次鼠标移动里重试
            if (state.MaximizeButtonResolveFailed || !state.Window.IsLoaded)
            {
                return false;
            }

            button = ResolveMaximizeButton(state);
            if (button is null)
            {
                return false;
            }
        }

        // 模板重建后旧实例已经脱离视觉树，PointFromScreen 会抛异常
        if (PresentationSource.FromVisual(button) is null)
        {
            state.MaximizeButton = null;
            return false;
        }

        if (!button.IsVisible)
        {
            return false;
        }

        // lParam 的低/高 16 位是「屏幕物理像素」，而且是有符号的 ——
        // 显示器摆在主屏左边或上面时坐标是负的，所以按 short 取，别用 & 0xFFFF 拼成无符号。
        long packed = (long)lParam;
        var screenPoint = new Point(
            unchecked((short)( packed & 0xFFFF )),
            unchecked((short)( ( packed >> 16 ) & 0xFFFF )));

        // PointFromScreen 收的正是物理像素，吐出来已经是这个按钮自己的 DIP 坐标 ——
        // 中间不要再自己乘 DPI 系数，乘了反而错。
        return new Rect(button.RenderSize).Contains(button.PointFromScreen(screenPoint));
    }

    private static Button? ResolveMaximizeButton(FramelessState state)
    {
        // Template.FindName 是命名域字典查找，正是模板部件的正确取法（GetTemplatePart 内部就是这个）
        var button = state.Window.Template?.FindName("PART_MaximizeButton", state.Window) as Button;
        if (button is null)
        {
            state.MaximizeButtonResolveFailed = true;
        }
        else
        {
            state.MaximizeButton = button;
        }

        return button;
    }

    private static void ToggleMaximize(Window window)
    {
        // 走 SystemCommands 而不是直接改 WindowState：它内部是 PostMessage(WM_SYSCOMMAND)，
        // 异步的，不会在钩子里重入消息循环，动画和贴边状态也和系统一致。
        if (window.WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(window);
        }
        else
        {
            SystemCommands.MaximizeWindow(window);
        }
    }

    private static void SetFlag(DependencyObject target, DependencyPropertyKey key, bool value) =>
        target.SetValue(key, value);

    #endregion
}
