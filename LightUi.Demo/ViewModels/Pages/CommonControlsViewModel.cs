using Caliburn.Micro;

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
}