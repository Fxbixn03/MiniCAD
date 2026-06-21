using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MiniCAD.App.Input;
using MiniCAD.App.ViewModels;
using MiniCAD.App.ViewModels.Toolbar;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ProjectFileType =
        new("MiniCAD Projekt") { Patterns = new[] { "*.mcad" } };

    private static readonly FilePickerFileType ImageFileType =
        new("Bilder") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } };

    private MainWindowViewModel? _boundViewModel;
    private TextEditRequest? _activeEdit;

    // In-process drag payload: "toolbar" (dock the whole bar) or "group:<id>" (reorder a block).
    private static readonly DataFormat<string> ToolbarDragFormat =
        DataFormat.CreateInProcessFormat<string>("minicad.toolbar.drag");

    private Point2D _lastCursorWorld;

    public MainWindow()
    {
        InitializeComponent();
        Canvas.CursorWorldMoved += OnCursorWorldMoved;
        Canvas.DoubleClicked += OnCanvasDoubleClicked;
        Canvas.DistanceInputStarted += OnDistanceInputStarted;
        DataContextChanged += OnDataContextChanged;

        DragDrop.AddDragOverHandler(this, OnToolbarDragOver);
        DragDrop.AddDropHandler(this, OnToolbarDrop);
    }

    // ----- Toolbar drag & drop (reorder blocks / re-dock the whole bar) -----

    /// <summary>Starts dragging a single tool block (the grip is the drag source).</summary>
    private async void OnToolGroupGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } vm || vm.Toolbar.DragLocked)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if ((sender as Control)?.DataContext is not ToolGroupViewModel group)
            return;

        e.Handled = true;
        await StartToolbarDrag(e, "group:" + group.Id);
    }

    /// <summary>Starts dragging the whole toolbar (the handle is the drag source).</summary>
    private async void OnToolbarHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } vm || vm.Toolbar.DragLocked)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        e.Handled = true;
        await StartToolbarDrag(e, "toolbar");
    }

    private static async Task StartToolbarDrag(PointerPressedEventArgs e, string payload)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ToolbarDragFormat, payload));
        try
        {
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        catch
        {
            // A failed drag must never crash the app.
        }
    }

    private void OnToolbarDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.DataTransfer.Contains(ToolbarDragFormat) ? DragDropEffects.Move : DragDropEffects.None;

    private void OnToolbarDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;
        string? payload = e.DataTransfer.TryGetValue(ToolbarDragFormat);
        if (payload is null)
            return;

        if (payload == "toolbar")
        {
            // Dock to the edge nearest the drop point.
            Point p = e.GetPosition(this);
            double w = Bounds.Width, h = Bounds.Height;
            double[] dist = { p.X, w - p.X, p.Y, h - p.Y }; // left, right, top, bottom
            int min = Array.IndexOf(dist, dist.Min());
            vm.Toolbar.Dock = min switch
            {
                1 => ToolbarDock.Right,
                2 => ToolbarDock.Top,
                3 => ToolbarDock.Bottom,
                _ => ToolbarDock.Left,
            };
        }
        else if (payload.StartsWith("group:", StringComparison.Ordinal))
        {
            string sourceId = payload["group:".Length..];
            ToolGroupViewModel? target = (e.Source as Visual)?
                .GetSelfAndVisualAncestors()
                .Select(a => a.DataContext)
                .OfType<ToolGroupViewModel>()
                .FirstOrDefault();
            if (target is not null)
                vm.Toolbar.MoveGroup(sourceId, target.Id);
        }
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

    private void OnCursorWorldMoved(object? sender, Point2D world)
    {
        _lastCursorWorld = world;
        ViewModel?.UpdateCursor(world);
    }

    private void OnCanvasDoubleClicked(object? sender, Point2D world) => ViewModel?.EditTextAt(world);

    // ----- Direct length entry at the crosshair -----

    private void OnDistanceInputStarted(object? sender, string firstChar)
    {
        if (ViewModel is not { } vm)
            return;

        Point2D device = vm.Viewport.WorldToScreenPoint(_lastCursorWorld);
        double scaling = TopLevel.GetTopLevel(Canvas)?.RenderScaling ?? 1.0;

        // Offset a little down-right of the crosshair so the field doesn't sit on top of it.
        DistanceInputBox.Margin = new Thickness(device.X / scaling + 16, device.Y / scaling + 16, 0, 0);
        DistanceInput.Text = firstChar;
        DistanceInputBox.IsVisible = true;
        DistanceInput.Focus();
        DistanceInput.CaretIndex = firstChar.Length;
    }

    private void OnDistanceInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideDistanceInput();
            Canvas.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CommitDistanceInput();
            e.Handled = true;
        }
    }

    private void OnDistanceInputLostFocus(object? sender, RoutedEventArgs e) => HideDistanceInput();

    private void CommitDistanceInput()
    {
        if (ViewModel is { } vm && CoordinateFormat.TryParse(DistanceInput.Text ?? string.Empty, out double length))
            vm.Tools.CommitDistance(length, _lastCursorWorld);

        HideDistanceInput();
        Canvas.Focus();
    }

    private void HideDistanceInput() => DistanceInputBox.IsVisible = false;

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

    private void OnInsertImage(object? sender, RoutedEventArgs e) => _ = InsertImageAsync();

    private async Task InsertImageAsync()
    {
        if (ViewModel is not { } viewModel || StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Bild als Unterlage einfügen",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType },
        });

        if (files.Count == 0)
            return;

        try
        {
            await using Stream source = await files[0].OpenReadAsync();
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            byte[] data = buffer.ToArray();

            double aspect = 1.0;
            try
            {
                using var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(data));
                if (bitmap.PixelSize.Height > 0)
                    aspect = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
            }
            catch
            {
                // Unknown pixel size — fall back to a square placement.
            }

            viewModel.InsertImageEntity(data, aspect);
        }
        catch (Exception ex)
        {
            viewModel.StatusMessage = $"Fehler beim Einfügen: {ex.Message}";
        }
    }

    private void OnDetach3D(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        // Same document (live model), independent camera.
        var window = new View3DWindow(viewModel.Document, new MiniCAD.Core.Viewing.Camera3D());
        window.Show(this);
    }

    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    private void OnPartialDrawingSelection(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
            return;

        var dialog = new PartialDrawingsWindow
        {
            DataContext = new PartialDrawingSelectionViewModel(viewModel.Document),
        };
        dialog.ShowDialog(this);
    }

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
