using System;
using System.Collections.Generic;
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
    private readonly ArcTool _arcTool = new();
    private readonly EllipseTool _ellipseTool = new();
    private readonly PolylineTool _polylineTool = new();
    private readonly SplineTool _splineTool = new();
    private readonly PointTool _pointTool = new();
    private readonly TextTool _textTool = new();
    private readonly LeaderTool _leaderTool = new();
    private readonly SetNullPointTool _setNullPointTool = new();

    // Editing tools operating on the current selection (Epic: Bearbeitungswerkzeuge).
    private readonly MoveTool _moveTool = new(copy: false);
    private readonly MoveTool _copyTool = new(copy: true);
    private readonly RotateTool _rotateTool = new();
    private readonly MirrorTool _mirrorTool = new();
    private readonly ScaleTool _scaleTool = new();
    private readonly OffsetTool _offsetTool = new();
    private readonly TrimExtendTool _trimTool = new();
    private readonly StretchTool _stretchTool = new();
    private readonly FilletTool _filletTool = new();
    private readonly ArrayTool _arrayTool = new();

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
        FilletOptions = new FilletOptionsViewModel(_filletTool);
        ArrayOptions = new ArrayOptionsViewModel(_arrayTool);
        ArcOptions = new ArcOptionsViewModel(_arcTool);
        PointOptions = new PointOptionsViewModel(_pointTool);
        TextOptions = new TextOptionsViewModel(_textTool, Document);
        LeaderOptions = new LeaderOptionsViewModel(_leaderTool);
        TextStyles = new TextStylesViewModel(Document);

        // Text and leader tools can't open a UI field themselves; re-raise their requests so the
        // view can show the shared inline editor.
        _textTool.EditRequested += request => TextEditRequested?.Invoke(request);
        _leaderTool.EditRequested += request => TextEditRequested?.Invoke(request);

        Document.Changed += OnDocumentChanged;
        Document.CoordinateSystem.Changed += OnOriginChanged;
        Tools.Selection.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusText));
            DeleteSelectionCommand.NotifyCanExecuteChanged();
            AddSelectionAsTemplateCommand.NotifyCanExecuteChanged();
            ActivateMoveCommand.NotifyCanExecuteChanged();
            ActivateCopyCommand.NotifyCanExecuteChanged();
            ActivateRotateCommand.NotifyCanExecuteChanged();
            ActivateMirrorCommand.NotifyCanExecuteChanged();
            ActivateScaleCommand.NotifyCanExecuteChanged();
            ActivateArrayCommand.NotifyCanExecuteChanged();
        };
        Tools.ActiveToolChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusText));
            CoordinateInput.IsEnabled = IsCoordinateTool(Tools.ActiveTool);
            RaiseActiveToolFlags();
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
        Tools.RegisterQuickSelectTool<ArcEntity>(_arcTool);
        Tools.RegisterQuickSelectTool<EllipseEntity>(_ellipseTool);
        Tools.RegisterQuickSelectTool<PolylineEntity>(_polylineTool);
        Tools.RegisterQuickSelectTool<SplineEntity>(_splineTool);
        Tools.RegisterQuickSelectTool<PointEntity>(_pointTool);
        Tools.RegisterQuickSelectTool<TextEntity>(_textTool);
        Tools.RegisterQuickSelectTool<MTextEntity>(_textTool);
        Tools.RegisterQuickSelectTool<LeaderEntity>(_leaderTool);

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

    /// <summary>Inline radius/chamfer parameters, shown while the fillet tool is active.</summary>
    public FilletOptionsViewModel FilletOptions { get; }

    /// <summary>Inline rectangular/polar array parameters, shown while the array tool is active.</summary>
    public ArrayOptionsViewModel ArrayOptions { get; }

    /// <summary>Inline arc-mode selector, shown while the arc tool is active.</summary>
    public ArcOptionsViewModel ArcOptions { get; }

    /// <summary>Inline marker style/size, shown while the point tool is active.</summary>
    public PointOptionsViewModel PointOptions { get; }

    /// <summary>Inline cap height/alignment, shown while the text tool is active.</summary>
    public TextOptionsViewModel TextOptions { get; }

    /// <summary>Inline label height, shown while the leader tool is active.</summary>
    public LeaderOptionsViewModel LeaderOptions { get; }

    /// <summary>The text-style management panel (Textstile tab).</summary>
    public TextStylesViewModel TextStyles { get; }

    /// <summary>Raised when the text tool wants the view to open its inline editor.</summary>
    public event Action<TextEditRequest>? TextEditRequested;

    /// <summary>True for tools that place points, where typed coordinate entry is meaningful.</summary>
    private bool IsCoordinateTool(ITool? tool)
        => tool == _lineTool || tool == _rectangleTool || tool == _circleTool
        || tool == _arcTool || tool == _ellipseTool || tool == _polylineTool || tool == _splineTool
        || tool == _pointTool || tool == _textTool || tool == _leaderTool || tool == _setNullPointTool
        || tool == _moveTool || tool == _copyTool || tool == _rotateTool
        || tool == _mirrorTool || tool == _scaleTool || tool == _offsetTool;

    /// <summary>Two-way bound to the snap toggle in the toolbar.</summary>
    [ObservableProperty]
    private bool _snapEnabled = true;

    /// <summary>Two-way bound to the Ortho toggle: locks segments to horizontal/vertical.</summary>
    [ObservableProperty]
    private bool _orthoEnabled;

    /// <summary>Two-way bound to the Polar toggle: snaps segments to fixed angle steps.</summary>
    [ObservableProperty]
    private bool _polarEnabled;

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

    /// <summary>Length/angle from the last point to the cursor while a drawing tool is active.</summary>
    [ObservableProperty]
    private string _polarReadout = string.Empty;

    /// <summary>Updates the coordinate readout from a cursor position in absolute world space.</summary>
    public void UpdateCursor(Point2D world)
    {
        Point3D local = Document.CoordinateSystem.ToLocal(world);
        CursorText = $"X {CoordinateFormat.ToText(local.X, "0.##")}   "
                   + $"Y {CoordinateFormat.ToText(local.Y, "0.##")}   "
                   + $"Z {CoordinateFormat.ToText(local.Z, "0.##")}";

        // Polar readout: distance and angle from the last placed point (Allplan/AutoCAD style).
        if (IsCoordinateTool(Tools.ActiveTool) && Tools.LastPoint is { } last)
        {
            Vector2D delta = world - last;
            double length = delta.Length;
            double angle = GeometryMath.RadiansToDegrees(System.Math.Atan2(delta.Y, delta.X));
            if (angle < 0)
                angle += 360.0;
            PolarReadout = length < 1e-6
                ? string.Empty
                : $"L {CoordinateFormat.ToText(length, "0.##")}   ∠ {CoordinateFormat.ToText(angle, "0.#")}°";
        }
        else
        {
            PolarReadout = string.Empty;
        }
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

    /// <summary>The active tool's display name, shown in the status bar tool cell.</summary>
    public string ActiveToolName => Tools.ActiveToolName;

    // Per-tool active flags drive the toolbar's brand-blue "selected tool" highlight.
    public bool IsSelectActive => Tools.ActiveTool == _selectTool;
    public bool IsLineActive => Tools.ActiveTool == _lineTool;
    public bool IsRectangleActive => Tools.ActiveTool == _rectangleTool;
    public bool IsCircleActive => Tools.ActiveTool == _circleTool;
    public bool IsArcActive => Tools.ActiveTool == _arcTool;
    public bool IsEllipseActive => Tools.ActiveTool == _ellipseTool;
    public bool IsPolylineActive => Tools.ActiveTool == _polylineTool;
    public bool IsSplineActive => Tools.ActiveTool == _splineTool;
    public bool IsPointActive => Tools.ActiveTool == _pointTool;
    public bool IsTextActive => Tools.ActiveTool == _textTool;
    public bool IsLeaderActive => Tools.ActiveTool == _leaderTool;
    public bool IsSetNullPointActive => Tools.ActiveTool == _setNullPointTool;
    public bool IsMoveActive => Tools.ActiveTool == _moveTool;
    public bool IsCopyActive => Tools.ActiveTool == _copyTool;
    public bool IsRotateActive => Tools.ActiveTool == _rotateTool;
    public bool IsMirrorActive => Tools.ActiveTool == _mirrorTool;
    public bool IsScaleActive => Tools.ActiveTool == _scaleTool;
    public bool IsOffsetActive => Tools.ActiveTool == _offsetTool;
    public bool IsTrimActive => Tools.ActiveTool == _trimTool;
    public bool IsStretchActive => Tools.ActiveTool == _stretchTool;
    public bool IsFilletActive => Tools.ActiveTool == _filletTool;
    public bool IsArrayActive => Tools.ActiveTool == _arrayTool;

    private void RaiseActiveToolFlags()
    {
        OnPropertyChanged(nameof(ActiveToolName));
        OnPropertyChanged(nameof(IsSelectActive));
        OnPropertyChanged(nameof(IsLineActive));
        OnPropertyChanged(nameof(IsRectangleActive));
        OnPropertyChanged(nameof(IsCircleActive));
        OnPropertyChanged(nameof(IsArcActive));
        OnPropertyChanged(nameof(IsEllipseActive));
        OnPropertyChanged(nameof(IsPolylineActive));
        OnPropertyChanged(nameof(IsSplineActive));
        OnPropertyChanged(nameof(IsPointActive));
        OnPropertyChanged(nameof(IsTextActive));
        OnPropertyChanged(nameof(IsLeaderActive));
        OnPropertyChanged(nameof(IsSetNullPointActive));
        OnPropertyChanged(nameof(IsMoveActive));
        OnPropertyChanged(nameof(IsCopyActive));
        OnPropertyChanged(nameof(IsRotateActive));
        OnPropertyChanged(nameof(IsMirrorActive));
        OnPropertyChanged(nameof(IsScaleActive));
        OnPropertyChanged(nameof(IsOffsetActive));
        OnPropertyChanged(nameof(IsTrimActive));
        OnPropertyChanged(nameof(IsStretchActive));
        OnPropertyChanged(nameof(IsFilletActive));
        OnPropertyChanged(nameof(IsArrayActive));
    }

    partial void OnProjectNameChanged(string value) => OnPropertyChanged(nameof(Title));

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    partial void OnSnapEnabledChanged(bool value)
    {
        Tools.SnapSettings.Enabled = value;
        Tools.RequestRedraw();
    }

    partial void OnOrthoEnabledChanged(bool value)
    {
        Tools.InputSettings.OrthoEnabled = value;
        Tools.RequestRedraw();
    }

    partial void OnPolarEnabledChanged(bool value)
    {
        Tools.InputSettings.PolarEnabled = value;
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
    private void ActivateArc() => ActivateDrawingTool(_arcTool);

    [RelayCommand]
    private void ActivateEllipse() => ActivateDrawingTool(_ellipseTool);

    [RelayCommand]
    private void ActivatePolyline() => ActivateDrawingTool(_polylineTool);

    [RelayCommand]
    private void ActivateSpline() => ActivateDrawingTool(_splineTool);

    [RelayCommand]
    private void ActivatePoint() => ActivateDrawingTool(_pointTool);

    [RelayCommand]
    private void ActivateText() => ActivateDrawingTool(_textTool);

    [RelayCommand]
    private void ActivateLeader() => ActivateDrawingTool(_leaderTool);

    /// <summary>
    /// Double-click handler: if a text entity sits under <paramref name="world"/>, switch to the
    /// text tool and open its inline editor for editing (otherwise do nothing).
    /// </summary>
    public void EditTextAt(Point2D world)
    {
        if (PickTextEntity(world) is null)
            return;

        Tools.SetActiveTool(_textTool);
        _textTool.BeginEditAt(world);
    }

    private IEntity? PickTextEntity(Point2D world)
    {
        double tolerance = Tools.PickTolerance;
        IReadOnlyList<IEntity> entities = Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (entity is (TextEntity or MTextEntity)
                && Document.IsEntityEditable(entity)
                && entity.HitTest(world, tolerance))
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>Clears any Assistant style so the next entity uses the layer default again.</summary>
    private void ActivateDrawingTool(ITool tool)
    {
        Tools.DefaultStrokeOverride = null;
        Tools.SetActiveTool(tool);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateMove() => Tools.SetActiveTool(_moveTool);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateCopy() => Tools.SetActiveTool(_copyTool);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateRotate() => Tools.SetActiveTool(_rotateTool);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateMirror() => Tools.SetActiveTool(_mirrorTool);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateScale() => Tools.SetActiveTool(_scaleTool);

    /// <summary>Offset picks its own source object, so it needs no prior selection.</summary>
    [RelayCommand]
    private void ActivateOffset() => Tools.SetActiveTool(_offsetTool);

    /// <summary>Trim/Extend picks its target and cutting edges from the drawing directly.</summary>
    [RelayCommand]
    private void ActivateTrim() => Tools.SetActiveTool(_trimTool);

    /// <summary>Stretch captures its own crossing window, so it needs no prior selection.</summary>
    [RelayCommand]
    private void ActivateStretch() => Tools.SetActiveTool(_stretchTool);

    /// <summary>Fillet/Chamfer picks two lines; the radius comes from the inline options line.</summary>
    [RelayCommand]
    private void ActivateFillet() => Tools.SetActiveTool(_filletTool);

    /// <summary>Array repeats the current selection; needs something selected first.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ActivateArray() => Tools.SetActiveTool(_arrayTool);

    /// <summary>True while at least one entity is selected (gates the editing tools).</summary>
    private bool HasSelection => !Tools.Selection.IsEmpty;

    [RelayCommand]
    private void ActivateSetNullPoint() => Tools.SetActiveTool(_setNullPointTool);

    /// <summary>
    /// Inserts a raster underlay centered in the current view. <paramref name="aspectRatio"/> is
    /// width/height of the source image (computed by the view), sizing the placement box.
    /// </summary>
    public void InsertImageEntity(byte[] data, double aspectRatio)
    {
        if (data.Length == 0)
            return;

        double scale = Math.Max(Viewport.Scale, Viewport.MinScale);
        double visibleWidth = Viewport.Width > 0 ? Viewport.Width / scale : 1000.0;
        double width = visibleWidth > 0 ? visibleWidth * 0.5 : 1000.0;
        double height = aspectRatio > 0 ? width / aspectRatio : width;

        Point2D center = Viewport.Center;
        var origin = new Point2D(center.X - width / 2, center.Y - height / 2);
        var image = new ImageEntity(data, origin, width, height);
        _commands.Execute(new AddEntityCommand(Document, image));
        Tools.Selection.Set(image);
        StatusMessage = "Bild als Unterlage eingefügt.";
    }

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
            case ShortcutAction.Arc: ActivateArc(); return true;
            case ShortcutAction.Ellipse: ActivateEllipse(); return true;
            case ShortcutAction.Polyline: ActivatePolyline(); return true;
            case ShortcutAction.Spline: ActivateSpline(); return true;
            case ShortcutAction.Point: ActivatePoint(); return true;
            case ShortcutAction.Text: ActivateText(); return true;
            case ShortcutAction.Leader: ActivateLeader(); return true;
            case ShortcutAction.Move: ActivateMoveCommand.Execute(null); return true;
            case ShortcutAction.Copy: ActivateCopyCommand.Execute(null); return true;
            case ShortcutAction.Rotate: ActivateRotateCommand.Execute(null); return true;
            case ShortcutAction.Mirror: ActivateMirrorCommand.Execute(null); return true;
            case ShortcutAction.Scale: ActivateScaleCommand.Execute(null); return true;
            case ShortcutAction.Offset: ActivateOffsetCommand.Execute(null); return true;
            case ShortcutAction.Trim: ActivateTrimCommand.Execute(null); return true;
            case ShortcutAction.Stretch: ActivateStretchCommand.Execute(null); return true;
            case ShortcutAction.Fillet: ActivateFilletCommand.Execute(null); return true;
            case ShortcutAction.Array: ActivateArrayCommand.Execute(null); return true;
            case ShortcutAction.Delete: DeleteSelectionCommand.Execute(null); return true;
            case ShortcutAction.Undo: UndoCommand.Execute(null); return true;
            case ShortcutAction.Redo: RedoCommand.Execute(null); return true;
            case ShortcutAction.ZoomToFit: ZoomToFit(); return true;
            case ShortcutAction.ToggleSnap: ToggleSnap(); return true;
            case ShortcutAction.Ortho: OrthoEnabled = !OrthoEnabled; return true;
            case ShortcutAction.Polar: PolarEnabled = !PolarEnabled; return true;
            case ShortcutAction.SetNullPoint: ActivateSetNullPoint(); return true;
            default: return false; // file actions handled by the window
        }
    }
}
