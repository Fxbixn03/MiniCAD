using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniCAD.App.Views;

public partial class PartialDrawingsWindow : Window
{
    public PartialDrawingsWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
