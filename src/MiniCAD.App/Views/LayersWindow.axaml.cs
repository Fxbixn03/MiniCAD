using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniCAD.App.Views;

public partial class LayersWindow : Window
{
    public LayersWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
