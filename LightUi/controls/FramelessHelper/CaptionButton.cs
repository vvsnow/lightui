namespace LightUi.Controls;

/// <summary>
/// 标题栏按钮的身份。由 DefaultFramelessWindowStyle 的触发器消费
/// （FramelessHelper.HoveredCaptionButton / PressedCaptionButton）。
///
/// 要有这么个枚举：返回 HTMINBUTTON/HTMAXBUTTON/HTCLOSE 之后那块是非客户区，
/// WPF 输入栈不参与、<c>Button.IsMouseOver</c> 恒为 false —— 悬停/按下只能由窗口过程判定后告诉 XAML。
/// </summary>
public enum CaptionButton
{
    /// <summary>鼠标不在任何标题栏按钮上。</summary>
    None,

    Minimize,

    Maximize,

    /// <summary>
    /// 已最大化时那颗按钮的身份（与最大化键是<b>同一颗</b>元素，见 <c>FramelessWindowAdapter</c> 里的说明）。
    /// 命中也返回 HTMAXBUTTON。
    /// </summary>
    Restore,

    Close,
}
