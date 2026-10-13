using Caliburn.Micro;

using System.Reflection;

namespace LightUi.Demo.ViewModels.Pages;

internal class VersionViewModel : Screen
{
    public string DemoVersion => GetVersion(Assembly.GetExecutingAssembly());
    public string UiVersion => GetVersion(typeof(LightUi.Controls.FontIcon).Assembly);

    private static string GetVersion(Assembly asm) =>
        ( asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?" )
            .Split('+')[0];
}
