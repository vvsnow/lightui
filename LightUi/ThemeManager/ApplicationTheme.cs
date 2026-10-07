namespace LightUi;

/// <summary>
/// 应用主题枚举（Light / Dark / HighContrast）。最小版只支持 Light / Dark，
/// HighContrast 保留为占位，暂未实现自动探测（见 ThemeManager.cs 注释）。
/// </summary>
public enum ApplicationTheme
{
    Light,
    Dark,
    HighContrast
}