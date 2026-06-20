using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MiniCAD.App.Input;
using MiniCAD.App.ViewModels;
using MiniCAD.Core.Geometry;

namespace MiniCAD.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ProjectFileType =
        new("MiniCAD Projekt") { Patterns = new[] { "*.mcad" } };

    public MainWindow()
    {
        InitializeComponent();
        Canvas.CursorWorldMoved += OnCursorWorldMoved;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnCursorWorldMoved(object? sender, Point2D world) => ViewModel?.UpdateCursor(world);

    private void OnAssistantDoubleTapped(object? sender, TappedEventArgs e) => ViewModel?.Assistant.UseSelected();

    private void OnCoordinateKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return))
            return;

        ViewModel?.CoordinateInput.CommitCommand.Execute(null);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || ViewModel is not { } viewModel)
            return;

        // Don't steal plain (modifier-less) keys while the user is typing in a panel field.
        bool hasModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (!hasModifier && IsTextInputFocused())
            return;

        if (!viewModel.Shortcuts.TryResolve(e, out ShortcutAction action))
            return;

        switch (action)
        {
            case ShortcutAction.NewProject:
                viewModel.NewProject();
                break;
            case ShortcutAction.OpenProject:
                _ = OpenAsync();
                break;
            case ShortcutAction.Save:
                _ = SaveAsync();
                break;
            case ShortcutAction.SaveAs:
                _ = SaveAsAsync();
                break;
            default:
                if (!viewModel.RunShortcut(action))
                    return;
                break;
        }

        e.Handled = true;
    }

    private bool IsTextInputFocused()
        => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;

    private void OnNewProject(object? sender, RoutedEventArgs e) => ViewModel?.NewProject();

    private void OnOpenProject(object? sender, RoutedEventArgs e) => _ = OpenAsync();

    private void OnSaveProject(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    private void OnSaveProjectAs(object? sender, RoutedEventArgs e) => _ = SaveAsAsync();

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        // The dialog shares the same settings instance, so edits apply to the canvas live.
        var settings = new SettingsWindow { DataContext = viewModel.Settings };
        settings.ShowDialog(this);
    }

    private async Task OpenAsync()
    {
        if (ViewModel is not { } viewModel || StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Projekt öffnen",
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjectFileType },
        });

        if (files.Count == 0)
            return;

        try
        {
            viewModel.OpenFromPath(files[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"Fehler beim Öffnen: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        if (ViewModel is not { } viewModel)
            return;

        if (!viewModel.HasFilePath)
        {
            await SaveAsAsync();
            return;
        }

        try
        {
            viewModel.Save();
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"Fehler beim Speichern: {ex.Message}";
        }
    }

    private async Task SaveAsAsync()
    {
        if (ViewModel is not { } viewModel || StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Projekt speichern",
            SuggestedFileName = viewModel.ProjectName,
            DefaultExtension = "mcad",
            FileTypeChoices = new[] { ProjectFileType },
        });

        if (file is null)
            return;

        try
        {
            viewModel.SaveToPath(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"Fehler beim Speichern: {ex.Message}";
        }
    }
}
