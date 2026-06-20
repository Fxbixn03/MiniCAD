using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MiniCAD.App.ViewModels;

namespace MiniCAD.App.Views;

public partial class StartWindow : Window
{
    private static readonly FilePickerFileType ProjectFileType =
        new("MiniCAD Projekt") { Patterns = new[] { "*.mcad" } };

    public StartWindow()
    {
        InitializeComponent();
    }

    private void OnNewProject(object? sender, RoutedEventArgs e) => OpenMainWindow(null);

    private async void OnOpenProject(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Projekt öffnen",
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjectFileType },
        });

        if (files.Count > 0)
            OpenMainWindow(files[0].Path.LocalPath);
    }

    private void OnRecentDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: RecentProjectItem item })
            OpenMainWindow(item.Path);
    }

    /// <summary>Creates and shows the main window (optionally loading a project), then closes the start window.</summary>
    private void OpenMainWindow(string? projectPath)
    {
        var viewModel = new MainWindowViewModel();
        var main = new MainWindow { DataContext = viewModel };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = main;

        // Show the main window before closing this one so the app does not shut down.
        main.Show();

        if (projectPath is not null)
        {
            try
            {
                viewModel.OpenFromPath(projectPath);
            }
            catch (Exception ex)
            {
                viewModel.StatusMessage = $"Fehler beim Öffnen: {ex.Message}";
            }
        }

        Close();
    }
}
