namespace LightUi.Controls;

/// <summary>
/// 给 ContextMenu 补上「弹层阴影预留」的反向偏移。
/// 留白留在样式模板里（Border 的 Margin），而 HorizontalOffset / VerticalOffset 由这里在 Opened 时设成局部值 ——
/// 有些宿主（TextBox 那类文本编辑控件）在打开菜单时会自己改写这两个属性，写在样式 Setter 里会被盖掉，
/// 挂到 Opened 之后再设，就没有人能覆盖了。
///
/// 用法（样式里挂开关，见 styles\Menu.xaml）：
/// <code>
/// &lt;Setter Property="ui:ContextMenuShadow.FixOffsets" Value="True" /&gt;
/// </code>
/// </summary>
public static class ContextMenuShadow
{
    public static readonly DependencyProperty FixOffsetsProperty =
        DependencyProperty.RegisterAttached(
            "FixOffsets",
            typeof(bool),
            typeof(ContextMenuShadow),
            new PropertyMetadata(false, OnFixOffsetsChanged));

    public static bool GetFixOffsets(ContextMenu menu) => (bool)menu.GetValue(FixOffsetsProperty);

    public static void SetFixOffsets(ContextMenu menu, bool value) => menu.SetValue(FixOffsetsProperty, value);

    private static void OnFixOffsetsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ContextMenu menu)
        {
            return;
        }

        menu.Opened -= OnOpened;                     // 防重复挂（样式重算时会再次进这里）
        if (e.NewValue is true)
        {
            menu.Opened += OnOpened;
        }
    }

    private static void OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
        {
            return;
        }

        // 键盘打开时 Placement 是 Center：留白本来就对称，面板还是居中的，补了反而偏
        if (menu.Placement == PlacementMode.Center)
        {
            return;
        }

        // 与样式里 Border 的 Margin 同源，读资源键而不是写字面量（FlyoutBase 也是这么读的）
        if (Application.Current?.TryFindResource("FlyoutShadowRoom") is not Thickness room)
        {
            return;
        }

        menu.HorizontalOffset = -room.Left;
        menu.VerticalOffset = -room.Top;
    }
}
