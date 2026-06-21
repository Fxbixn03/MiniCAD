using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using MiniCAD.Core.Model3D;

namespace MiniCAD.App.Views;

/// <summary>A read-only report listing model volumes/masses aggregated by material (#269).</summary>
public partial class MassTakeoffWindow : Window
{
    private IReadOnlyList<MassTakeoffRow> _rows = new List<MassTakeoffRow>();

    public MassTakeoffWindow()
    {
        InitializeComponent();
    }

    public MassTakeoffWindow(IReadOnlyList<MassTakeoffRow> rows) : this()
    {
        _rows = rows;
        RowsHost.ItemsSource = rows;
    }

    private async void OnCopyCsv(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is { } clipboard)
            await clipboard.SetTextAsync(MassTakeoff.ToCsv(_rows));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
