using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Configuration;
using MiniCAD.App.Input;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Tools;
using MiniCAD.Core.Viewing;

namespace MiniCAD.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string UntitledName = "Unbenannt";

    private readonly UndoRedoManager _commands = new();

    private readonly SelectTool _selectTool = new();
    private readonly LineTool _lineTool = new();
    private readonly RectangleTool _rectangleTool = new();
    private readonly CircleTool _circleTool = new();
    private readonly PolylineTool _polylineTool = new();
    private readonly SetNullPointTool _setNullPointTool = new();

    private string? _filePath;

    public MainWindowViewModel()
    {
        Document = new CadDocument();
        Tools = new ToolManager(Document, _commands, Viewport) { DefaultTool = _selectTool };
        Settings = new SettingsViewModel(Shortcuts);
        Attributes = new AttributesViewModel(Document, Tools.Selection, _commands);
        StructurePanel = new StructurePanelViewModel(Document);
        Patterns = new PatternsViewModel(Document);
        Assistant = new AssistantViewModel();
        CoordinateInput = new CoordinateInputViewModel(Tools, Document);

        Document.Changed += OnDocumentChanged;
        Document.CoordinateSystem.Changed += OnOriginChanged;
        Tools.Selection.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusText));
            DeleteSelectionCommand.NotifyCanExecuteChanged();
            AddSelectionAsTemplateCommand.NotifyCanExecuteChanged();
        };
        Tools.ActiveToolChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusText));
            CoordinateInput.IsEnabled = IsCoordinateTool(Tools.ActiveTool);
        };
        _commands.StateChanged += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(StatusText));
        };

        // Grid snapping is only meaningful when the grid is shown, so tie the two together.
        Tools.SnapSettings.SnapToGrid = Settings.ShowGrid;
        Settings.PropertyChanged += OnSettingsChanged;

        // The Assistant reuses a predefined element by re-activating its tool with that style.
        Assistant.UseRequested += OnUseTemplate;

        // Double right-click on an object adopts its layer/Teilbild and re-activates its tool.
        Tools.RegisterQuickSelectTool<LineEntity>(_lineTool);
        Tools.RegisterQuickSelectTool<CircleEntity>(_circleTool);
        Tools.RegisterQuickSelectTool<PolylineEntity>(_polylineTool);

        Tools.SetActiveTool(_selectTool);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ShowGrid))
            Tools.SnapSettings.SnapToGrid = Settings.ShowGrid;
    }

    public CadDocument Document { get; }

    public Viewport Viewport { get; } = new();

    public ToolManager Tools { get; }

    /// <summary>The customizable keyboard shortcuts, shared with the settings dialog.</summary>
    public KeyboardShortcuts Shortcuts { get; } = new();

    /// <summary>View settings shown in the settings dialog and applied to the canvas.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Docked attributes ("Eigenschaften") panel for the current selection.</summary>
    public AttributesViewModel Attributes { get; }

    /// <summary>Side panel managing Teilbilder and layers.</summary>
    public StructurePanelViewModel StructurePanel { get; }

    /// <summary>The hatch pattern ("Muster") library and project pattern editor.</summary>
    public PatternsViewModel Patterns { get; }

    /// <summary>The Assistant panel with reusable predefined elements.</summary>
    public AssistantViewModel Assistant { get; }

    /// <summary>The Allplan-style coordinate entry line (type X/Y instead of clicking).</summary>
    public CoordinateInputViewModel CoordinateInput { get; }

    /// <summary>True for tools that place points, where typed coordinate entry is meaningful.</summary>
    private bool IsCoordinateTool(ITool? tool)
        => tool == _lineTool || tool == _rectangleTool || tool == _circleTool
        || tool == _polylineTool || tool == _setNullPointTool;

    /// <summary>Two-way bound to the snap toggle in the toolbar.</summary>
    [ObservableProperty]
    private bool _snapEnabled = true;

    /// <summary>The current project's display name (derived from the file name).</summary>
    [ObservableProperty]
    private string _projectName = UntitledName;

    /// <summary>True when there are unsaved changes.</summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>Transient status message (save/open result or error), shown in the status bar.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Live cursor coordinates relative to the Nullpunkt (X/Y/Z), shown in the status bar.</summary>
    [ObservableProperty]
    private string _cursorText = "X 0   Y 0   Z 0";

    /// <summary>Updates the coordinate readout from a cursor position in absolute world space.</summary>
    public void UpdateCursor(Point2D world)
    {
        Point3D local = Document.CoordinateSystem.ToLocal(world);
        CursorText = $"X {CoordinateFormat.ToText(local.X, "0.##")}   "
                   + $"Y {CoordinateFormat.ToText(local.Y, "0.##")}   "
                   + $"Z {CoordinateFormat.ToText(local.Z, "0.##")}";
    }

    private void OnOriginChanged(object? sender, System.EventArgs e)
    {
        // Moving the Nullpunkt is a saved change and shifts the reported coordinates.
        IsDirty = true;
        Tools.RequestRedraw();
    }

    /// <summary>Full path of the current project file, or <c>null</c> if never saved.</summary>
    public string? FilePath => _filePath;

    public bool HasFilePath => _filePath is not null;

    public string Title => $"{ProjectName}{(IsDirty ? " *" : string.Empty)} — MiniCAD";

    public string StatusText =>
        $"Werkzeug: {Tools.ActiveToolName}   ·   {Document.Entities.Count} Objekte   ·   Auswahl: {Tools.Selection.Count}";

    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(Title));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    partial void OnSnapEnabledChanged(bool value)
    {
        Tools.SnapSettings.Enabled = value;
        Tools.RequestRedraw();
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        // Content edits mark the project dirty; loading/replacing does not.
        if (e.Kind is not DocumentChangeKind.Reloaded)
            IsDirty = true;

        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>Starts a fresh, empty project (location chosen on first save).</summary>
    public void NewProject()
    {
        DocumentMapper.Apply(new DocumentDto(), Document);
        _commands.Clear();
        _filePath = null;
        OnPropertyChanged(nameof(HasFilePath));
        ProjectName = UntitledName;
        IsDirty = false;
        Viewport.Reset();
        StatusMessage = "Neues Projekt angelegt.";
    }

    /// <summary>Saves the current plan to <paramref name="path"/> and adopts it as the project file.</summary>
    public void SaveToPath(string path)
    {
        ProjectName = Path.GetFileNameWithoutExtension(path);
        var dto = new ProjectFileDto { Name = ProjectName, Document = DocumentMapper.ToDto(Document) };
        ProjectSerializer.Save(dto, path);

        _filePath = path;
        OnPropertyChanged(nameof(HasFilePath));
        IsDirty = false;
        AppConfig.Instance.AddRecentProject(path);
        StatusMessage = $"Gespeichert: {path}";
    }

    /// <summary>Saves to the existing project file (caller ensures <see cref="HasFilePath"/>).</summary>
    public void Save()
    {
        if (_filePath is not null)
            SaveToPath(_filePath);
    }

    /// <summary>Loads a project plan from <paramref name="path"/> into the current document.</summary>
    public void OpenFromPath(string path)
    {
        ProjectFileDto dto = ProjectSerializer.Load(path);
        DocumentMapper.Apply(dto.Document, Document);
        _commands.Clear();

        _filePath = path;
        OnPropertyChanged(nameof(HasFilePath));
        ProjectName = string.IsNullOrWhiteSpace(dto.Name) ? Path.GetFileNameWithoutExtension(path) : dto.Name;
        IsDirty = false;
        AppConfig.Instance.AddRecentProject(path);

        // The canvas auto-fits to the new content when it sees the Reloaded event.
        StatusMessage = $"Geöffnet: {path}";
        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand]
    private void ActivateSelect() => Tools.SetActiveTool(_selectTool);

    [RelayCommand]
    private void ActivateLine() => ActivateDrawingTool(_lineTool);

    [RelayCommand]
    private void ActivateRectangle() => ActivateDrawingTool(_rectangleTool);

    [RelayCommand]
    private void ActivateCircle() => ActivateDrawingTool(_circleTool);

    [RelayCommand]
    private void ActivatePolyline() => ActivateDrawingTool(_polylineTool);

    /// <summary>Clears any Assistant style so the next entity uses the layer default again.</summary>
    private void ActivateDrawingTool(ITool tool)
    {
        Tools.DefaultStrokeOverride = null;
        Tools.SetActiveTool(tool);
    }

    [RelayCommand]
    private void ActivateSetNullPoint() => Tools.SetActiveTool(_setNullPointTool);

    [RelayCommand]
    private void ResetNullPoint() => Document.CoordinateSystem.Reset();

    [RelayCommand]
    private void AddStandardLayers() => StructurePanel.AddStandardLayersCommand.Execute(null);

    [RelayCommand]
    private void ToggleSnap() => SnapEnabled = !SnapEnabled;

    [RelayCommand(CanExecute = nameof(CanAddSelectionAsTemplate))]
    private void AddSelectionAsTemplate()
    {
        if (Tools.Selection.Items.Count != 1)
            return;

        IEntity entity = Tools.Selection.Items[0];
        Assistant.AddFromEntity(entity, Document.ResolveStroke(entity));
        StatusMessage = "Vorlage zum Assistenten hinzugefügt.";
    }

    private bool CanAddSelectionAsTemplate() => Tools.Selection.Count == 1;

    /// <summary>Reuses an Assistant template: re-activate its tool with the stored line style.</summary>
    private void OnUseTemplate(AssistantItemViewModel item)
    {
        ITool tool = item.ToolKind switch
        {
            AssistantToolKind.Line => _lineTool,
            AssistantToolKind.Rectangle => _rectangleTool,
            AssistantToolKind.Circle => _circleTool,
            AssistantToolKind.Polyline => _polylineTool,
            _ => _lineTool,
        };

        Tools.DefaultStrokeOverride = item.Stroke;
        Tools.SetActiveTool(tool);
        StatusMessage = $"Vorlage „{item.Name}“ aktiv.";
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        if (Document.GetContentBounds() is { } bounds)
            Viewport.ZoomToFit(bounds);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelection))]
    private void DeleteSelection()
    {
        if (Tools.Selection.IsEmpty)
            return;

        var removals = Tools.Selection.Items
            .Select(entity => (IUndoableCommand)new RemoveEntityCommand(Document, entity))
            .ToList();

        _commands.Execute(new CompositeCommand("Löschen", removals));
        Tools.Selection.Clear();
    }

    private bool CanDeleteSelection() => !Tools.Selection.IsEmpty;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _commands.Undo();

    private bool CanUndo() => _commands.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _commands.Redo();

    private bool CanRedo() => _commands.CanRedo;

    /// <summary>
    /// Runs a keyboard-shortcut action that does not need UI services. File actions
    /// (new/open/save) are not handled here — they return <c>false</c> so the window can run
    /// its async file pickers instead.
    /// </summary>
    public bool RunShortcut(ShortcutAction action)
    {
        switch (action)
        {
            case ShortcutAction.Select: ActivateSelect(); return true;
            case ShortcutAction.Line: ActivateLine(); return true;
            case ShortcutAction.Rectangle: ActivateRectangle(); return true;
            case ShortcutAction.Circle: ActivateCircle(); return true;
            case ShortcutAction.Polyline: ActivatePolyline(); return true;
            case ShortcutAction.Delete: DeleteSelectionCommand.Execute(null); return true;
            case ShortcutAction.Undo: UndoCommand.Execute(null); return true;
            case ShortcutAction.Redo: RedoCommand.Execute(null); return true;
            case ShortcutAction.ZoomToFit: ZoomToFit(); return true;
            case ShortcutAction.ToggleSnap: ToggleSnap(); return true;
            case ShortcutAction.SetNullPoint: ActivateSetNullPoint(); return true;
            default: return false; // file actions handled by the window
        }
    }
}
