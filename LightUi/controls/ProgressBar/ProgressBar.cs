using System.Windows.Media.Animation;

namespace LightUi.Controls;

/// <summary>
/// 进度条（参考 ModernWpf 的简化版）。
///
/// 为什么继承 RangeBase 而不是 WPF 原生的 ProgressBar：
/// styles/ProgressBar.xaml 给原生 ProgressBar 套的隐式样式，模板根元素就是本控件。
/// 若本控件继承原生 ProgressBar，WPF 的隐式样式查找会沿基类链命中那份样式，
/// 于是模板再次实例化本控件 —— 无限递归。继承 RangeBase 后基类链是 Control → FrameworkElement，
/// 不会命中。（ModernWpf 的 ProgressBar 同样继承 RangeBase。）
///
/// 确定态：指示块宽度按 Value 算。值变化时让指示块从「旧右边界」滑到「新右边界」，形成「追赶」动画。
/// 不确定态：40% / 60% 两条指示块以 2s 周期接力流过，位移随实际宽度自适应。
/// 指示块的显隐靠「宽度归零」而不是 Opacity，免得依赖动画停止时属性值的回滚行为。
///
/// 动画为什么写在代码里、而不是照 ModernWpf 那样放 XAML 的 VisualState + Storyboard：
/// ModernWpf 把位移量放在 Storyboard 关键帧的 Value 绑定上
/// （{Binding RelativeSource={RelativeSource TemplatedParent}, Path=TemplateSettings.Xxx}）。
/// 本项目里实测这种绑定不生效 —— 关键帧绑定只在故事板启动那一刻求值一次，而模板应用时
/// ActualWidth 还是 0，四个位移量会被读成 0，指示块从此僵在 X=0；即便事后重播也没用。
/// 位移量本来就依赖 ActualWidth，直接在代码里算好再用 BeginAnimation 驱动，可靠得多。
/// </summary>
public class ProgressBar : RangeBase
{
    private const string DeterminateIndicatorPartName = "DeterminateProgressBarIndicator";
    private const string IndeterminateIndicatorPartName = "IndeterminateProgressBarIndicator";
    private const string IndeterminateIndicator2PartName = "IndeterminateProgressBarIndicator2";
    private const string ClipHostPartName = "TrackClip";

    /// <summary>不确定态第一条指示块的宽度占轨道比例。</summary>
    private const double IndeterminateIndicatorRatio = 0.4;

    /// <summary>不确定态第二条指示块的宽度占轨道比例。</summary>
    private const double IndeterminateIndicator2Ratio = 0.6;

    /// <summary>不确定态动画周期。</summary>
    private static readonly TimeSpan IndeterminateCycle = TimeSpan.FromSeconds(2);

    /// <summary>确定态「追赶」动画时长。</summary>
    private static readonly TimeSpan CatchUpDuration = TimeSpan.FromSeconds(0.367);

    private Border? _determinateIndicator;
    private Border? _indeterminateIndicator;
    private Border? _indeterminateIndicator2;
    private Grid? _clipHost;

    private TranslateTransform? _determinateTransform;
    private TranslateTransform? _indeterminateTransform;
    private TranslateTransform? _indeterminateTransform2;

    /// <summary>不确定态开关。true 时忽略 Value，显示连续流动动画。</summary>
    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(ProgressBar),
            new PropertyMetadata(false, OnIsIndeterminateChanged));

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// 圆角。Control 上没有这个属性（Border 才有），这里作为实例 DP 提供，
    /// 供模板显示和代码里算裁剪几何共用。
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(ProgressBar),
            new PropertyMetadata(default(CornerRadius), OnCornerRadiusChanged));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _determinateIndicator = GetTemplateChild(DeterminateIndicatorPartName) as Border;
        _indeterminateIndicator = GetTemplateChild(IndeterminateIndicatorPartName) as Border;
        _indeterminateIndicator2 = GetTemplateChild(IndeterminateIndicator2PartName) as Border;
        _clipHost = GetTemplateChild(ClipHostPartName) as Grid;

        _determinateTransform = EnsureUnfrozenTransform(_determinateIndicator);
        _indeterminateTransform = EnsureUnfrozenTransform(_indeterminateIndicator);
        _indeterminateTransform2 = EnsureUnfrozenTransform(_indeterminateIndicator2);

        Refresh();
    }

    /// <summary>
    /// 取指示块的位移变换，并保证它是「未冻结」的实例。
    ///
    /// ControlTemplate 里的 Freezable 会在模板密封时被 Freeze。冻结对象不能直接 BeginAnimation
    /// （会抛 InvalidOperationException「该对象已密封或已冻结」），所以这里换成一个全新的实例。
    /// 走 Storyboard 时 WPF 会自己克隆，看不出问题；直接驱动动画就必须自己处理。
    /// </summary>
    private static TranslateTransform? EnsureUnfrozenTransform(Border? indicator)
    {
        if (indicator is null)
        {
            return null;
        }

        if (indicator.RenderTransform is TranslateTransform { IsFrozen: false } existing)
        {
            return existing;
        }

        var transform = new TranslateTransform();
        indicator.RenderTransform = transform;
        return transform;
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        SetProgressBarIndicatorWidth();
    }

    protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
    {
        base.OnMinimumChanged(oldMinimum, newMinimum);
        SetProgressBarIndicatorWidth();
    }

    protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
    {
        base.OnMaximumChanged(oldMaximum, newMaximum);
        SetProgressBarIndicatorWidth();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // 两态都依赖实际宽度：确定态算指示块宽度，不确定态算指示块宽度 + 滑动位移。
        // 动画值是这里现算现用的具体数字（不是绑定），所以尺寸一变就能立刻重播出正确结果。
        Refresh();
    }

    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ProgressBar)d).Refresh();
    }

    /// <summary>圆角变了只需刷新裁剪几何，不必重播动画（重播会让不确定态跳一下）。</summary>
    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var progressBar = (ProgressBar)d;
        progressBar.UpdateClip(progressBar.ActualWidth, progressBar.ActualHeight);
    }

    /// <summary>按当前 <see cref="IsIndeterminate"/> 切到对应态。</summary>
    private void Refresh()
    {
        if (IsIndeterminate)
        {
            EnterIndeterminate();
        }
        else
        {
            EnterDeterminate();
        }
    }

    private void EnterIndeterminate()
    {
        // 确定态指示块宽度归零即不可见，顺手清掉它的位置
        if (_determinateIndicator is not null)
        {
            _determinateIndicator.Width = 0;
        }

        _determinateTransform?.BeginAnimation(TranslateTransform.XProperty, null);

        double trackWidth = ActualWidth;

        double width1 = Math.Max(0, trackWidth * IndeterminateIndicatorRatio);
        double width2 = Math.Max(0, trackWidth * IndeterminateIndicator2Ratio);

        if (_indeterminateIndicator is not null)
        {
            _indeterminateIndicator.Width = width1;
        }

        if (_indeterminateIndicator2 is not null)
        {
            _indeterminateIndicator2.Width = width2;
        }

        UpdateClip(trackWidth, ActualHeight);

        // 两条指示块各自从轨道左侧屏外滑到右侧屏外，位移量按宽度缩放（比例照抄 ModernWpf）：
        // 1 号：0 → 1.5s 滑完，停在终点等周期结束；
        // 2 号：先原地等 0.75s，再用 1.25s 滑完 —— 两者在 t=2s 同时收尾，形成接力。
        StartSweep(_indeterminateTransform, width1 * -1.0, width1 * 3.0, TimeSpan.Zero, TimeSpan.FromSeconds(1.5));
        StartSweep(_indeterminateTransform2, width2 * -1.5, width2 * 1.66, TimeSpan.FromSeconds(0.75), TimeSpan.FromSeconds(1.25));
    }

    private void EnterDeterminate()
    {
        // 不确定指示块宽度归零即不可见，并停掉它们的位移动画
        _indeterminateTransform?.BeginAnimation(TranslateTransform.XProperty, null);
        _indeterminateTransform2?.BeginAnimation(TranslateTransform.XProperty, null);

        if (_indeterminateIndicator is not null)
        {
            _indeterminateIndicator.Width = 0;
        }

        if (_indeterminateIndicator2 is not null)
        {
            _indeterminateIndicator2.Width = 0;
        }

        SetProgressBarIndicatorWidth();
    }

    /// <summary>把确定态指示块的宽度设成 Value 对应的值，并从旧位置滑过去。</summary>
    private void SetProgressBarIndicatorWidth()
    {
        if (IsIndeterminate || _determinateIndicator is null)
        {
            return;
        }

        double trackWidth = ActualWidth;
        double previousWidth = double.IsNaN(_determinateIndicator.Width) ? 0 : _determinateIndicator.Width;

        double newWidth;

        if (trackWidth <= 0 || Maximum - Minimum <= 0)
        {
            // 尺寸或区间还没就绪，别做除法
            newWidth = 0;
        }
        else
        {
            newWidth = Math.Clamp(trackWidth / (Maximum - Minimum) * (Value - Minimum), 0, trackWidth);
        }

        _determinateIndicator.Width = newWidth;
        UpdateClip(trackWidth, ActualHeight);

        if (_determinateTransform is null)
        {
            return;
        }

        // 旧右边界与新右边界之差：指示块从这个偏移滑回 0，看起来就是「追」到了新位置
        double delta = previousWidth - newWidth;

        if (delta == 0 || trackWidth <= 0)
        {
            _determinateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            return;
        }

        var animation = new DoubleAnimationUsingKeyFrames { Duration = new Duration(CatchUpDuration) };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(delta, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(CatchUpDuration), new KeySpline(0.1, 0.9, 0.2, 1.0)));

        _determinateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>让指示块在整条轨道上跑一趟，随后按周期循环。</summary>
    private static void StartSweep(TranslateTransform? transform, double from, double to, TimeSpan hold, TimeSpan sweep)
    {
        if (transform is null)
        {
            return;
        }

        var sweepEnd = hold + sweep;
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(IndeterminateCycle),
            RepeatBehavior = RepeatBehavior.Forever,
        };

        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));

        if (hold > TimeSpan.Zero)
        {
            // 原地等待，让两条指示块错开
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(hold)));
        }

        // KeySpline 0.4,0.0,0.6,1.0 是标准慢入慢出
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(sweepEnd), new KeySpline(0.4, 0.0, 0.6, 1.0)));

        if (sweepEnd < IndeterminateCycle)
        {
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(to, KeyTime.FromTimeSpan(IndeterminateCycle)));
        }

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    /// <summary>按圆角裁掉滑出轨道的部分。</summary>
    private void UpdateClip(double width, double height)
    {
        if (_clipHost is null)
        {
            return;
        }

        double radius = double.IsNaN(CornerRadius.TopLeft) ? 0 : Math.Max(0, CornerRadius.TopLeft);

        _clipHost.Clip = width > 0 && height > 0
            ? new RectangleGeometry(new Rect(0, 0, width, height), radius, radius)
            : null;
    }
}
