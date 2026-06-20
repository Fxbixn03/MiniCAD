using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MiniCAD.App.Input;
using MiniCAD.App.ViewModels;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ProjectFileType =
        new("MiniCAD Projekt") { Patterns = new[] { "*.mcad" } };

    private MainWindowViewModel? _boundViewModel;
    private TextEditRequest? _activeEdit;

    public MainWindow()
    {
        InitializeComponent();
        Canvas.CursorWorldMoved += OnCursorWorldMoved;
        Canvas.DoubleClicked += OnCanvasDoubleClicked;
        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundViewModel is { } previous)
            previous.TextEditRequested -= OnTextEditRequested;

        _boundViewModel = ViewModel;
        if (_boundViewModel is { } current)
            current.TextEditRequested += OnTextEditRequested;
    }

    private void OnCursorWorldMoved(object? sender, Point2D world) => ViewModel?.UpdateCursor(world);

    private void OnCanvasDoubleClicked(object? sender, Point2D world) => ViewModel?.EditTextAt(world);

    // ----- Inline text editor (text tool) -----

    private void OnTextEditRequested(TextEditRequest request)
    {
        if (ViewModel is not { } viewModel)
            return;

        Point2D device = viewModel.Viewport.WorldToScreenPoint(request.AnchorWorld);
        double scaling = TopLevel.GetTopLevel(Canvas)?.RenderScaling ?? 1.0;

        _activeEdit = request;
        InlineTextEditor.AcceptsReturn = request.Multiline;
        InlineTextEditor.Text = request.InitialText;
        InlineTextEditor.Margin = new Thickness(device.X / scaling, device.Y / scaling, 0, 0);
        InlineTextEditor.IsVisible = true;
        InlineTextEditor.Focus();
        InlineTextEditor.SelectAll();
    }

    private void OnInlineTextEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelInlineEditor();
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Return
            && (!InlineTextEditor.AcceptsReturn || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            // Single-line commits on Enter; multi-line keeps Enter for newlines, Ctrl+Enter commits.
            CommitInlineEditor();
            e.Handled = true;
        }
    }

    private void OnInlineTextEditorLostFocus(object? sender, RoutedEventArgs e) => CommitInlineEditor();

    private void CommitInlineEditor()
    {
        if (_activeEdit is not { } request)
            return;

        _activeEdit = null;
        string text = InlineTextEditor.Text ?? string.Empty;
        InlineTextEditor.IsVisible = false;
        request.Commit(text);
    }

    private void CancelInlineEditor()
    {
        if (_activeEdit is not { } request)
            return;

        _activeEdit = null;
        InlineTextEditor.IsVisible = false;
        request.Cancel();
        Canvas.Focus();
    }

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
