using Caliburn.Micro;

using LightUi.Demo.Models;

namespace LightUi.Demo.ViewModels.Pages;

internal class CommonControlsViewModel : Screen
{
    public List<string> FruitItems { get; } = new()
    {
        "Apple", "Banana", "Cherry", "Durian", "Elderberry",
        "Fig", "Grape", "Honeydew", "Jackfruit", "Kiwi",
        "Lemon", "Mango", "Nectarine", "Orange", "Papaya"
    };

    public string SelectedFruit { get; set; } = "Apple";

    /// <summary>TreeView 演示数据：一棵小文件树，图标码点取系统图标字体。</summary>
    public IReadOnlyList<FileNode> FileTree { get; } =
    [
        new FileNode("LightUi", "", true,
            new FileNode("Theme", "", true,
                new FileNode("Light.xaml", ""),
                new FileNode("Dark.xaml", ""),
                new FileNode("Size.xaml", "")),
            new FileNode("styles", "", true,
                new FileNode("Button.xaml", ""),
                new FileNode("ListBox.xaml", ""),
                new FileNode("TreeView.xaml", "")),
            new FileNode("LightUi.csproj", ""),
            new FileNode("README.md", "")),

        new FileNode("LightUi.Demo", "", false,
            new FileNode("App.xaml", ""),
            new FileNode("Bootstrapper.cs", ""),
            new FileNode("Views", "", false,
                new FileNode("ShellView.xaml", ""))),
    ];
}