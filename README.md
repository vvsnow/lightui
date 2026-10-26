# LightUi

一个轻量的 WPF 控件库：把 WPF 自带控件统一重刷成一套现代扁平外观，再补上几个 WPF 没有的控件（Flyout / MenuFlyout / DropDownButton / ToggleSwitch / FontIcon），以及一套**不依赖 `WindowChrome`** 的无边框窗口方案。

- 目标框架 `net472` / `net10.0-windows`（多目标；.NET Framework 4.7.2+ 与现代 .NET 共用同一份源码），语言 C# / XAML
- 单程序集 `LightUi.dll`，根命名空间 `LightUi`
- 视觉参考 [ModernWpf](https://github.com/Kinnara/ModernWpf) 0.9.6，但代码与模板全部重写

仓库里有两个工程：

| 工程 | 作用 |
|---|---|
| `LightUi` | 库本体（`ControlsResources.xaml` 是唯一的资源入口） |
| `LightUi.Demo` | 演示程序，Caliburn.Micro 4.0.230 + PropertyChanged.Fody，左侧导航对应本文档的章节顺序 |

## 目录

- [一、快速开始](#一快速开始)
- [二、主题与颜色](#二主题与颜色)
- [三、无边框窗口](#三无边框窗口)
- [四、Button 页](#四button-页)
- [五、Common Control 页](#五common-control-页)
- [六、Extended Control 页](#六extended-control-页)
- [七、Flyout Control 页](#七flyout-control-页)
- [八、Helper 与附加属性汇总](#八helper-与附加属性汇总)
- [九、已知限制与注意事项](#九已知限制与注意事项)

## 一、快速开始

### 1.1 引用库

在 `App.xaml` 里合并**唯一入口** `ControlsResources.xaml`，它会把自己下面所有样式字典都带进来：

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/LightUi;component/ControlsResources.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### 1.2 命名空间

```xml
xmlns:ui="http://schemas.lightui.com/2019"
```

这一个前缀同时映射了 `LightUi`、`LightUi.Controls`、`LightUi.Controls.Primitives` 三个命名空间，所以控件（`ui:FontIcon`）、附加属性（`ui:TextBoxHelper.Placeholder`）、基类静态成员（`ui:FlyoutBase.CloseCommand`）都走 `ui:`。

### 1.3 绝大多数控件不用写 Style

库里的样式基本都是**隐式样式**（无 `x:Key`），合并资源字典后 `<Button />`、`<TextBox />` 自动带外观，**不需要** `Style="{StaticResource ...}"`。

日常要显式引用的只有两个：`AccentButtonStyle`、`CircleButtonStyle`。另外 `FramelessSystemButtonStyle` / `FramelessCloseButtonStyle` 在自定义标题栏按钮时会用到（见 [3.5](#35-标题栏按钮样式)）。

> ⚠ 隐式样式按元素的**精确类型**匹配，不会命中派生类。XAML 里用 `x:Class` 声明的 `Window` 都是派生类，命不中 `<Style TargetType="Window">` —— 无边框窗口的默认样式因此由 `FramelessHelper` 在挂载时用代码补上（见[第三节](#三无边框窗口)）。

### 1.4 DPI 感知必须由宿主 exe 声明

库改不了进程的 DPI 感知，那属于宿主 exe 的 `app.manifest`：

```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
```

不声明的话，显示缩放一变整窗会被系统位图拉伸、字体发糊。`FramelessHelper` 会在挂载时用 `GetDpiForWindow` 与 `VisualTreeHelper.GetDpi` 交叉核对一次，不一致就打一条 `Debug` 警告（**只警告，不抛异常**）。

参考 `LightUi.Demo/app.manifest`。

## 二、主题与颜色

### 2.1 主题切换

`ThemeManager` 是纯静态 API：

```csharp
using LightUi;

ThemeManager.ApplyTheme(ApplicationTheme.Dark);   // Light / Dark
ThemeManager.ApplyTheme(ApplicationTheme.Light);
```

| 成员 | 说明 |
|---|---|
| `ApplyTheme(ApplicationTheme)` | 切主题。把主题 xaml 里的每个 brush 逐个写进 `Application.Resources` **自己那一层**的键上 |
| `ApplyAccentColor(Color)` | 自定义强调色。按钮 / 复选框 / 开关 / 链接 / 进度条 / 光标选中色一起变 |
| `ResetAccentColor()` | 丢掉自定义强调色，回到主题自带的那套 |
| `Current.AccentColor` | 当前生效的强调色（直接读资源里的 `AccentBrush`，不另存状态） |
| `Current.CurrentTheme` | 当前主题，**只读 DP**，可绑定 / 可用于触发器 |
| `CurrentThemeChanged` | 静态事件，切完主题触发 |

默认主题是 **Light**（`ThemeResources.xaml` 合并的就是 `Light.xaml`）。

**为什么必须写顶层键**：Light / Dark 挂在三层字典深处（`Application.Resources` → `ControlsResources.xaml` → `Theme/ThemeResources.xaml`），而 WPF 只在资源链**顶层**变更时才让全树 `DynamicResource` 失效重求值。改嵌套层的值会变，但界面不重绘。

**自定义强调色的推导规则**（`ApplyAccentColor` 里）：

- 其上的文字色用 **YIQ** 判亮暗（分界点 180），亮色配黑字、暗色配白字。用 YIQ 而不是 WCAG，是因为 WCAG 会低估高饱和色（尤其纯红）的"暗" —— 纯红按 WCAG 算黑字更优，但观感是黑字糊在红底上
- hover / pressed 底色朝**远离文字色**的方向混（12% / 25%）：白字压暗、黑字提亮，两个分支的对比度都单调升高
- 再次应用主题自带的那个色**等价于重置**，走 `ResetAccentColor` 的短路，不会产生可见改动

在 XAML 里绑当前主题：

```xml
<DataTrigger Binding="{Binding Source={x:Static ui:ThemeManager.Current}, Path=CurrentTheme}" Value="Light">
```

### 2.2 语义色板

所有控件只引用下面这些**语义令牌**，不写死颜色 —— 换主题就是在换这一组值。Demo 的 Home 页把其中 9 个摆成了色板，`TextWarningBrush` 以文字示例展示。

| 令牌 | 浅色 | 深色 | 用途 |
|---|---|---|---|
| `PageBackgroundBrush` | `#F8F8F8` | `#1F1F1F` | 窗口 / 页面底 |
| `ControlBackgroundBrush` | `#FFFFFF` | `#2B2B2B` | 卡片 / 输入控件底 |
| `ControlSubtleBackgroundBrush` | `#F5F5F5` | `#252525` | 次级卡片底（Expander 内容区、ToolTip） |
| `ControlHoverBackgroundBrush` | `#EEEEEE` | `#363636` | 鼠标悬停 |
| `ControlSelectedBackgroundBrush` | `#E5E5E5` | `#404040` | 按下 / 选中 |
| `BorderBrush` | `#D3D3D3` | `#4A4A4A` | 描边 |
| `TextPrimaryBrush` | `#1F1F1F` | `#F5F5F5` | 主文字（标题、正文） |
| `TextSecondaryBrush` | `#6B6B6B` | `#B0B0B0` | 次文字（说明、辅助） |
| `TextWarningBrush` | `#C2410C` | `#FFA033` | 警告 / 危险提示文字 |
| `AccentBrush` | `#2563EB` | `#3B82F6` | 强调色 / Primary 按钮 |
| `AccentHoverBrush` | `#1D4ED8` | `#60A5FA` | 强调色悬停 |
| `AccentPressedBrush` | `#1E40AF` | `#2563EB` | 强调色按下 |
| `AccentForegroundBrush` | `#FFFFFF` | `#FFFFFF` | 强调色上的文字 |
| `ToggleSwitchThumbBrush` | `#FFFFFF` | `#E5E5E5` | 开关滑块 |
| `ProgressBackgroundBrush` | `#E0E0E0` | `#505050` | 进度条轨道 |
| `ScrollBackgroundBrush` | `#E0E0E0` | `#505050` | 滚动条滑块 |
| `SystemCloseHoverBrush` | `#D42B1C` | 同左 | 标题栏关闭键悬停 |
| `SystemClosePressedBrush` | `#BF2A1E` | 同左 | 标题栏关闭键按下 |
| `SystemCloseForegroundBrush` | `#FFFFFF` | 同左 | 标题栏关闭键前景 |
| `FlyoutShadowEffect` | DropShadow · Blur 10 · Dir 270 · Opacity 0.4 · `#DDDDDD` | 同参数 · `#000000` | 弹层阴影 |

`TextWarningBrush` 专门用于警告 / 危险提示的字色：浅色 `#C2410C` 在白底约 5.2:1，深色 `#FFA033` 在 `#1F1F1F` 上约 8.3:1，两边都满足正常文字的对比度要求。

弹层阴影整个 `DropShadowEffect` 放在主题里而不在模板里 —— 切主题时颜色、模糊、浓度可以各自调。深色主题必须用黑：浅灰投影在深底上会变成一圈发光边。

### 2.3 尺寸与形状令牌（`Theme/Size.xaml`）

与主题无关，两套主题共用同一份。

| 令牌 | 值 | 用途 |
|---|---|---|
| `ControlHeight` | `28` | 控件标准高度（按钮、输入框、列表项…） |
| `MenuItemHeight` | `26` | 菜单项高度 = `ControlHeight − 2 × ControlBorderThickness` |
| `ControlBorderThickness` | `Thickness 1` | 标准描边宽度（**必须是 Thickness**，拿到 boxed double 会抛"不是有效值"） |
| `ControlPaddingHorizontal` | `Thickness 12,0` | 按钮 / 输入框的横向内边距 |
| `FlyoutShadowRoom` | `Thickness 12` | 弹层阴影预留：presenter 模板拿它当 Border 的 `Margin`，偏移补偿也用同一个数 |
| `ScrollBarThickness` | `12` | 滚动条厚度 |
| `SeparatorThickness` | `1` | 分割线高度 |
| `TitleFontSize` | `22` | 页级标题字号（`TitleTextStyle`） |
| `HeaderFontSize` | `16` | 区块标题字号（`HeaderTextStyle`） |
| `ControlFontSize` | `13` | 控件标准字号 |
| `ControlGlyphFontSize` | `14` | `FontIcon` 的标准字号 |
| `TextSecondaryFontSize` | `12` | 次级文字字号（ToolTip 也用这个） |
| `CaptionGlyphFontSize` | `13` | 标题栏三颗系统按钮的图标字号 |
| `ControlCornerRadius` | `CornerRadius 3` | 控件圆角 |
| `OverlayCornerRadius` | `CornerRadius 4` | 弹层圆角（Flyout / MenuFlyout / ContextMenu） |
| `ContainerCornerRadius` | `CornerRadius 6` | 容器卡片圆角（Expander / TabControl / ListBox） |
| `DisabledOpacity` | `0.5` | 所有 `IsEnabled=False` 触发器共用 |
| `ChevronDownGeometry` | `M0,3 L5,7.5 L10,3` | V 形箭头几何，开口约 96°（DropDownButton / ComboBox / Expander / TreeView 共用） |

另有一个 `ExpanderHeaderHeight`（`50`）定义在 `styles/Expander.xaml` 里，不在 `Size.xaml`。

### 2.4 图标字体

`FontIcon` 用**系统自带**的图标字体，不随包分发。`Segoe Fluent Icons` 是微软字体，其内嵌许可明确写着 "Any other use is prohibited" —— 打进 NuGet 包属于再分发，有法律风险。

默认 `FontFamily` 是一条回退链：

```
Segoe Fluent Icons, Segoe MDL2 Assets
```

Win11 有前者、Win10 有后者，两代都覆盖。库和 Demo 用到的 19 个码点在**两个字体里都存在**（实测），回退不会出现豆腐块；两代之间只有个别字形的绘制细节不同。

想换成别的图标字体，覆盖 `FontIcon` 样式里的 `FontFamily` 即可。

### 2.5 排版档位

`styles/TextBlock.xaml` 里有四个**具名**文本样式，按「字号 + 字重 + 前景色」分层。

区别在于：隐式样式（不写 `Style` 时生效的那条）**刻意不设 `Foreground`**，让颜色从父级继承；下面这四个把颜色**钉死**。要跟随父级就用隐式样式，要锁死就用这四个。

| 样式 | 字号 | 字重 | 前景色 | 用在哪 |
|---|---|---|---|---|
| `TitleTextStyle` | `TitleFontSize`（22） | SemiBold | `TextPrimaryBrush` | 页级大标题 |
| `HeaderTextStyle` | `HeaderFontSize`（16） | Medium | `TextPrimaryBrush` | 区块标题 |
| `PrimaryTextStyle` | `ControlFontSize`（13） | Normal | `TextPrimaryBrush` | 正文 |
| `SecondaryTextStyle` | `TextSecondaryFontSize`（12） | Normal | `TextSecondaryBrush` | 说明、图注、脚注 |

```xml
<TextBlock Style="{StaticResource TitleTextStyle}" Text="页面标题" />
<TextBlock Style="{StaticResource SecondaryTextStyle}" Text="一行说明" />
```

四条都 `BasedOn` `DefaultTextStyle`，所以改 `DefaultTextStyle` 会同时影响这四档。

**两点注意：**

- **`PrimaryTextStyle` 的 13 是基准，不要单独抬。** 它等于 `ControlFontSize` —— 按钮、文本框、菜单里的字都是这个大小，正文跟控件同大界面才不自相矛盾。12 是最小档，也是 Windows 标准 UI 字号；中文再往下（11 及以下）笔画会糊。
- **`SemiBold` / `Medium` 对中文可能无效。** 微软雅黑只有 Regular 和 Bold 两档，600 / 500 会取最近的实有字重、很可能退成 Regular。中文标题用之前实测一下，不行就改 `Bold`。

## 三、无边框窗口

### 3.1 一行开起来

```xml
<Window
    x:Class="MyApp.Views.ShellView"
    xmlns:ui="http://schemas.lightui.com/2019"
    ui:FramelessHelper.IsFrameless="True"
    Title="ShellView">
```

`IsFrameless="True"` 就够了。**不用自己写 `Style`** —— 窗口没写 `Style` 时 `FramelessHelper` 会补上 `DefaultFramelessWindowStyle`；自己写了就尊重你的。

这套方案**不用 WPF 的 `WindowChrome`**，自己答 `WM_NCCALCSIZE` / `WM_NCHITTEST`、自己把窗口接进 DWM frame 通道。比 `WindowChrome` 多出来的东西：

- 不可见边框还在（`DWMWA_EXTENDED_FRAME_BOUNDS` 与 `GetWindowRect` 有差值），右边缘不丢 Win11 Snap 热区
- 启动不闪原生标题栏
- 标题栏右键能弹系统菜单

### 3.2 附加属性

| 附加属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `ui:FramelessHelper.IsFrameless` | `bool` | `false` | 挂上这套无边框逻辑，关掉就退回 WPF 原生行为 |
| `ui:FramelessHelper.CaptionHeight` | `double` | `30` | caption（标题栏）条带高度，单位 DIP。设成 `0` 或负数才回到"按系统度量算 |
| `ui:FramelessHelper.CloakUntilFirstFrame` | `bool` | `true` | 启动不闪：窗口先被 DWM 隐身（照常渲染、用户看不见），第一帧画完才现身。 |

`CaptionHeight` **只决定"多高算标题栏 / 拖动区"**，与系统按钮的高度**无关** —— 按钮尺寸归样式（见 3.4）。这是这一版特意做掉的解耦。

### 3.3 只读状态（给样式触发器用）

| 只读附加属性 | 类型 | 说明 |
|---|---|---|
| `ui:FramelessHelper.HoveredCaptionButton` | `CaptionButton` | 鼠标当前停在哪颗标题栏按钮上 |
| `ui:FramelessHelper.PressedCaptionButton` | `CaptionButton` | 鼠标当前按在哪颗标题栏按钮上 |

`CaptionButton` 枚举：`None` / `Minimize` / `Maximize` / `Restore` / `Close`。

**为什么需要它们**：窗口过程返回 `HTMINBUTTON` / `HTMAXBUTTON` / `HTCLOSE` 之后，那一块在 WPF 看来是**非客户区**，WPF 输入栈根本不参与，`Button.IsMouseOver` 恒为 `false`。悬停 / 按下只能由窗口过程判定后写进这两个属性，再由模板触发器消费。所以 **不要**在无边框窗口的标题栏按钮上用 `IsMouseOver` 触发器。

### 3.4 样式：`DefaultFramelessWindowStyle`

```xml
<Style x:Key="DefaultFramelessWindowStyle" TargetType="Window">
```

已注册为 `Window` 的隐式默认（`styles/Window.Frameless.xaml` 末尾那条 `<Style TargetType="{x:Type Window}" />`），要单独覆盖或显式引用时才用这个 key。

**模板部件**（在模板里替换 / 重排这些元素即可自定义标题栏按钮）：

| 部件名 | 类型 | 说明 |
|---|---|---|
| `PART_MinimizeButton` | `Button` | 最小化。`Command="{x:Static SystemCommands.MinimizeWindowCommand}"` |
| `PART_MaximizeButton` | `Button` | 最大化 ↔ 还原。`WindowState=Maximized` 触发器把命令换成 `RestoreWindowCommand`、字形换成 `&#xE923;` |
| `PART_CloseButton` | `Button` | 关闭。悬停 / 按下变红底白字 |
| `CaptionButtons` | `StackPanel` | 右上角那一整条按钮容器，`IsFrameless=True` 时才可见 |

**触发器行为**：

- `ResizeMode=NoResize` → 收起最小化与最大化；`CanMinimize` → 收起最大化。**必须由样式收起**：命中判定要求元素真的 `IsVisible=false` 才会跳过它
- 悬停 / 按下写在模板触发器里（以模板父级即这个 Window 为对象），而不是按钮样式里 —— 因为那两个附加属性正是写在窗口上的

### 3.5 标题栏按钮样式

`styles/Button.Frameless.xaml`：

| 资源 | 说明 |
|---|---|
| `FramelessCaptionButtonWidth` | `46` |
| `FramelessCaptionButtonHeight` | `30` |
| `FramelessSystemButtonStyle` | 最小化 / 最大化两颗按钮 |
| `FramelessCloseButtonStyle` | 关闭键，尺寸相同、只在配色上不同 |
| `ThemeButtonStyle` | 主题切换按钮，`BasedOn` 上面第一条 |
| `ThemeGlyphDark` / `ThemeGlyphLight` | 上面那条用的两个图标（`x:Shared="False"`，每次取值新造实例） |

`FramelessSystemButtonStyle` 和 `FramelessCloseButtonStyle` **刻意不 `BasedOn` 任何按钮样式** —— 跨字典的 `{StaticResource}` 在合并进来的子字典里搜不到，启动会抛"无法找到名为 … 的资源"，合并顺序也解决不了。所以它们自带全套 setter。

`ThemeButtonStyle` 是个例外：它 `BasedOn` 的就是**同文件**的 `FramelessSystemButtonStyle`，同字典解析，没有这个问题。

按钮尺寸是**样式自己的字面量**（46 × 30），与 `CaptionHeight` 无关；想让按钮填满 caption 就自己改这两个资源。

### 3.6 标题栏里放自己的控件

默认 caption 条带整条是拖动区。要放可交互控件（搜索框、主题切换按钮），给它加：

```xml
<TextBox WindowChrome.IsHitTestVisibleInChrome="True" />
```

这个附加属性在这里只当"逃生出口"用（**读**它不需要真的挂 `WindowChrome`）：标了的元素命中时返回 `HTCLIENT` 交给 WPF，没标的按 `HTCAPTION` 当拖动区。

系统按钮自己反而显式设成 `WindowChrome.IsHitTestVisibleInChrome="False"`，免得它们在"逃生出口"那一判里多嘴。

**主题切换按钮库里有现成的**，直接挂样式：

```xml
<Button
    WindowChrome.IsHitTestVisibleInChrome="True"
    Style="{StaticResource ThemeButtonStyle}"
    Click="OnToggleTheme" />
```

它 `BasedOn` `FramelessSystemButtonStyle`（尺寸、配色、非客户区的悬停与按下色都齐了），图标跟着 `ThemeManager.CurrentTheme` 自动在太阳 / 月亮之间切。

**它只负责显示图标，切主题要你自己在 `Click` 里调**：

```csharp
ThemeManager.ApplyTheme(
    ThemeManager.Current.CurrentTheme == ApplicationTheme.Light
        ? ApplicationTheme.Dark
        : ApplicationTheme.Light);
```

### 3.7 子窗口居中于父窗口

非模态 `IWindowManager.ShowWindowAsync` **不会**自动设 `Owner`，而 `WindowStartupLocation="CenterOwner"` 在 `Owner == null` 时会降级成 `Manual`。靠 Caliburn 的 settings 参数传进去：

```csharp
public void ShowFramelessDialog()
{
    var settings = new Dictionary<string, object>
    {
        ["Owner"] = Application.Current.MainWindow,
    };
    windowManager.ShowWindowAsync(new FramelessDialogViewModel(), null, settings);
}
```

### 3.8 与 `WindowChrome` 互斥

两套都要管 `WM_NCCALCSIZE` / `WM_NCHITTEST`，同一窗口只能选一套。窗口上已经挂了 `WindowChrome` 时 `FramelessHelper` 会**让位不挂**，只打一条 `Debug` 警告。

### 3.9 特性一览

- 拖动标题栏移动窗口、双击标题栏最大化 / 还原
- 8 个方向缩放（顶部有一条 5 DIP 的内部缩放带，两端 16 DIP 是角）
- 最大化时自动内缩，不会盖住任务栏
- 标题栏右键弹系统菜单
- 布局完成前命中会回落到 WPF 输入，那时按钮就是普通 WPF 按钮（`FramelessHelper` 注册了 `SystemCommands.*WindowCommand` 的 `CommandBinding` 兜底）

## 四、Button 页

### 4.1 `Button`

**样式**：隐式，基于 `DefaultButtonStyle`（`TargetType="ButtonBase"`），`RepeatButton` 共用同一份。

```xml
<Button Content="普通按钮" />
<Button Content="Primary" Style="{StaticResource AccentButtonStyle}" />
<Button Content="Disabled" IsEnabled="False" />
```

| 可调属性 | 默认 | 说明 |
|---|---|---|
| `Height` / `MinHeight` | `ControlHeight`(28) | 钉死盒子高 |
| `Padding` | `ControlPaddingHorizontal`(12,0) | 内容内边距 |
| `Background` | `ControlBackgroundBrush` | 底 |
| `Foreground` | `TextPrimaryBrush` | 字 |
| `BorderBrush` | `BorderBrush` | 描边 |
| `BorderThickness` | `ControlBorderThickness`(1) | 描边宽 |
| 圆角 | 模板里取 `ControlCornerRadius`(3) | `Button` 没有 `CornerRadius` 属性，要单独改只能换模板；想全局改就覆盖这个资源键 |
| `Cursor` | `Hand` | |

状态：悬停 → `ControlHoverBackgroundBrush`；按下 → `ControlSelectedBackgroundBrush`；禁用 → `Opacity = DisabledOpacity`。

### 4.2 `AccentButtonStyle`（Primary 按钮）

```xml
<Button Content="Primary" Style="{StaticResource AccentButtonStyle}" />
```

- 底 `AccentBrush`、字 `AccentForegroundBrush`、`BorderThickness=0`
- 悬停 `AccentHoverBrush`、按下 `AccentPressedBrush`
- 调 `ThemeManager.ApplyAccentColor(...)` 时整组一起变

### 4.3 `CircleButtonStyle`（圆形按钮）

```xml
<!-- 尺寸由内容撑：图标多大，圆多大 -->
<Button Style="{StaticResource CircleButtonStyle}">
    <ui:FontIcon Glyph="&#xE710;" />
</Button>

<!-- 想大一点：调 Padding，或给 FontIcon 加 Margin / FontSize -->
<Button Style="{StaticResource CircleButtonStyle}">
    <ui:FontIcon Margin="10" Glyph="&#xE713;" />
</Button>

<!-- 也可以直接写 Width / Height 定死 -->
<Button Width="40" Height="40" Style="{StaticResource CircleButtonStyle}" />
```

| 特性 | 说明 |
|---|---|
| 形状 | `Bd` 上 `CornerRadius="9999"` —— 超大圆角会被 WPF 缩到短边的一半，方形即整圆 |
| 边框 | 默认 `BorderBrush="Transparent"`，**悬停才显示** `BorderBrush`。边框宽度始终是 1px，所以悬停时布局不跳 |
| 尺寸 | `Height` 放开成 `NaN`、`MinHeight` 归 0，盒子交给内容 + `SquarePanel`。**不写尺寸时圆自动包住内容，宽高永远相等** |
| `Padding` | 默认 `4`，是圆和内边距之间那圈留白。内容大了正方形跟着大 |
| 外部定死尺寸 | 取短边做边长，居中留白 —— 宁可圆比盒子小，也不要溢出到相邻控件上 |

`SquarePanel`（`ui:SquarePanel`）是这套尺寸的核心：它把子元素摆进一个正方形，边长取子元素自然尺寸的**较大者**，子元素居中。XAML 里算不出 `max(宽, 高)`，只能由布局代码做。

### 4.4 `RepeatButton`

隐式样式，与 `Button` 完全一致（`RepeatButton` 派生自 `ButtonBase` 而非 `Button`，所以不能 `BasedOn` 一个 `TargetType="Button"` 的样式，两者各自 `BasedOn` `DefaultButtonStyle`）。

```xml
<RepeatButton Delay="200" Interval="80" cal:Message.Attach="Increase">
    <ui:FontIcon Glyph="&#xE710;" />
</RepeatButton>
```

### 4.5 `ToggleButton`

**隐式样式**，`styles/ToggleButton.xaml`。

```xml
<ToggleButton Content="未选中" />
<ToggleButton Content="已选中" IsChecked="True" />
```

选中态：底 `AccentBrush`、字 `AccentForegroundBrush`、边框刷成 `Transparent`（保留 1px 宽度，不影响按边框算的布局）。

> 注意：Expander 的头部、ComboBox 的下拉触发器都基于 `ToggleButton`，但它们在自己的模板里覆盖了 `ToggleButton.Template`，所以不受这条隐式样式影响。

### 4.6 `ui:HyperlinkButton`

超链接外观的按钮，点击后用系统 shell 打开 `NavigateUri`。

```xml
<ui:HyperlinkButton Content="访问 GitHub" NavigateUri="https://github.com" />
<ui:HyperlinkButton Content="不跳转（纯样式）" />
<ui:HyperlinkButton Content="执行动作" cal:Message.Attach="DoSomeAction" />
```

| 属性 | 类型 | 说明 |
|---|---|---|
| `NavigateUri` | `Uri` | 点击后 `Process.Start(..., UseShellExecute = true)`。**不设就是个普通按钮**，可以自己挂 `Click` |

**隐式样式**：底 `Transparent`、前景 `AccentBrush`、`BorderThickness=0`、`Padding` 4,0。悬停 → `Opacity` 0.85。打开失败不崩 UI，打一条 `Debug` 留痕。

## 五、Common Control 页

这一页全是 WPF 自带控件，库给它们套了隐式样式 —— **一律不用写 `Style=`**。

### 5.1 `CheckBox`

16 × 16 方框 + `Path` 勾。选中 → 框底 / 框边 `AccentBrush`、勾色 `AccentForegroundBrush`。内容在右侧，间距 `Margin="8,0,0,0"`。

### 5.2 `RadioButton`

16 × 16 圆 + 8 × 8 圆点。选中 → 圆点点 `AccentBrush`、外圈描边 `AccentBrush`。

### 5.3 `TextBox` / `PasswordBox`

```xml
<TextBox Width="200" ui:TextBoxHelper.Placeholder="普通文本" />
<PasswordBox Width="200" ui:TextBoxHelper.Placeholder="密码" />
```

| 项 | 说明 |
|---|---|
| 尺寸 | `TextBox` 有 `MinHeight` + `Height = ControlHeight`；`PasswordBox` 只设了 `MinHeight`，高度由外部布局决定 |
| `Padding` | `4,0` |
| `CaretBrush` | `TextPrimaryBrush` |
| `SelectionBrush` | `AccentBrush` |
| 聚焦 | 描边变 `AccentBrush` |
| 禁用 | `Opacity = DisabledOpacity` |
| 占位符 | `ui:TextBoxHelper.Placeholder`，模板里由 `PART_Placeholder` 承载 |
| 右键菜单 | **样式自带**一套剪切 / 复制 / 粘贴 / 全选菜单（带 `FontIcon` 图标与 `InputGestureText`），不用自己写 |

> `TextBoxHelper.Placeholder` 只对 `TextBox` / `PasswordBox` 生效，标到别的类型上只打一条 `Debug` 警告（回调里抛会毁掉整份 XAML 加载）。

### 5.4 `ComboBox`

```xml
<ComboBox Width="180" ItemsSource="{Binding FruitItems}" SelectedItem="{Binding SelectedFruit}" />
```

| 项 | 说明 |
|---|---|
| `MinHeight` | `ControlHeight` |
| `Padding` | `ControlPaddingHorizontal`(12,0) |
| `MaxDropDownHeight` | 下拉面板直接 `TemplateBinding` 到它 |
| 下拉面板 | `Placement=Bottom`、`MinWidth` 绑控件实际宽、`Margin="0,4,0,0"`、圆角 `ControlCornerRadius` |
| 右侧箭头 | `ChevronDownGeometry` 共用几何，`Margin="0,0,10,0"` |
| 悬停 | 触发器底变 `ControlHoverBackgroundBrush` |
| 聚焦 | 描边变 `AccentBrush` |

**`ComboBoxItem`** 也有隐式样式：`MinHeight`/`Height` = `ControlHeight`、悬停与 `IsHighlighted` → `ControlHoverBackgroundBrush`、选中 → `ControlSelectedBackgroundBrush`。

> ⚠ **不支持 `IsEditable` 模式**（Demo 未用到，需要时再扩展）。

### 5.5 `ListBox` / `ListBoxItem`

| 项 | 说明 |
|---|---|
| 容器圆角 | `ContainerCornerRadius`(6)，写在外层 `Border` 上（`ListBox` 没有 `CornerRadius` 属性，只能换模板） |
| `Padding` | 同时喂给外层 `Border` 的 `Margin` 与内部 `ScrollViewer` |
| `ListBoxItem` | `MinHeight` = `ControlHeight`、`Padding` 8,0、`HorizontalContentAlignment=Stretch` |
| 状态 | 悬停 `ControlHoverBackgroundBrush`、选中 `ControlSelectedBackgroundBrush` |

### 5.6 `ProgressBar`

原生 `ProgressBar` 的隐式样式只是个**壳**：模板根是 LightUi 的自定义 `ui:ProgressBar`（继承 `RangeBase`），属性经 `TemplateBinding` 透传，可视逻辑全在 `controls/ProgressBar/`。直接写 `<ProgressBar />` 或 `<ui:ProgressBar />` 都行。

```xml
<ProgressBar Value="65" />
<ProgressBar IsIndeterminate="True" />
```

| 属性 | 默认 | 说明 |
|---|---|---|
| `Value` / `Minimum` / `Maximum` | `0` / `0` / `100` | 标准范围 |
| `IsIndeterminate` | `false` | 不确定态，忽略 `Value`，显示连续流动动画 |
| `CornerRadius` | `ControlCornerRadius`(3) | **本控件自己加的 DP**（原生 `ProgressBar` 没有），模板显示与代码裁剪几何共用 |
| `Foreground` | `AccentBrush` | 指示块颜色 |
| `Background` | `ProgressBackgroundBrush` | 轨道颜色 |
| `MinHeight` | `6` | |

- **确定态**：`Value` 变化时指示块从旧位置"追赶"滑到新位置（0.367s，KeySpline `0.1,0.9,0.2,1.0`）
- **不确定态**：两条指示块接力（1 号 0→1.5s 滑完；2 号先等 0.75s 再用 1.25s 滑完），2s 一个周期
- 指示块显隐靠**宽度归零**而不是 `Opacity`；动画在代码里用 `BeginAnimation` 驱动（位移量依赖 `ActualWidth`，XAML 关键帧的绑定只在故事板启动时求值一次，那时还是 0）

### 5.7 `ToolTip`

```xml
<Button
    Content="tooltip 左"
    ToolTip="这是左边的 tooltip"
    ToolTipService.Placement="Left"
    ToolTipService.HorizontalOffset="-6" />
```

隐式样式：底 `ControlSubtleBackgroundBrush`、圆角 `ControlCornerRadius`、`Padding` 8,4、字号 `TextSecondaryFontSize`(12)。位置用 `ToolTipService.Placement` / `HorizontalOffset` / `VerticalOffset` 调。

> 覆盖模板会失去 WPF 默认的 `DropShadow` —— 想要就自己在 `Border` 上加 `DropShadowEffect`。

### 5.8 `TabControl` / `TabItem`

```xml
<TabControl BorderThickness="0">
    <TabItem Header="选项 A">…</TabItem>
    <TabItem Header="选项 B" IsSelected="True">…</TabItem>
    <TabItem Header="Disabled" IsEnabled="False">…</TabItem>
</TabControl>
```

| 项 | 说明 |
|---|---|
| 容器 | `ContainerCornerRadius`(6)；`Padding` 默认 0，同时喂给外层 `Border` 的 `Margin` 与内层 `DockPanel` |
| 标题条 | `TabPanel` 放在一个带描边圆角的 `Border` 里 |
| `TabItem` | `MinHeight`/`Height` = `ControlHeight`、`Padding` = `ControlPaddingHorizontal`、选中 `ControlSelectedBackgroundBrush`、悬停 `ControlHoverBackgroundBrush` |
| `Cursor=Hand` | 设在模板的 `Bd` 上而不是 `TabItem` 上 —— 设在 `TabItem` 上会顺着属性继承把整个内容区也变成手形 |

> ⚠ **只支持 `TabStripPlacement=Top`**，其它方向会渲染异常（模板是 `DockPanel.Dock="Top"`）。

### 5.9 `ScrollBar` / `ScrollViewer`

| 项 | 说明 |
|---|---|
| 厚度 | `ScrollBarThickness`(12)，垂直取 `Width`/`MinWidth`，水平由 `Orientation=Horizontal` 触发器换成 `Height`/`MinHeight` |
| 滑块 | `Thumb`，`Margin=2`、圆角 `ControlCornerRadius`、色 `ScrollBackgroundBrush` |
| 无箭头 | 两个 `RepeatButton` 用 `DefaultScrollBarPageButton` 样式做成全透明矩形（点轨道翻页仍然有效） |
| `ScrollViewer` | 底 `Transparent`、前景 `TextPrimaryBrush` |

### 5.10 `Menu` / `MenuItem` / `Separator` / `ContextMenu`

```xml
<Menu>
    <MenuItem Header="文件">
        <MenuItem Header="打开" InputGestureText="Ctrl+O" />
        <Separator />
        <MenuItem Header="退出" />
    </MenuItem>
</Menu>
```

**`MenuItem`** 模板里有四个列：`Icon` / `Header` / `InputGestureText`（右侧提示文字）/ 子菜单箭头。

| 项 | 说明 |
|---|---|
| `MinHeight` / `Height` | `MenuItemHeight`(26)，比 `ControlHeight` 小 2 —— 外层还裹着菜单的 1px 边框 |
| `Icon` | 放在第一列的 `ContentPresenter`，右边距 8 |
| `InputGestureText` | 右侧那列提示文字，`FontSize` = `TextSecondaryFontSize` |
| `Role=SubmenuHeader` | 显示右侧箭头，并把 `PART_Popup` 的 `Placement` 从 `Bottom` 改成 `Right` |
| `OverridesDefaultStyle=True` | 绕过 WPF 主题里那条会失败绑定的 `HorizontalContentAlignment` setter |

**`PART_Popup` 必须叫这个名字** —— `MenuItem.OnApplyTemplate` 靠 `GetTemplateChild` 取它来展开子菜单，改名后所有嵌套菜单永远打不开，且不报错。

**子菜单弹层**也带阴影：`Popup` 的直接子元素 `Border` 上有 `Margin="{DynamicResource FlyoutShadowRoom}"` + `Effect="{DynamicResource FlyoutShadowEffect}"`。少一个都不行 —— `Popup` 的窗口尺寸 = 子元素的 `DesiredSize`（含 `Margin`），少了 `Margin` 阴影会被弹窗边界裁掉。

**`Separator`**：

| 资源键 | 说明 |
|---|---|
| `DefaultSeparatorStyle` | 本体：高 `SeparatorThickness`、`Margin` 0,4、`SnapsToDevicePixels` + `UseLayoutRounding` |
| `{x:Static MenuItem.SeparatorStyleKey}` | **菜单里的分割线必须挂在这个键上** —— `Menu` / `MenuItem` 会给每个 `Separator` 显式套上这个键的样式，而显式样式优先于隐式样式。只写隐式样式的话，`ContextMenu` / `MenuFlyout` / `Menu` 里的分割线会全部落回默认主题那条 |

**`ContextMenu`**（隐式样式）：

| 项 | 说明 |
|---|---|
| 圆角 | `OverlayCornerRadius`(4) |
| 阴影 | `FlyoutShadowEffect` |
| 阴影预留 | `Padding` = `FlyoutShadowRoom`(12)，模板把它当 `Border` 的 `Margin` |
| 偏移补偿 | `ui:ContextMenuShadow.FixOffsets="True"`，见 [7.5](#75-uicontextmenushadow附加属性) |

### 5.11 `TreeView` / `TreeViewItem`

VS Code 资源管理器风格的文件树：行高紧凑、每级**只缩进内容**，hover / 选中的高亮从控件最左边铺满整行。

```xml
<TreeView ItemsSource="{Binding FileTree}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <ui:SimpleStackPanel Orientation="Horizontal" Spacing="6">
                <ui:FontIcon Glyph="{Binding Glyph}" />
                <TextBlock VerticalAlignment="Center" Text="{Binding Name}" />
            </ui:SimpleStackPanel>
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

| 项 | 说明 |
|---|---|
| 行高 | `ui:TreeViewHelper.ItemHeight`，默认 `28`；继承到每个 `TreeViewItem` |
| 缩进 | `ui:TreeViewHelper.Indent`，默认 `8`；只缩进内容，**行本身保持全宽** |
| 展开状态 | 隐式样式默认把 `TreeViewItem.IsExpanded` 双向绑到数据项的 `IsExpanded`，不用写 `ItemContainerStyle` |
| 回车 | 切换当前行的展开 / 收起；`ui:TreeViewHelper.ToggleExpandedOnEnter`（默认开，写 `False` 关闭），叶子不响应 |
| 焦点 | `TreeView` / `TreeViewItem` 都不画虚线焦点框（`FocusVisualStyle="{x:Null}"`） |
| 选中 | `ControlSelectedBackgroundBrush`，与 `ListBox` 一致 |
| 悬停 | `ControlHoverBackgroundBrush`，触发器用 `SourceName="Bd"` 只看本行（`TreeViewItem.IsMouseOver` 在子项上恒为 `True`） |
| 展开箭头 | 共享 `ChevronDownGeometry`：收起旋转 `-90°`，展开 `0°`；叶子不显示箭头，但保留 16px 列保证同级文字对齐 |
| 全宽高亮 | 子项容器不右移，缩进由 `TreeViewIndentConverter` 按层级算到行内容上 |

```xml
<!-- 行高 / 缩进都在 TreeView 上设置一次即可 -->
<TreeView ui:TreeViewHelper.ItemHeight="30" ui:TreeViewHelper.Indent="16" />

<!-- 不需要「回车切换展开」时 -->
<TreeView ui:TreeViewHelper.ToggleExpandedOnEnter="False" />
```

> 数据项**没有** `IsExpanded` 属性时，默认绑定会失败（调试输出里有一条 binding 错误，不影响运行），树保持收起；不想要这条默认行为可以覆盖 `ItemContainerStyle`。

### 5.12 `DataGrid`

与 `ListBox` / `TreeView` 同一套扁平风格：圆角卡片、行高 28、行间 1px 分隔线。

```xml
<DataGrid
    AutoGenerateColumns="False"
    IsReadOnly="True"
    ItemsSource="{Binding Rows}">
    <DataGrid.Columns>
        <DataGridTextColumn Width="*" Binding="{Binding Name}" Header="名称" />
        <DataGridTextColumn Width="100" Binding="{Binding Kind}" Header="类型" />
        <DataGridTextColumn Width="100" Binding="{Binding Size}" Header="大小" />
    </DataGrid.Columns>
</DataGrid>
```

| 项 | 说明 |
|---|---|
| 容器 | 圆角 `ContainerCornerRadius` + 1px 描边 |
| 行高 / 列头高 | 都是 `ControlHeight`（28） |
| 分隔线 | `GridLinesVisibility="None"`，改由**行模板的 1px 上边框**画 —— DataGrid 自带网格线不吸附像素，125% 缩放下会被抗锯齿摊成 2px |
| 行状态 | 悬停 `ControlHoverBackgroundBrush`、选中 `ControlSelectedBackgroundBrush`，与 `ListBox` 一致 |
| 列头 | 透明底、`TextSecondaryBrush` 小字（12px）、无底边（第一行的上边框充当分隔线） |
| 排序 | 点击列头排序，`SortDirection` 用共享的 `ChevronDownGeometry`：升序箭头朝上、降序朝下 |
| 列宽 | 列头两侧的透明 `Thumb`（`PART_LeftHeaderGripper` / `PART_RightHeaderGripper`）支持拖拽改宽 |
| 行头 | 默认 `HeadersVisibility=Column` 不显示；改成 `All` / `Row` 会出现，但**行头本身没有做样式** |

> 模板部件名不能改：`DG_ScrollViewer`、`PART_ColumnHeadersPresenter`、`PART_ScrollContentPresenter`、`PART_VerticalScrollBar`、`PART_HorizontalScrollBar`、`DGR_CellsPresenter`、`DGR_DetailsPresenter`、`DGR_RowHeaderPresenter`。

### 5.13 `ListView`（GridView）

与 `ListBox` / `DataGrid` 同一套扁平风格。`ListView` / `ListViewItem` / `GridViewColumnHeader` 都不是 `ListBox` / `ListBoxItem` 的精确类型，所以库里为它们单独定义了隐式样式。

```xml
<ListView Height="160" ItemsSource="{Binding Rows}">
    <ListView.View>
        <GridView>
            <GridViewColumn Width="260" DisplayMemberBinding="{Binding Name}" Header="名称" />
            <GridViewColumn Width="120" DisplayMemberBinding="{Binding Kind}" Header="类型" />
            <GridViewColumn Width="120" DisplayMemberBinding="{Binding Size}" Header="大小" />
        </GridView>
    </ListView.View>
</ListView>
```

| 项 | 说明 |
|---|---|
| 容器 | 圆角 `ContainerCornerRadius` + 1px 描边 |
| 行 | 无分割线；`Margin="0"`（覆盖系统主题默认的 `0,0,0,1`，否则会有 1px 白缝）；hover / 选中与 `ListBox` 一致；3px 圆角 |
| 行内容左缩进 | `Padding="2,0"` —— `GridViewRowPresenter` 会给单元格内容额外加约 6 DIP，2 + 6 = 8，与 `ListBoxItem` 的 8 对齐 |
| 表头 | 透明底、12px 次要色；`Padding="8.5,0"` + `Margin="-1.5,0,0,0"` 抵消系统 GridView 表头行约 1.6 DIP 的内缩，避免左侧白缝并保持文字与单元格对齐 |
| 滚动 | 仍使用系统 `GridViewScrollViewerStyleKey`（列头与内容的横向同步由它负责） |
| 列宽 | 列头两侧的透明 `Thumb`（`PART_LeftHeaderGripper` / `PART_RightHeaderGripper`）支持拖拽改宽 |

> 样式按 **GridView** 用法适配；无 `View` 的纯 ListView 没有专门处理。

## 六、Extended Control 页

### 6.1 `ui:ToggleSwitch`

开关。**继承 `ToggleButton`**，On / Off 两态，不做拖动动画。

```xml
<ui:ToggleSwitch OffContent="关闭" OnContent="打开" />
<ui:ToggleSwitch IsChecked="True" OffContent="关闭" OnContent="打开" />
<ui:ToggleSwitch IsChecked="True" IsEnabled="False" OffContent="关闭" OnContent="打开" />
```

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `OnContent` | `object` | `null` | 选中时显示的**内容**（不只是文字，任意对象都行） |
| `OffContent` | `object` | `null` | 未选中时显示的内容 |
| `IsChecked` | `bool?` | — | 继承自 `ToggleButton`，可直接双向绑定 |

**隐式样式**：轨道 36 × 18、圆角 9、滑块 14 × 14、滑块色 `ToggleSwitchThumbBrush`。选中 → 轨道 `AccentBrush`、滑块靠右（`HorizontalAlignment` 切 `Right` + `Margin` 切 `0,0,2,0`）。两个 `ContentPresenter` 靠 `Visibility` 互斥切换，间距 `Margin="6,0,2,0"`。

### 6.2 `ui:FontIcon`

图标字体，**继承 `TextBlock`**。

```xml
<ui:FontIcon Glyph="&#xE710;" />
<ui:FontIcon FontSize="18" Foreground="{DynamicResource AccentBrush}" Glyph="&#xE74D;" />
```

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Glyph` | `string` | `""` | 字形，就是字体里的一个私有区码点 |
| `FontFamily` | — | `Segoe Fluent Icons, Segoe MDL2 Assets` | 系统字体回退链，不随包分发 |
| `FontSize` | — | `ControlGlyphFontSize`(14) | |
| `Foreground` | — | **不设**，跟随父级 | 默认样式刻意不写 `Foreground`：写了具体 brush 就成了 local value，会盖掉继承（比如 `TabItem` 选中态传不下来） |
| 对齐 | — | 水平垂直都居中 | `VerticalAlignment` / `HorizontalAlignment` 都是 `Center` |

字体取系统自带的：Win11 走 `Segoe Fluent Icons`，Win10 回退到 `Segoe MDL2 Assets`。用到的码点两代都有，不会出豆腐块。

库和 Demo 里用到的码点（名称以 Demo 自己标的为准）：

| 码点 | 名称 / 用处 |
|---|---|
| `E710` | Add（加号） |
| `E74D` | Delete（删除） |
| `E713` | Settings（设置） |
| `E70D` | Chevron Down（下箭头） |
| `E10A` | Cancel（取消） |
| `E711` | 标题栏关闭键 |
| `E71A` | 标题栏最大化键 |
| `E923` | 标题栏还原键 |
| `E738` | 减号，也用作标题栏最小化键 |
| `E721` | 搜索（圆形按钮示例） |
| `E700` | 列表 / 菜单类图标（`TabItem` 与 `Expander` 的标题里） |
| `E706` / `E708` | 主题切换按钮的两种状态（`ThemeButtonStyle` 按当前主题二选一） |
| `E8C6` / `E8C8` / `E77F` / `E8B3` | 剪切 / 复制 / 粘贴 / 全选（`TextBox` 右键菜单） |
| `E8A5` / `E7C3` | 新建文件 / 新建项目（`DropDownButton` 示例） |

想知道某个码点长什么样、或者找别的图标，用系统自带的字符映射表（`charmap.exe`）选字体 **Segoe Fluent Icons** 查（Win10 上选 **Segoe MDL2 Assets**）。

### 6.3 `ui:SimpleStackPanel`

比原生 `StackPanel` 多一个 `Spacing` 的最小面板：只做堆叠 + 等距。

```xml
<ui:SimpleStackPanel Spacing="12">
    <Button Content="A" />
    <Button Content="B" />
</ui:SimpleStackPanel>

<ui:SimpleStackPanel Orientation="Horizontal" Spacing="8">…</ui:SimpleStackPanel>
```

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Orientation` | `Orientation` | `Vertical` | 堆叠方向 |
| `Spacing` | `double` | `0` | 相邻元素之间的间距 |

行为细节：

- **间距按可见性算**，不按下标 —— `Collapsed` 的元素不占位、也不产生间距
- 堆叠轴会**放开成无限再量子元素**：否则会被调用方的有限约束压扁、拿不到自然尺寸
- 交叉轴取 `max(分配值, 子元素自然尺寸)`：子元素更大时让它溢出，而不是硬压到 `finalSize`
- 横向堆叠时子元素会被安排到面板的**全高**（这正是 `CircleButtonStyle` 需要 `SquarePanel` 的原因）

### 6.4 `ui:SquarePanel`

把子元素摆进一个正方形。

```xml
<ui:SquarePanel>
    <Border CornerRadius="9999">内容</Border>
</ui:SquarePanel>
```

- 边长取子元素**自然尺寸的较大者**，子元素在正方形里居中
- 测量时对子元素放开约束（拿父级约束过的尺寸当边长，圆会忽大忽小）
- 尺寸被外部定死时取**短边**做边长
- 本身没有可配属性，主要供 `CircleButtonStyle` 使用

## 七、Flyout Control 页

### 7.1 `ui:DropDownButton`

点击后弹出附带 `Flyout` 的按钮。**继承 `Button`**。

```xml
<ui:DropDownButton Content="新建">
    <ui:DropDownButton.Flyout>
        <ui:MenuFlyout>
            <MenuItem Header="新建文件" />
            <MenuItem Header="新建项目" />
            <Separator />
            <MenuItem Header="从模板新建…" />
        </ui:MenuFlyout>
    </ui:DropDownButton.Flyout>
</ui:DropDownButton>
```

| 属性 | 类型 | 说明 |
|---|---|---|
| `Flyout` | `FlyoutBase` | 内容属性，可以放 `ui:Flyout` 或 `ui:MenuFlyout` |

**隐式样式**：`MinHeight` = `ControlHeight`、`Padding` = `ControlPaddingHorizontal`、`HorizontalContentAlignment=Left`、右侧 `ChevronDownGeometry` 箭头（`Margin="8,0,10,0"`）。悬停 / 按下 / 禁用与普通 `Button` 一致。

> 点击时会把 `Flyout.Placement` **强制成 `Bottom`** 再 `ShowAt(this)`。所以想用别的方向就别用 `DropDownButton`，改用 `Button` + `ui:FlyoutService.Flyout`。

### 7.2 `ui:Flyout`

轻量弹出层，承载任意内容。

```xml
<Button Content="向上弹">
    <ui:FlyoutService.Flyout>
        <ui:Flyout Placement="Top">
            <TextBlock Margin="10" Text="Placement = Top" />
        </ui:Flyout>
    </ui:FlyoutService.Flyout>
</Button>

<!-- 内容里的按钮可以用 CloseCommand 关掉弹层 -->
<Button Content="关闭弹出" Command="{x:Static ui:FlyoutBase.CloseCommand}" />
```

| 属性 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `Content` | `object` | `null` | 内容属性（XAML 里直接写子元素） |
| `Placement` | `FlyoutPlacementMode` | `Bottom` | `Top` / `Bottom` / `Left` / `Right` |
| `VerticalOffset` | `double` | `0` | 额外的纵向偏移（DIP，正 = 往下），与 `Placement` 无关 |
| `HorizontalOffset` | `double` | `0` | 额外的横向偏移（DIP，正 = 往右） |
| `IsOpen` | `bool` | `false` | **可双向绑定**，可以在 ViewModel 里控制开合 |

| 成员 | 说明 |
|---|---|
| `ShowAt(FrameworkElement anchor)` | 在指定控件旁弹出 |
| `Hide()` | 关闭 |
| `FlyoutBase.CloseCommand` | 静态 `RoutedCommand`，给**弹层内容里**的按钮用。内容里拿不到 `Flyout` 实例（presenter 与 `Popup` 都是代码 new 的、不在逻辑树上），所以命令绑定挂在 presenter 上，按钮的路由会一路冒到它 —— 不用 code-behind、也不用 `x:Name` |

行为：

- **与宿主之间固定留 2 DIP 缝**（`PlacementGap`）
- **Esc 关闭**：Esc 是发给焦点元素的，弹层自己收不到键盘事件（它的 `Popup` 只捕获鼠标），所以钩在锚点所在的窗口上，打开时挂、关掉时摘
- **`DataContext` 从 anchor 接**：`Flyout` 只是附加属性的值、不在元素树上，继承不过来，`ShowAt` 时从 anchor 建一个绑定接过去（只在没设过的时候接）
- **点外部关闭**（`StaysOpen = false`）

**样式 `DefaultFlyoutPresenterStyle`**（`TargetType="ContentControl"`）：底 `ControlBackgroundBrush`、圆角 `OverlayCornerRadius`(4)、阴影 `FlyoutShadowEffect`。这里的 `Padding` **专指"给阴影留的空间"**（模板把它当 `Border` 的 `Margin` 用），内容的内边距由使用者自己在内容根上设。

### 7.3 `ui:MenuFlyout`

弹出菜单，条目是 `MenuItem` 风格。

```xml
<Button Content="普通 Button 挂 MenuFlyout">
    <ui:FlyoutService.Flyout>
        <ui:MenuFlyout>
            <MenuItem Header="选项 1" />
            <MenuItem Header="选项 2" />
            <Separator />
            <MenuItem Header="更多…">
                <MenuItem Header="二级选项 1" />
                <MenuItem Header="二级选项 2" />
            </MenuItem>
        </ui:MenuFlyout>
    </ui:FlyoutService.Flyout>
</Button>
```

| 属性 | 类型 | 说明 |
|---|---|---|
| `Items` | `List<object>` | 内容属性，XAML 里直写的子条目（`MenuItem` / `Separator`） |
| `ItemsSource` | `IEnumerable` | 数据绑定用；设了就优先于 `Items` |

`Placement` / `VerticalOffset` / `HorizontalOffset` / `IsOpen` / `ShowAt` / `Hide` 与 `ui:Flyout` 完全相同（都来自 `FlyoutBase`）。

**二级菜单**：在 `MenuItem` 里再嵌一层 `MenuItem` 就自动成为 `SubmenuHeader` —— 样式里 `Role=SubmenuHeader` 那条触发器负责显示右侧箭头、并把 `PART_Popup` 改成向右展开。

宿主是 `MenuFlyoutPresenter`（继承 `ContextMenu`），借 WPF 原生 `MenuBase` 接管"点条目自动关闭 + LightDismiss + 方向键导航与高亮"。**样式 `DefaultMenuFlyoutPresenterStyle`**：`MinWidth` 160、圆角 `OverlayCornerRadius`、阴影、`Padding` 同样是阴影预留，内边距 4 写在模板里。

### 7.4 `ui:FlyoutService`（附加属性）

给**任意 `ButtonBase`** 挂 `Flyout` 的附着属性：点击按钮即弹出。

```xml
<Button Content="点我弹出">
    <ui:FlyoutService.Flyout>
        <ui:MenuFlyout>…</ui:MenuFlyout>
    </ui:FlyoutService.Flyout>
</Button>
```

| 附加属性 | 类型 | 说明 |
|---|---|---|
| `ui:FlyoutService.Flyout` | `FlyoutBase` | 挂上后按钮的 `Click` 会 `flyout.ShowAt(button)` |

与 `DropDownButton` 的区别：**不改 `Placement`**，所以在 `Flyout` 上写的方向生效。换 `Flyout` 时会先摘掉旧 handler 再挂新的。


### 7.5 关于 `TextBox` 自带的右键菜单

`TextBox` / `PasswordBox` 的隐式样式**自带**一套剪切 / 复制 / 粘贴 / 全选菜单，吃的是 LightUi 的 `ContextMenu` 样式（圆角 / 阴影 / 偏移补偿）。

但**不挂自己的菜单时**，WPF 会弹一个内部类型（`TextEditorContextMenu+EditorContextMenu`，条目是 `EditorMenuItem`）。隐式样式按精确类型匹配，命不中这些内部派生类型 —— 它们既不吃 `TargetType="ContextMenu"` 也不吃 `TargetType="MenuItem"` 的样式，外观是系统默认的。要统一外观就自己挂一个 `ContextMenu`（见 Demo 的 Flyout 页）。

## 八、Helper 与附加属性汇总

| 名称 | 宿主类型 | 成员 | 作用 |
|---|---|---|---|
| `ui:TextBoxHelper` | `TextBox` / `PasswordBox` | `Placeholder` (`string`) | 占位符文字，内容为空时显示 |
| | | `HasText` (只读 `bool`) | 控件里当前有没有内容，**模板消费**（`PART_Placeholder` 靠它收起） |
| `ui:FlyoutService` | 任意 `ButtonBase` | `Flyout` (`FlyoutBase`) | 点击按钮弹出 Flyout / MenuFlyout |
| `ui:TreeViewHelper` | `TreeView` | `ItemHeight` (`double`) | 每行高度（默认 `28`），继承到每个 `TreeViewItem` |
| | | `Indent` (`double`) | 每级内容缩进（默认 `8`），只影响内容、不影响行宽 |
| | | `ToggleExpandedOnEnter` (`bool`) | 回车切换当前行展开状态（默认开；写 `False` 关闭） |
| `ui:FramelessHelper` | `Window` | `IsFrameless` (`bool`) | 挂上无边框逻辑 |
| | | `CaptionHeight` (`double`) | 标题栏条带高度（DIP），`0` = 按系统度量算 |
| | | `CloakUntilFirstFrame` (`bool`) | 启动不闪 |
| | | `HoveredCaptionButton` (只读) | 鼠标停在哪颗标题栏按钮上 |
| | | `PressedCaptionButton` (只读) | 鼠标按在哪颗标题栏按钮上 |
| `ThemeManager` | 静态类 | `ApplyTheme` / `ApplyAccentColor` / `ResetAccentColor` | 切主题、换强调色 |
| | | `Current.CurrentTheme` (只读 DP) | 当前主题，可绑定 |
| | | `CurrentThemeChanged` | 切主题完成事件 |
| `ui:FlyoutBase` | 静态成员 | `CloseCommand` | 关闭弹层的 `RoutedCommand`，给弹层内容里的按钮用 |

**自定义控件一览**：

| 控件 | 基类 | 自定义属性 |
|---|---|---|
| `ui:Flyout` | `FlyoutBase` : `FrameworkElement` | `Content` |
| `ui:MenuFlyout` | `FlyoutBase` : `FrameworkElement` | `Items`, `ItemsSource` |
| `ui:DropDownButton` | `Button` | `Flyout` |
| `ui:HyperlinkButton` | `Button` | `NavigateUri` |
| `ui:ToggleSwitch` | `ToggleButton` | `OnContent`, `OffContent` |
| `ui:FontIcon` | `TextBlock` | `Glyph` |
| `ui:ProgressBar` | `RangeBase` | `IsIndeterminate`, `CornerRadius`（+ 继承来的 `Value`/`Minimum`/`Maximum`） |
| `ui:SimpleStackPanel` | `Panel` | `Orientation`, `Spacing` |
| `ui:SquarePanel` | `Decorator` | — |
| `ui:MenuFlyoutPresenter` | `ContextMenu` | — （`MenuFlyout` 内部创建，一般不直接用） |

## 九、已知限制与注意事项

1. **无边框方案与 `WindowChrome` 互斥** —— 同一窗口只能选一套，已挂 `WindowChrome` 时 `FramelessHelper` 会让位不挂。
2. **隐式样式按精确类型匹配**，命不中派生类。XAML 声明的 `Window` 都是派生类；`TextBox` 内部右键菜单也是内部派生类型，同样吃不到样式。
3. **菜单里的 `Separator` 必须挂 `{x:Static MenuItem.SeparatorStyleKey}`** —— 隐式样式对它无效。
4. **`ComboBox` 不支持 `IsEditable`**。
5. **`Expander` 只支持 `ExpandDirection=Down`**、**`TabControl` 只支持 `TabStripPlacement=Top`**，其它方向模板会渲染异常。
6. **`ToolTip` 模板覆盖后没有默认阴影**。
7. **不监听系统主题**：`ThemeManager` 只在被显式调用时切换，不跟随 `SystemParameters`，也不探测高对比度（`ApplicationTheme.HighContrast` 目前是占位，映射到 Light）。
8. **DPI 感知是宿主 exe 的责任**，库里只能探测并打警告。
9. **`TreeView` 默认约定数据项带 `IsExpanded` 属性** —— 隐式样式里做了双向绑定；没有这个属性时绑定会失败（不影响运行，树保持收起），可以覆盖 `ItemContainerStyle` 去掉它。
10. **`DataGrid` 的行头没有做样式** —— 默认 `HeadersVisibility=Column` 不显示；改成 `All` / `Row` 时行头是系统默认外观。另外 DataGrid 模板是自绘的，部件名改动会让列头 / 单元格 / 行详情失效。
11. **`ListView` 样式按 GridView 适配** —— 无 `View` 的纯 ListView 未专门处理；列头 / 横向同步仍由系统 `GridViewScrollViewerStyleKey` 提供，如果那里也需要去掉右下角小方块，需要单独覆盖该 key。

