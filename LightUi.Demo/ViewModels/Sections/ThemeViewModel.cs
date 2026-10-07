using Caliburn.Micro;

using System.Diagnostics;
using System.Windows.Media;

namespace LightUi.Demo.ViewModels.Sections
{
    class ThemeViewModel : Screen
    {
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("ThemeViewModel activated");
            return base.OnActivateAsync(cancellationToken);
        }

        public string CurrentThemeName => ThemeManager.CurrentTheme.ToString();

        /// <summary>输入框里的强调色，形如 #RRGGBB 或 #AARRGGBB（也认颜色名）。</summary>
        public string AccentInput { get; set; } = "#2563EB";


        /// <summary>输入解析失败时的提示；null 表示没问题。</summary>
        public string? AccentError { get; set; } = string.Empty;

        public void ApplyLight() => ThemeManager.ApplyTheme(ApplicationTheme.Light);

        public void ApplyDark() => ThemeManager.ApplyTheme(ApplicationTheme.Dark);

        /// <summary>把输入框里的颜色应用到界面。</summary>
        public void ApplyAccent()
        {
            if (TryParseColor(AccentInput, out var color))
            {
                ThemeManager.ApplyAccentColor(color);
                AccentError = null;

                // 回写规整后的写法（比如输入 "Red" → "#FFFF0000"）
                SyncAccentInput();
            }
            else
            {
                AccentError = $"认不出这个颜色：{AccentInput}（试试 #0078D4）";
            }
        }

        /// <summary>回到当前主题自带的强调色，输入框同步改成该颜色。</summary>
        public void ResetAccent()
        {
            ThemeManager.ResetAccentColor();
            AccentError = null;
            SyncAccentInput();
        }

        /// <summary>把输入框同步成当前实际生效的强调色。</summary>
        private void SyncAccentInput()
        {
            if (ThemeManager.CurrentAccentColor is not { } c)
            {
                return;
            }

            AccentInput = c.A == 255
                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private static bool TryParseColor(string? text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                if (ColorConverter.ConvertFromString(text.Trim()) is Color parsed)
                {
                    color = parsed;
                    return true;
                }
            }
            catch (FormatException)
            {
                // 输入不是合法颜色
            }
            catch (NotSupportedException)
            {
                // 输入不是颜色格式
            }

            return false;
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);

            ThemeManager.CurrentThemeChanged += (_, __) =>
            {
                NotifyOfPropertyChange(nameof(CurrentThemeName));

                // 换主题时强调色也会变（没自定义过的话），输入框同步显示当前值
                SyncAccentInput();
            };
        }
    }
}
