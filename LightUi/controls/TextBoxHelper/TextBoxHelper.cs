using System.Diagnostics;
using System.Windows.Controls;

namespace LightUi.Controls;

/// <summary>
/// 给 <see cref="TextBox"/> / <see cref="PasswordBox"/> 加占位符文字：内容为空时显示，输入后隐藏。
///
/// 用法：
/// <code>
/// &lt;TextBox ui:TextBoxHelper.Placeholder="请输入用户名" /&gt;
/// &lt;PasswordBox ui:TextBoxHelper.Placeholder="请输入密码" /&gt;
/// </code>
/// 只对 TextBox / PasswordBox 生效（标到别的类型上只打一条 Debug 警告，不抛 —— 回调里抛会毁掉整份 XAML 加载）；
/// 模板里要有名为 <c>PART_Placeholder</c> 的 TextBlock，库自带模板已提供。
///
/// 显示状态由本类在代码里驱动、不靠 XAML 触发器：模板里的 DataTrigger 绑 TemplatedParent 不参与匹配，
/// 而 <c>PasswordBox.Password</c> 背后的 DP 是 internal（触发器写不了），所以状态做成公开的只读附加属性
/// <see cref="HasTextProperty"/> 给模板消费。
/// </summary>
public static class TextBoxHelper
{
    #region Placeholder

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(TextBoxHelper),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

    public static string GetPlaceholder(DependencyObject obj) =>
        (string)obj.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject obj, string value) =>
        obj.SetValue(PlaceholderProperty, value);

    #endregion

    #region HasText

    private static readonly DependencyPropertyKey HasTextPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "HasText",
            typeof(bool),
            typeof(TextBoxHelper),
            new PropertyMetadata(false));

    /// <summary>
    /// 控件里当前有没有内容（<c>TextBox.Text</c> / <c>PasswordBox.Password</c> 非空）。
    /// 只读：模板靠它收起占位符，外部写不了。
    /// </summary>
    public static readonly DependencyProperty HasTextProperty = HasTextPropertyKey.DependencyProperty;

    public static bool GetHasText(Control control) => (bool)control.GetValue(HasTextProperty);

    private static void SetHasText(Control control, bool value) => control.SetValue(HasTextPropertyKey, value);

    private static void UpdateHasText(Control control) => SetHasText(control, control switch
    {
        TextBox textBox => !string.IsNullOrEmpty(textBox.Text),
        PasswordBox passwordBox => !string.IsNullOrEmpty(passwordBox.Password),
        _ => false,
    });

    #endregion

    #region 挂载

    /// <summary>已订阅标记：Placeholder 被重复赋值时不重复订阅。</summary>
    private static readonly DependencyProperty HookedProperty =
        DependencyProperty.RegisterAttached("Hooked", typeof(bool), typeof(TextBoxHelper), new PropertyMetadata(false));

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not (TextBox or PasswordBox))
        {
            Debug.WriteLine(
                "[LightUi] TextBoxHelper.Placeholder 只能用在 TextBox / PasswordBox 上。");
            return;
        }

        var control = (Control)d;
        if ((bool)control.GetValue(HookedProperty))
        {
            return;
        }

        control.SetValue(HookedProperty, true);

        switch (control)
        {
            case TextBox textBox:
                textBox.TextChanged += OnTextChanged;
                break;
            case PasswordBox passwordBox:
                passwordBox.PasswordChanged += OnPasswordChanged;
                break;
        }

        // 值可能是「先有内容、后挂 Placeholder」（代码里先赋值后设属性）—— 立刻算一次
        UpdateHasText(control);
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e) => UpdateHasText((Control)sender);

    private static void OnPasswordChanged(object sender, RoutedEventArgs e) => UpdateHasText((Control)sender);

    #endregion
}
