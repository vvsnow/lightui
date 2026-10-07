using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LightUi.Tools.XamlCheck;

/// <summary>
/// 把 LightUi 里每个控件都实例化一次并逼它装载模板（Measure/Arrange → ApplyTemplate），
/// 弹层类（Flyout / MenuFlyout）用 ShowAt 真打开一次。
///
/// 为什么需要它：
///   控件模板是**按需装载**的 —— 只有当某个控件第一次出现在界面上时，它的 ControlTemplate（BAML）
///   才会被解析并赋值。所以"进程还活着 / 主窗口出来了"这种冒烟测试覆盖不到大部分模板，
///   一旦某个模板里有非法值，表现是"启动正常、点进某个页面才崩"。
///
///   真实案例：把尺寸令牌 ControlBorderThickness 声明成了 sys:Double，而它被用在 14 处
///   BorderThickness（Thickness 属性）上 —— 启动毫无异常，进"通用控件"页才抛
///   「"1" 不是属性 "BorderThickness" 的有效值」。这个脚本当时就能立刻报出来。
///
/// 退出码：0 = 全通过，1 = 有模板装载失败，2 = 资源字典没加载上（下面所有检查都无意义）。
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/LightUi;component/ControlsResources.xaml", UriKind.Absolute),
        });

        // 防假绿：资源字典若没加载上，下面每个控件都会退回 WPF 默认主题样式，全部"通过"却是假的。
        if (app.TryFindResource("ControlHeight") is null || app.TryFindResource("SystemButtonStyle") is null)
        {
            Console.WriteLine("  FATAL  LightUi 的资源字典没加载上（ControlsResources.xaml）—— 后面的检查都是假绿，已中止");
            return 2;
        }

        Type[] controlTypes =
        {
            // 套了 LightUi 样式的框架控件
            typeof(Button), typeof(RepeatButton), typeof(ToggleButton), typeof(CheckBox),
            typeof(TextBox), typeof(ComboBox), typeof(ComboBoxItem),
            typeof(ListBox), typeof(ListBoxItem), typeof(MenuItem), typeof(ContextMenu),
            typeof(Separator), typeof(ScrollBar), typeof(ScrollViewer), typeof(ProgressBar), typeof(ToolTip),
            // LightUi 自己的控件
            typeof(Controls.DropDownButton),
            typeof(Controls.ToggleSwitch),
            typeof(Controls.FontIcon),
            typeof(Controls.HyperlinkButton),
            typeof(Controls.SimpleStackPanel),
        };

        int failed = 0;
        foreach (Type type in controlTypes)
        {
            if (TryCheck(type.Name, () =>
                {
                    var element = (FrameworkElement)Activator.CreateInstance(type)!;
                    element.Measure(new Size(300, 100)); // 触发 ApplyTemplate
                    element.Arrange(new Rect(0, 0, 300, 100));
                }))
            {
                continue;
            }

            failed++;
        }

        // FlyoutBase 不是 FrameworkElement，模板在"打开弹层"时才装载，得给真锚点 + 真 ShowAt。
        // 顺便这个宿主 Window 会套上 LightUiWindowStyle —— 窗口模板也一并验了。
        var host = new Window
        {
            Width = 1,
            Height = 1,
            Left = -20000,
            Top = -20000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
        };
        var anchor = new Button();
        host.Content = anchor;
        host.Show();
        host.UpdateLayout();
        Console.WriteLine("  OK    Window + LightUiWindowStyle（宿主）");

        if (!TryCheck("Flyout（presenter）", () =>
            {
                var flyout = new Controls.Flyout { Content = "probe" };
                flyout.ShowAt(anchor);
                host.UpdateLayout();
                flyout.Hide();
            }))
        {
            failed++;
        }

        if (!TryCheck("MenuFlyout（presenter）", () =>
            {
                var menu = new Controls.MenuFlyout();
                menu.Items.Add(new MenuItem { Header = "probe" });
                menu.ShowAt(anchor);
                host.UpdateLayout();
                menu.Hide();
            }))
        {
            failed++;
        }

        host.Close();

        int total = controlTypes.Length + 3;
        Console.WriteLine(failed == 0
            ? $"\n全部 {total} 项模板装载通过"
            : $"\n{failed} / {total} 项失败");

        return failed == 0 ? 0 : 1;
    }

    private static bool TryCheck(string name, Action check)
    {
        try
        {
            check();
            Console.WriteLine($"  OK    {name}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL  {name}");
            Console.WriteLine($"        {ex.GetBaseException().GetType().Name}: {ex.GetBaseException().Message}");
            return false;
        }
    }
}
