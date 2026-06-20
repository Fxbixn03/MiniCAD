using MiniCAD.Core.Coordinates;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>
/// The in-memory CAD document. Entities are organized along two independent axes, both
/// Allplan-inspired: <see cref="Layer"/>s (cross-cutting display defaults) and
/// <see cref="PartialDrawing"/>s/Teilbilder (overlays that stack and toggle). Each entity
/// references one of each by id. Change notification keeps views and renderers in sync.
/// </summary>
public sealed class CadDocument : ICadDocument
{
    private readonly List<Layer> _layers = new();
    private readonly Dictionary<Guid, Layer> _layersById = new();
    private readonly List<PartialDrawing> _partialDrawings = new();
    private readonly Dictionary<Guid, PartialDrawing> _partialDrawingsById = new();
    private readonly List<IEntity> _entities = new();
    private readonly List<HatchPattern> _patterns = new();
    private readonly List<TextStyle> _textStyles = new();
    private readonly Dictionary<Guid, TextStyle> _textStylesById = new();

    public CadDocument()
    {
        DefaultLayer = new Layer("0", new StrokeStyle(Color.White, 1.0));
        RegisterLayer(DefaultLayer);
        ActiveLayer = DefaultLayer;

        DefaultPartialDrawing = new PartialDrawing("Teilbild 1");
        RegisterPartialDrawing(DefaultPartialDrawing);
        ActivePartialDrawing = DefaultPartialDrawing;

        DefaultTextStyle = new TextStyle("Standard");
        RegisterTextStyle(DefaultTextStyle);
        ActiveTextStyle = DefaultTextStyle;
    }

    public IReadOnlyList<Layer> Layers => _layers;

    public IReadOnlyList<PartialDrawing> PartialDrawings => _partialDrawings;

    public IReadOnlyList<IEntity> Entities => _entities;

    public Layer DefaultLayer { get; private set; }

    public Layer ActiveLayer { get; set; }

    public PartialDrawing DefaultPartialDrawing { get; private set; }

    public PartialDrawing ActivePartialDrawing { get; set; }

    public CoordinateSystem CoordinateSystem { get; } = new();

    public IReadOnlyList<HatchPattern> Patterns => _patterns;

    public IReadOnlyList<TextStyle> TextStyles => _textStyles;

    public TextStyle DefaultTextStyle { get; private set; }

    public TextStyle ActiveTextStyle { get; set; }

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    // ----- Entities -----

    public void AddEntity(IEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity.LayerId == Guid.Empty)
            entity.LayerId = ActiveLayer.Id;
        if (entity.PartialDrawingId == Guid.Empty)
            entity.PartialDrawingId = ActivePartialDrawing.Id;

        _entities.Add(entity);
        Raise(DocumentChangedEventArgs.ForEntity(DocumentChangeKind.EntityAdded, entity));
    }

    public bool RemoveEntity(IEntity entity)
    {
        if (!_entities.Remove(entity))
            return false;

        Raise(DocumentChangedEventArgs.ForEntity(DocumentChangeKind.EntityRemoved, entity));
        return true;
    }

    public void NotifyEntityModified(IEntity entity)
        => Raise(DocumentChangedEventArgs.ForEntity(DocumentChangeKind.EntityModified, entity));

    // ----- Layers -----

    public Layer AddLayer(string name, StrokeStyle stroke)
    {
        var layer = new Layer(name, stroke);
        RegisterLayer(layer);
        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerAdded, layer));
        return layer;
    }

    public bool RemoveLayer(Layer layer)
    {
        if (layer == DefaultLayer || !_layers.Remove(layer))
            return false;

        _layersById.Remove(layer.Id);

        // Re-home orphaned entities onto the default layer so nothing silently disappears.
        foreach (IEntity entity in _entities)
        {
            if (entity.LayerId == layer.Id)
                entity.LayerId = DefaultLayer.Id;
        }

        if (ActiveLayer == layer)
            ActiveLayer = DefaultLayer;

        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerRemoved, layer));
        return true;
    }

    public Layer? FindLayer(Guid id) => _layersById.GetValueOrDefault(id);

    /// <summary>
    /// Adds every <see cref="StandardLayers"/> definition that is not already present (matched
    /// by name, case-insensitively). Returns the number of layers actually created.
    /// </summary>
    public int AddStandardLayers()
    {
        int added = 0;
        foreach (StandardLayers.Definition definition in StandardLayers.All)
        {
            bool exists = _layers.Any(l => string.Equals(l.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
            if (exists)
                continue;

            AddLayer(definition.Name, definition.Stroke);
            added++;
        }

        return added;
    }

    public void SetLayerState(Layer layer, ElementState state)
    {
        if (layer.State == state)
            return;

        layer.State = state;
        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerModified, layer));
    }

    public void RenameLayer(Layer layer, string name)
    {
        layer.Name = name;
        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerModified, layer));
    }

    /// <summary>Sets a layer's color (keeping its width); entities using the layer re-color.</summary>
    public void SetLayerColor(Layer layer, Color color)
    {
        if (layer.Stroke.Color == color)
            return;

        layer.Stroke = layer.Stroke.WithColor(color);
        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerModified, layer));
    }

    // ----- Partial drawings (Teilbilder) -----

    public PartialDrawing AddPartialDrawing(string name)
    {
        var partialDrawing = new PartialDrawing(name);
        RegisterPartialDrawing(partialDrawing);
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingAdded, partialDrawing));
        return partialDrawing;
    }

    public PartialDrawing? FindPartialDrawing(Guid id) => _partialDrawingsById.GetValueOrDefault(id);

    public void SetPartialDrawingState(PartialDrawing partialDrawing, ElementState state)
    {
        if (partialDrawing.State == state)
            return;

        partialDrawing.State = state;
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingModified, partialDrawing));
    }

    /// <summary>
    /// The Allplan four-state status of a Teilbild: <see cref="PartialDrawingStatus.Current"/> for
    /// the active drawing target, <see cref="PartialDrawingStatus.Active"/> for other editable
    /// Teilbilder, <see cref="PartialDrawingStatus.Passive"/> for Locked (dimmed/snap-only) ones,
    /// and <see cref="PartialDrawingStatus.Off"/> for hidden ones.
    /// </summary>
    public PartialDrawingStatus GetPartialDrawingStatus(PartialDrawing partialDrawing) => partialDrawing.State switch
    {
        ElementState.Off => PartialDrawingStatus.Off,
        ElementState.Locked => PartialDrawingStatus.Passive,
        _ => ReferenceEquals(partialDrawing, ActivePartialDrawing)
            ? PartialDrawingStatus.Current
            : PartialDrawingStatus.Active,
    };

    /// <summary>
    /// Makes <paramref name="partialDrawing"/> the current (drawing-target) Teilbild. It is forced
    /// to <see cref="ElementState.Active"/> so the target is always editable, and a change is
    /// raised so views and the renderer refresh.
    /// </summary>
    public void SetCurrentPartialDrawing(PartialDrawing partialDrawing)
    {
        ArgumentNullException.ThrowIfNull(partialDrawing);
        if (partialDrawing.State != ElementState.Active)
            partialDrawing.State = ElementState.Active;

        ActivePartialDrawing = partialDrawing;
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingModified, partialDrawing));
    }

    /// <summary>Sets a Teilbild's reference scale denominator (e.g. 50 for 1:50) and notifies.</summary>
    public void SetPartialDrawingReferenceScale(PartialDrawing partialDrawing, double referenceScale)
    {
        if (partialDrawing.ReferenceScale == referenceScale)
            return;

        partialDrawing.ReferenceScale = referenceScale;
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingModified, partialDrawing));
    }

    public void RenamePartialDrawing(PartialDrawing partialDrawing, string name)
    {
        partialDrawing.Name = name;
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingModified, partialDrawing));
    }

    /// <summary>Moves a Teilbild within the stacking order by <paramref name="delta"/> positions.</summary>
    public void MovePartialDrawing(PartialDrawing partialDrawing, int delta)
    {
        int index = _partialDrawings.IndexOf(partialDrawing);
        if (index < 0)
            return;

        int target = Math.Clamp(index + delta, 0, _partialDrawings.Count - 1);
        if (target == index)
            return;

        _partialDrawings.RemoveAt(index);
        _partialDrawings.Insert(target, partialDrawing);
        Raise(DocumentChangedEventArgs.ForPartialDrawing(DocumentChangeKind.PartialDrawingModified, partialDrawing));
    }

    // ----- Text styles -----

    public TextStyle AddTextStyle(string name, string fontFamily = "", double height = 12.0, double widthFactor = 1.0)
    {
        var style = new TextStyle(name, fontFamily, height, widthFactor);
        RegisterTextStyle(style);
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.TextStylesChanged));
        return style;
    }

    public bool RemoveTextStyle(TextStyle style)
    {
        if (style == DefaultTextStyle || !_textStyles.Remove(style))
            return false;

        _textStylesById.Remove(style.Id);

        // Re-home texts that used the removed style onto the default style.
        foreach (IEntity entity in _entities)
        {
            if (entity is ITextEntity text && text.TextStyleId == style.Id)
                ApplyTextStyle(text, DefaultTextStyle);
        }

        if (ActiveTextStyle == style)
            ActiveTextStyle = DefaultTextStyle;

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.TextStylesChanged));
        return true;
    }

    public TextStyle? FindTextStyle(Guid id) => _textStylesById.GetValueOrDefault(id);

    public void RenameTextStyle(TextStyle style, string name)
    {
        style.Name = name;
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.TextStylesChanged));
    }

    /// <summary>
    /// Updates a style's font/height/width factor and re-applies them to every text assigned to
    /// it, so edits propagate to the drawing (the Allplan/AutoCAD text-style behaviour).
    /// </summary>
    public void UpdateTextStyle(TextStyle style, string fontFamily, double height, double widthFactor)
    {
        style.FontFamily = fontFamily ?? string.Empty;
        style.Height = height <= 0.0 ? 1.0 : height;
        style.WidthFactor = widthFactor <= 0.0 ? 1.0 : widthFactor;

        foreach (IEntity entity in _entities)
        {
            if (entity is ITextEntity text && text.TextStyleId == style.Id)
                ApplyTextStyle(text, style);
        }

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.TextStylesChanged));
    }

    /// <summary>Assigns <paramref name="style"/> to a text entity (adopting its look) and notifies.</summary>
    public void AssignTextStyle(IEntity entity, TextStyle style)
    {
        if (entity is not ITextEntity text)
            return;

        ApplyTextStyle(text, style);
        Raise(DocumentChangedEventArgs.ForEntity(DocumentChangeKind.EntityModified, entity));
    }

    /// <summary>Copies a style's appearance (font, height, width factor) and link onto a text entity.</summary>
    public static void ApplyTextStyle(ITextEntity text, TextStyle style)
    {
        text.TextStyleId = style.Id;
        text.FontFamily = style.FontFamily;
        text.Height = style.Height;
        text.WidthFactor = style.WidthFactor;
    }

    private void RegisterTextStyle(TextStyle style)
    {
        _textStyles.Add(style);
        _textStylesById[style.Id] = style;
    }

    // ----- Patterns (Muster) -----

    /// <summary>Adds a project-specific hatch pattern.</summary>
    public HatchPattern AddPattern(HatchPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _patterns.Add(pattern);
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.PatternsChanged));
        return pattern;
    }

    public bool RemovePattern(HatchPattern pattern)
    {
        if (!_patterns.Remove(pattern))
            return false;

        // Drop the fill from any entity that referenced the removed pattern.
        foreach (IEntity entity in _entities)
        {
            if (entity is PolylineEntity { Fill: { } fill } poly && fill.Id == pattern.Id)
                poly.Fill = null;
        }

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.PatternsChanged));
        return true;
    }

    /// <summary>Resolves a pattern id against the project patterns first, then the global library.</summary>
    public HatchPattern? FindPattern(Guid id)
    {
        foreach (HatchPattern pattern in _patterns)
        {
            if (pattern.Id == id)
                return pattern;
        }

        return PatternLibrary.Find(id);
    }

    // ----- Queries -----

    public bool IsEntityVisible(IEntity entity)
    {
        if (FindLayer(entity.LayerId) is { IsVisible: false })
            return false;
        if (FindPartialDrawing(entity.PartialDrawingId) is { IsVisible: false })
            return false;
        return true;
    }

    public bool IsEntityEditable(IEntity entity)
    {
        if (FindLayer(entity.LayerId) is { IsEditable: false })
            return false;
        if (FindPartialDrawing(entity.PartialDrawingId) is { IsEditable: false })
            return false;
        return true;
    }

    public StrokeStyle ResolveStroke(IEntity entity)
    {
        if (entity.StrokeOverride is { } @override)
            return @override;

        Layer layer = FindLayer(entity.LayerId) ?? DefaultLayer;
        return layer.Stroke;
    }

    public Rect2D? GetContentBounds()
    {
        Rect2D? bounds = null;
        foreach (IEntity entity in _entities)
        {
            if (!IsEntityVisible(entity))
                continue;

            bounds = bounds is { } current ? current.Union(entity.Bounds) : entity.Bounds;
        }

        return bounds;
    }

    public void Clear()
    {
        if (_entities.Count == 0)
            return;

        _entities.Clear();
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.Cleared));
    }

    /// <summary>
    /// Replaces all layers, partial drawings and entities in place (used when loading a
    /// project). Reusing the same instance keeps every existing reference valid; a single
    /// <see cref="DocumentChangeKind.Reloaded"/> event signals the swap.
    /// </summary>
    public void LoadContents(DocumentContents contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (contents.Layers.Count == 0)
            throw new ArgumentException("A document must have at least one layer.", nameof(contents));
        if (contents.PartialDrawings.Count == 0)
            throw new ArgumentException("A document must have at least one Teilbild.", nameof(contents));

        _layers.Clear();
        _layersById.Clear();
        foreach (Layer layer in contents.Layers)
            RegisterLayer(layer);
        DefaultLayer = FindLayer(contents.DefaultLayerId) ?? _layers[0];
        ActiveLayer = FindLayer(contents.ActiveLayerId) ?? DefaultLayer;

        _partialDrawings.Clear();
        _partialDrawingsById.Clear();
        foreach (PartialDrawing partialDrawing in contents.PartialDrawings)
            RegisterPartialDrawing(partialDrawing);
        DefaultPartialDrawing = _partialDrawings[0];
        ActivePartialDrawing = FindPartialDrawing(contents.ActivePartialDrawingId) ?? DefaultPartialDrawing;

        _patterns.Clear();
        _patterns.AddRange(contents.Patterns);

        _textStyles.Clear();
        _textStylesById.Clear();
        foreach (TextStyle style in contents.TextStyles)
            RegisterTextStyle(style);
        if (_textStyles.Count == 0)
            RegisterTextStyle(new TextStyle("Standard"));
        DefaultTextStyle = FindTextStyle(contents.DefaultTextStyleId) ?? _textStyles[0];
        ActiveTextStyle = FindTextStyle(contents.ActiveTextStyleId) ?? DefaultTextStyle;

        _entities.Clear();
        foreach (IEntity entity in contents.Entities)
            _entities.Add(entity);

        CoordinateSystem.Origin = contents.Origin;

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.Reloaded));
    }

    private void RegisterLayer(Layer layer)
    {
        _layers.Add(layer);
        _layersById[layer.Id] = layer;
    }

    private void RegisterPartialDrawing(PartialDrawing partialDrawing)
    {
        _partialDrawings.Add(partialDrawing);
        _partialDrawingsById[partialDrawing.Id] = partialDrawing;
    }

    private void Raise(DocumentChangedEventArgs args) => Changed?.Invoke(this, args);
}
