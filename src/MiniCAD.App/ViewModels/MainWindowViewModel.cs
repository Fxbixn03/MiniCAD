using System;
using System.Collections.Generic;
using System.ComponentModel;
using MiniCAD.Core.Coordinates;
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
using MiniCAD.Core.Model3D;
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
    private readonly LinearDimensionTool _linearDimensionTool = new();
    private readonly AngularDimensionTool _angularDimensionTool = new();
    private readonly RadialDimensionTool _radialDimensionTool = new();
    private readonly ElevationDimensionTool _elevationDimensionTool = new();
    private readonly OrdinateDimensionTool _ordinateDimensionTool = new();
    private readonly BlockInsertTool _blockInsertTool = new();
    private readonly ParametricInsertTool _parametricInsertTool = new();
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
        DimensionOptions = new DimensionOptionsViewModel(_linearDimensionTool);
        RadialDimensionOptions = new RadialDimensionOptionsViewModel(_radialDimensionTool);
        ElevationDimensionOptions = new ElevationDimensionOptionsViewModel(_elevationDimensionTool);
        TextStyles = new TextStylesViewModel(Document);
        Blocks = new BlocksViewModel(Document, Tools.Selection, _commands, Tools, _blockInsertTool);
        Library = new LibraryViewModel(Document, Tools, _blockInsertTool);
        SmartSymbols = new SmartSymbolViewModel(Tools, _parametricInsertTool);

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
            ConvertConstructionCommand.NotifyCanExecuteChanged();
            GroupSelectionCommand.NotifyCanExecuteChanged();
            UngroupSelectionCommand.NotifyCanExecuteChanged();
            ExtrudeSelectionCommand.NotifyCanExecuteChanged();
            RevolveSelectionCommand.NotifyCanExecuteChanged();
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
        Tools.RegisterQuickSelectTool<LinearDimensionEntity>(_linearDimensionTool);
        Tools.RegisterQuickSelectTool<AngularDimensionEntity>(_angularDimensionTool);
        Tools.RegisterQuickSelectTool<RadialDimensionEntity>(_radialDimensionTool);
        Tools.RegisterQuickSelectTool<ElevationDimensionEntity>(_elevationDimensionTool);
        Tools.RegisterQuickSelectTool<OrdinateDimensionEntity>(_ordinateDimensionTool);
        Tools.RegisterQuickSelectTool<BlockReferenceEntity>(_blockInsertTool);
        Tools.RegisterQuickSelectTool<ParametricSymbolEntity>(_parametricInsertTool);

        Tools.SetActiveTool(_selectTool);
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ShowGrid))
            Tools.SnapSettings.SnapToGrid = Settings.ShowGrid;
    }

    public CadDocument Document { get; }

    public Viewport Viewport { get; } = new();

    /// <summary>The shared 3D camera for the model-space view(s).</summary>
    public Camera3D Camera3D { get; } = new();

    /// <summary>The 3D view's render mode (wireframe / hidden-line / shaded).</summary>
    [ObservableProperty]
    private Render3DMode _view3DMode = Render3DMode.Shaded;

    [RelayCommand]
    private void SetRenderMode(string mode) => View3DMode = mode switch
    {
        "Wireframe" => Render3DMode.Wireframe,
        "HiddenLine" => Render3DMode.HiddenLine,
        _ => Render3DMode.Shaded,
    };

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

    /// <summary>Inline orientation selector, shown while the linear dimension tool is active.</summary>
    public DimensionOptionsViewModel DimensionOptions { get; }

    /// <summary>Inline radius/diameter toggle, shown while the radial dimension tool is active.</summary>
    public RadialDimensionOptionsViewModel RadialDimensionOptions { get; }

    /// <summary>Inline Z value, shown while the elevation (Höhenkote) tool is active.</summary>
    public ElevationDimensionOptionsViewModel ElevationDimensionOptions { get; }

    /// <summary>The text-style management panel (Textstile tab).</summary>
    public TextStylesViewModel TextStyles { get; }

    /// <summary>The block library panel (Blöcke tab).</summary>
    public BlocksViewModel Blocks { get; }

    /// <summary>The cross-project symbol library panel (Bibliothek tab).</summary>
    public LibraryViewModel Library { get; }

    /// <summary>The parametric symbol (Smartsymbole) palette.</summary>
    public SmartSymbolViewModel SmartSymbols { get; }

    public bool IsBlockInsertActive => Tools.ActiveTool == _blockInsertTool;

    /// <summary>Raised when the text tool wants the view to open its inline editor.</summary>
    public event Action<TextEditRequest>? TextEditRequested;

    /// <summary>True for tools that place points, where typed coordinate entry is meaningful.</summary>
    private bool IsCoordinateTool(ITool? tool)
        => tool == _lineTool || tool == _rectangleTool || tool == _circleTool
        || tool == _arcTool || tool == _ellipseTool || tool == _polylineTool || tool == _splineTool
        || tool == _pointTool || tool == _textTool || tool == _leaderTool
        || tool == _linearDimensionTool || tool == _angularDimensionTool
        || tool == _elevationDimensionTool || tool == _ordinateDimensionTool
        || tool == _blockInsertTool || tool == _parametricInsertTool || tool == _setNullPointTool
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

    /// <summary>Two-way bound to the construction toggle: new geometry is drawn as Hilfskonstruktion.</summary>
    [ObservableProperty]
    private bool _constructionMode;

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
    public bool IsLinearDimensionActive => Tools.ActiveTool == _linearDimensionTool;
    public bool IsAngularDimensionActive => Tools.ActiveTool == _angularDimensionTool;
    public bool IsRadialDimensionActive => Tools.ActiveTool == _radialDimensionTool;
    public bool IsElevationDimensionActive => Tools.ActiveTool == _elevationDimensionTool;
    public bool IsOrdinateDimensionActive => Tools.ActiveTool == _ordinateDimensionTool;
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
        OnPropertyChanged(nameof(IsLinearDimensionActive));
        OnPropertyChanged(nameof(IsAngularDimensionActive));
        OnPropertyChanged(nameof(IsRadialDimensionActive));
        OnPropertyChanged(nameof(IsElevationDimensionActive));
        OnPropertyChanged(nameof(IsOrdinateDimensionActive));
        OnPropertyChanged(nameof(IsBlockInsertActive));
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

    partial void OnConstructionModeChanged(bool value)
    {
        Tools.ConstructionMode = value;
        StatusMessage = value ? "Hilfskonstruktion: neue Elemente werden als Konstruktion gezeichnet." : string.Empty;
    }

    /// <summary>Groups the selected entities so they select and move as a unit.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void GroupSelection()
    {
        if (Tools.Selection.Count < 2)
            return;

        var groupId = Guid.NewGuid();
        var commands = Tools.Selection.Items.Select(entity =>
        {
            IEntity captured = entity;
            Guid old = captured.GroupId;
            return (IUndoableCommand)new SetEntityPropertyCommand("Gruppieren", Document, captured,
                () => captured.GroupId = groupId, () => captured.GroupId = old);
        }).ToList();

        _commands.Execute(new CompositeCommand("Gruppieren", commands));
    }

    /// <summary>Removes the selected entities from their group.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void UngroupSelection()
    {
        var commands = Tools.Selection.Items
            .Where(e => e.GroupId != Guid.Empty)
            .Select(entity =>
            {
                IEntity captured = entity;
                Guid old = captured.GroupId;
                return (IUndoableCommand)new SetEntityPropertyCommand("Gruppierung aufheben", Document, captured,
                    () => captured.GroupId = Guid.Empty, () => captured.GroupId = old);
            }).ToList();

        if (commands.Count == 0)
            return;

        _commands.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Gruppierung aufheben", commands));
    }

    /// <summary>Toggles the selected entities between normal geometry and Hilfskonstruktion.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ConvertConstruction()
    {
        if (Tools.Selection.IsEmpty)
            return;

        // If every selected entity is already construction, convert back; otherwise convert to it.
        bool target = !Tools.Selection.Items.All(e => e.IsConstruction);

        var commands = Tools.Selection.Items
            .Where(e => e.IsConstruction != target)
            .Select(entity =>
            {
                IEntity captured = entity;
                return (IUndoableCommand)new SetEntityPropertyCommand(
                    "Hilfskonstruktion umschalten", Document, captured,
                    () => captured.IsConstruction = target,
                    () => captured.IsConstruction = !target);
            })
            .ToList();

        if (commands.Count == 0)
            return;

        _commands.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand("Hilfskonstruktion umschalten", commands));
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

    [RelayCommand]
    private void ActivateLinearDimension() => ActivateDrawingTool(_linearDimensionTool);

    [RelayCommand]
    private void ActivateAngularDimension() => ActivateDrawingTool(_angularDimensionTool);

    [RelayCommand]
    private void ActivateRadialDimension() => ActivateDrawingTool(_radialDimensionTool);

    [RelayCommand]
    private void ActivateElevationDimension() => ActivateDrawingTool(_elevationDimensionTool);

    [RelayCommand]
    private void ActivateOrdinateDimension() => ActivateDrawingTool(_ordinateDimensionTool);

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

    /// <summary>Adds a primitive 3D body to the model space (Box/Cylinder/Sphere/Cone).</summary>
    [RelayCommand]
    private void InsertPrimitive3D(string kind)
    {
        (Mesh3D mesh, string name) = kind switch
        {
            "Cylinder" => (Mesh3D.Cylinder(500, 1000), "Zylinder"),
            "Sphere" => (Mesh3D.Sphere(500), "Kugel"),
            "Cone" => (Mesh3D.Cone(500, 1000), "Kegel"),
            _ => (Mesh3D.Box(1000, 1000, 1000), "Quader"),
        };

        Document.AddModelObject(new Model3DObject(mesh, name) { Color = new Core.Styling.Color(120, 200, 255) });
        if (Document.GetModelBounds() is { } bounds)
            Camera3D.ZoomToFit(bounds);
        StatusMessage = $"3D-Körper „{name}“ eingefügt – zur 3D-Ansicht wechseln.";
    }

    /// <summary>Extrudes the selected closed 2D profile into a 3D solid using the Teilbild height.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ExtrudeSelection()
    {
        if (Tools.Selection.Count != 1)
            return;
        if (ProfileExtractor.FromEntity(Tools.Selection.Items[0]) is not { } profile)
        {
            StatusMessage = "Zum Extrudieren ein geschlossenes Objekt (Polylinie/Kreis/Ellipse) wählen.";
            return;
        }

        PartialDrawing pd = Document.ActivePartialDrawing;
        double height = pd.Height > 0 ? pd.Height : 1000.0;
        Mesh3D mesh = Extruder.Extrude(profile, pd.BaseHeight, height);
        Document.AddModelObject(new Model3DObject(mesh, "Extrusion") { Color = new Core.Styling.Color(150, 210, 150) });
        if (Document.GetModelBounds() is { } bounds)
            Camera3D.ZoomToFit(bounds);
        StatusMessage = $"Profil extrudiert (H {height:0.##}) – zur 3D-Ansicht wechseln.";
    }

    /// <summary>Revolves the selected closed 2D profile a full turn around the Y axis into a solid.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RevolveSelection()
    {
        if (Tools.Selection.Count != 1)
            return;
        if (ProfileExtractor.FromEntity(Tools.Selection.Items[0]) is not { } profile)
        {
            StatusMessage = "Zum Rotieren ein geschlossenes Profil wählen.";
            return;
        }

        Mesh3D mesh = Revolver.Revolve(profile);
        Document.AddModelObject(new Model3DObject(mesh, "Rotationskörper") { Color = new Core.Styling.Color(210, 180, 140) });
        if (Document.GetModelBounds() is { } bounds)
            Camera3D.ZoomToFit(bounds);
        StatusMessage = "Profil rotiert (Revolve) – zur 3D-Ansicht wechseln.";
    }

    /// <summary>
    /// Combines the two model-space bodies with a boolean operation (#126: Union/Subtract/Intersect),
    /// replacing them with the single result solid.
    /// </summary>
    [RelayCommand]
    private void BooleanOp(string op)
    {
        if (Document.Models.Count != 2)
        {
            StatusMessage = "Boolesche Operation: es müssen genau zwei 3D-Körper vorhanden sein.";
            return;
        }

        Model3DObject a = Document.Models[0];
        Model3DObject b = Document.Models[1];
        Mesh3D wa = a.WorldMesh();
        Mesh3D wb = b.WorldMesh();

        (Mesh3D result, string name) = op switch
        {
            "Subtract" => (Csg.Subtract(wa, wb), "Differenz"),
            "Intersect" => (Csg.Intersect(wa, wb), "Schnittmenge"),
            _ => (Csg.Union(wa, wb), "Vereinigung"),
        };

        Document.RemoveModelObject(a);
        Document.RemoveModelObject(b);
        Document.AddModelObject(new Model3DObject(result, name) { Color = a.Color });
        if (Document.GetModelBounds() is { } bounds)
            Camera3D.ZoomToFit(bounds);
        StatusMessage = $"Boolesche Operation: {name} erzeugt.";
    }

    /// <summary>Sets the 3D camera to a standard view (0=Iso, 1=Top, 2=Front, 3=Right).</summary>
    [RelayCommand]
    private void SetView3D(string view)
    {
        Camera3D.SetStandardView(view switch
        {
            "Top" => StandardView.Top,
            "Front" => StandardView.Front,
            "Right" => StandardView.Right,
            _ => StandardView.Iso,
        });
        if (Document.GetModelBounds() is { } bounds)
            Camera3D.ZoomToFit(bounds);
    }

    /// <summary>Sets the active work plane (UCS) used for planar operations.</summary>
    [RelayCommand]
    private void SetWorkPlane(string plane)
    {
        Document.ActiveWorkPlane = plane switch
        {
            "Front" => WorkPlane.Front,
            "Right" => WorkPlane.Right,
            _ => WorkPlane.Top,
        };
        StatusMessage = $"Arbeitsebene: {plane}";
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
            case ShortcutAction.LinearDimension: ActivateLinearDimension(); return true;
            case ShortcutAction.AngularDimension: ActivateAngularDimension(); return true;
            case ShortcutAction.RadialDimension: ActivateRadialDimension(); return true;
            case ShortcutAction.ElevationDimension: ActivateElevationDimension(); return true;
            case ShortcutAction.OrdinateDimension: ActivateOrdinateDimension(); return true;
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
            case ShortcutAction.Group: GroupSelectionCommand.Execute(null); return true;
            case ShortcutAction.Ungroup: UngroupSelectionCommand.Execute(null); return true;
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
