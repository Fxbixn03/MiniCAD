using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Owns the active tool, the selection, and the bridge to the document and undo history.
/// It implements <see cref="IToolContext"/> itself and routes abstract input to the active
/// tool. The host (App) forwards translated pointer/key events and listens for redraw
/// requests; it never talks to individual tools directly.
/// </summary>
public sealed class ToolManager : IToolContext
{
    private const double ScreenPickPixels = 6.0;

    private readonly UndoRedoManager _commands;
    private readonly Viewport _viewport;
    private readonly SnapEngine _snapEngine;
    private readonly Dictionary<Type, ITool> _quickSelectTools = new();

    public ToolManager(ICadDocument document, UndoRedoManager commands, Viewport viewport)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _snapEngine = new SnapEngine(document, viewport, SnapSettings);

        Selection.Changed += (_, _) => RequestRedraw();
        Document.Changed += OnDocumentChanged;
    }

    public ICadDocument Document { get; }

    public Selection Selection { get; } = new();

    public SnapSettings SnapSettings { get; } = new();

    public InputSettings InputSettings { get; } = new();

    /// <summary>The tool returned to when a drawing tool is cancelled (the select tool).</summary>
    public ITool? DefaultTool { get; set; }

    public StrokeStyle? DefaultStrokeOverride { get; set; }

    /// <summary>When true, geometry drawn by tools is created as construction (Hilfskonstruktion).</summary>
    public bool ConstructionMode { get; set; }

    public ITool? ActiveTool { get; private set; }

    public string ActiveToolName => ActiveTool?.Name ?? "—";

    /// <summary>
    /// The most recent point fed to a tool (snapped mouse click or a typed coordinate). Used as
    /// the reference for relative coordinate entry ("vom letzten Punkt").
    /// </summary>
    public Point2D? LastPoint { get; private set; }

    /// <summary>A few screen pixels expressed in world units at the current zoom.</summary>
    public double PickTolerance => ScreenPickPixels / Math.Max(_viewport.Scale, Viewport.MinScale);

    public event EventHandler? ActiveToolChanged;

    public event EventHandler? RedrawRequested;

    public void Execute(IUndoableCommand command) => _commands.Execute(command);

    public void Push(IUndoableCommand command) => _commands.Push(command);

    public SnapResult Snap(Point2D world) => _snapEngine.Snap(world);

    public void RequestRedraw() => RedrawRequested?.Invoke(this, EventArgs.Empty);

    public void ReturnToDefaultTool()
    {
        if (DefaultTool is { } tool && !ReferenceEquals(tool, ActiveTool))
            SetActiveTool(tool);
    }

    public void SetActiveTool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (ReferenceEquals(tool, ActiveTool))
            return;

        ActiveTool?.Deactivate();
        ActiveTool = tool;
        tool.Activate(this);
        ActiveToolChanged?.Invoke(this, EventArgs.Empty);
        RequestRedraw();
    }

    /// <summary>
    /// Registers the create tool to activate when the user quick-selects (double right-click)
    /// an entity of type <typeparamref name="TEntity"/>.
    /// </summary>
    public void RegisterQuickSelectTool<TEntity>(ITool tool) where TEntity : IEntity
    {
        ArgumentNullException.ThrowIfNull(tool);
        _quickSelectTools[typeof(TEntity)] = tool;
    }

    public void PointerDown(in ToolPointerInput input)
    {
        // Allplan-style quick select: a double right-click adopts the clicked object's
        // properties and re-activates its matching create tool, regardless of the current tool.
        if (input.Button == ToolButton.Right && input.ClickCount >= 2 && TryQuickSelect(input.World))
            return;

        if (input.Button == ToolButton.Left)
            LastPoint = Snap(input.World).Point;

        ActiveTool?.PointerDown(input);
    }

    /// <summary>
    /// Places a point at an exact, typed world coordinate by feeding the active tool a synthetic
    /// left click. Snapping is suspended for the commit so the typed value is honoured precisely.
    /// </summary>
    public void CommitCoordinate(Point2D world)
    {
        bool wasEnabled = SnapSettings.Enabled;
        SnapSettings.Enabled = false;
        try
        {
            ActiveTool?.PointerDown(new ToolPointerInput
            {
                World = world,
                Button = ToolButton.Left,
                ClickCount = 1,
            });
        }
        finally
        {
            SnapSettings.Enabled = wasEnabled;
        }

        LastPoint = world;
        RequestRedraw();
    }

    public void PointerMove(in ToolPointerInput input) => ActiveTool?.PointerMove(input);

    public void PointerUp(in ToolPointerInput input) => ActiveTool?.PointerUp(input);

    public void KeyDown(ToolKey key) => ActiveTool?.KeyDown(key);

    /// <summary>Selection highlights plus the active tool's preview, drawn over the document.</summary>
    public IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>(Selection.Count + 1);
        foreach (IEntity entity in Selection.Items)
            items.Add(new OverlayItem(entity, ToolStyle.Selection));

        if (ActiveTool is { } tool)
            items.AddRange(tool.GetOverlay());

        return items;
    }

    private bool TryQuickSelect(Point2D world)
    {
        IEntity? entity = PickTopmost(world);
        if (entity is null)
            return false;

        // Adopt the object's structural properties for subsequent drawing.
        if (Document.FindLayer(entity.LayerId) is { } layer)
            Document.ActiveLayer = layer;
        if (Document.FindPartialDrawing(entity.PartialDrawingId) is { } partialDrawing)
            Document.ActivePartialDrawing = partialDrawing;

        if (_quickSelectTools.TryGetValue(entity.GetType(), out ITool? tool))
        {
            SetActiveTool(tool);
        }
        else
        {
            // No matching create tool (e.g. an arc) — just select it so the adoption is visible.
            Selection.Set(entity);
        }

        RequestRedraw();
        return true;
    }

    private IEntity? PickTopmost(Point2D world)
    {
        double tolerance = PickTolerance;
        IReadOnlyList<IEntity> entities = Document.Entities;

        for (int i = entities.Count - 1; i >= 0; i--)
        {
            IEntity entity = entities[i];
            if (Document.IsEntityEditable(entity) && entity.HitTest(world, tolerance))
                return entity;
        }

        return null;
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        // Keep the selection from referencing entities that left the document.
        if (e.Kind == DocumentChangeKind.EntityRemoved && e.Entity is { } removed)
            Selection.Remove(removed);
        else if (e.Kind is DocumentChangeKind.Cleared or DocumentChangeKind.Reloaded)
            Selection.Clear();
    }
}
