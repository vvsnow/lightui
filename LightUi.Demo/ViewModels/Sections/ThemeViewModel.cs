using Caliburn.Micro;

namespace LightUi.Demo.ViewModels.Sections
{
    class ThemeViewModel : Screen
    {

        public void ApplyLight() => ThemeManager.ApplyTheme(ApplicationTheme.Light);

        public void ApplyDark() => ThemeManager.ApplyTheme(ApplicationTheme.Dark);

    }
}
