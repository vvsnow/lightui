# XamlCheck

把 `LightUi` 里每个控件都实例化一遍，逼它装载 `ControlTemplate`，弹层类（`Flyout` / `MenuFlyout`）真打开一次。

## 为什么需要它

**控件模板是按需装载的。** 只有某个控件第一次出现在界面上时，它的 `ControlTemplate`（BAML）才会被解析并赋值。所以「进程还活着 / 主窗口出来了」这类冒烟测试覆盖不到大部分模板 —— 模板里一旦有非法值，表现是**启动完全正常，点进某个页面才崩**。

真实案例：`Theme/Size.xaml` 里把尺寸令牌 `ControlBorderThickness` 声明成了 `sys:Double`，而它被用在 14 处 `BorderThickness`（`Thickness` 属性）上。启动毫无异常，进「通用控件」页才抛：

```
System.InvalidOperationException: "1" 不是属性 "BorderThickness" 的有效值。
```

**动过样式 / 尺寸令牌 / 模板之后，跑一次这个。**

## 用法

```
dotnet run --project tools/XamlCheck/XamlCheck.csproj
```

退出码：`0` 全通过 ／ `1` 有模板装载失败 ／ `2` 资源字典没加载上（此时后面所有检查都是假绿，已提前中止）。

因为它以 `ProjectReference` 引 `LightUi`，跑之前不需要先单独 build 库。

## 它覆盖什么、不覆盖什么

- **覆盖**：所有带 LightUi 样式的控件模板装载 —— 包括尺寸/画刷令牌的类型是否和属性类型匹配（模板装载时会真正赋值）、`{x:Static}` 是否解析得到、模板里的键名有没有写错。
- **不覆盖**：布局与视觉结果（宽高、间距、颜色对不对）、交互行为。这些仍然要靠肉眼看 Demo。
- ~3 秒跑完，可以直接挂进 CI。
