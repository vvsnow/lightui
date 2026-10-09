namespace LightUi.VsCodeFrameTest;

/// <summary>
/// 标题栏按钮的身份。对应 Electron 的 <c>ViewID</c>：<c>VIEW_ID_MINIMIZE_BUTTON</c> /
/// <c>VIEW_ID_MAXIMIZE_BUTTON</c> / <c>VIEW_ID_RESTORE_BUTTON</c> / <c>VIEW_ID_CLOSE_BUTTON</c>。
///
/// 存在的理由：返回 HTMINBUTTON/HTMAXBUTTON/HTCLOSE 之后那块是非客户区，
/// WPF 输入栈不参与、<c>Button.IsMouseOver</c> 恒为 false，悬停/按下状态只能由窗口过程判定后告诉 XAML。
/// </summary>
public enum CaptionButton
{
    /// <summary>鼠标不在任何标题栏按钮上。</summary>
    None,

    Minimize,

    Maximize,

    /// <summary>已最大化时显示的那颗（对应 Electron 的 restore_button_），命中也返回 HTMAXBUTTON。</summary>
    Restore,

    Close,
}
