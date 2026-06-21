using MiniCAD.Core.Coordinates;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;
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
    private readonly List<DimStyle> _dimStyles = new();
    private readonly Dictionary<Guid, DimStyle> _dimStylesById = new();
    private readonly List<LayerFavorite> _layerFavorites = new();
    private readonly List<BlockDefinition> _blockDefinitions = new();
    private readonly Dictionary<Guid, BlockDefinition> _blockDefinitionsById = new();
    private readonly List<Model3DObject> _models = new();

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

        DefaultDimStyle = new DimStyle("Standard");
        RegisterDimStyle(DefaultDimStyle);
        ActiveDimStyle = DefaultDimStyle;
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

    public IReadOnlyList<DimStyle> DimStyles => _dimStyles;

    public DimStyle DefaultDimStyle { get; private set; }

    public DimStyle ActiveDimStyle { get; set; }

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

    /// <summary>Sets a layer's default line type; entities using the layer restyle.</summary>
    public void SetLayerLineType(Layer layer, LineType lineType)
    {
        if (layer.Stroke.LineType == lineType)
            return;

        layer.Stroke = layer.Stroke.WithLineType(lineType);
        Raise(DocumentChangedEventArgs.ForLayer(DocumentChangeKind.LayerModified, layer));
    }

    /// <summary>Sets a layer's default plot line weight in mm (0 = pixel width).</summary>
    public void SetLayerLineWeight(Layer layer, double lineWeightMm)
    {
        if (layer.Stroke.LineWeightMm.Equals(lineWeightMm))
            return;

        layer.Stroke = layer.Stroke.WithLineWeight(lineWeightMm);
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

    /// <summary>Sets a Teilbild's base elevation and default extrusion height (for 3D), then notifies.</summary>
    public void SetPartialDrawingHeights(PartialDrawing partialDrawing, double baseHeight, double height)
    {
        if (partialDrawing.BaseHeight.Equals(baseHeight) && partialDrawing.Height.Equals(height))
            return;

        partialDrawing.BaseHeight = baseHeight;
        partialDrawing.Height = height;
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

    // ----- Layer favorites (saved layer-state sets) -----

    public IReadOnlyList<LayerFavorite> LayerFavorites => _layerFavorites;

    /// <summary>Captures the current state of every layer as a new named favorite.</summary>
    public LayerFavorite SaveLayerFavorite(string name)
    {
        var favorite = new LayerFavorite(name);
        foreach (Layer layer in _layers)
            favorite.States[layer.Id] = layer.State;

        _layerFavorites.Add(favorite);
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.LayerFavoritesChanged));
        return favorite;
    }

    /// <summary>Restores the states stored in <paramref name="favorite"/> for layers that still exist.</summary>
    public void ApplyLayerFavorite(LayerFavorite favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        foreach ((Guid layerId, ElementState state) in favorite.States)
        {
            if (FindLayer(layerId) is { } layer)
                SetLayerState(layer, state);
        }
    }

    public bool RemoveLayerFavorite(LayerFavorite favorite)
    {
        if (!_layerFavorites.Remove(favorite))
            return false;

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.LayerFavoritesChanged));
        return true;
    }

    // ----- Block definitions -----

    public IReadOnlyList<BlockDefinition> BlockDefinitions => _blockDefinitions;

    public BlockDefinition AddBlockDefinition(BlockDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _blockDefinitions.Add(definition);
        _blockDefinitionsById[definition.Id] = definition;
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.BlocksChanged));
        return definition;
    }

    public BlockDefinition? FindBlockDefinition(Guid id) => _blockDefinitionsById.GetValueOrDefault(id);

    public bool RemoveBlockDefinition(BlockDefinition definition)
    {
        if (!_blockDefinitions.Remove(definition))
            return false;

        _blockDefinitionsById.Remove(definition.Id);

        // Drop instances of the removed definition so nothing references a missing block.
        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            if (_entities[i] is BlockReferenceEntity reference && reference.DefinitionId == definition.Id)
                _entities.RemoveAt(i);
        }

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.BlocksChanged));
        return true;
    }

    private void RegisterBlockDefinition(BlockDefinition definition)
    {
        _blockDefinitions.Add(definition);
        _blockDefinitionsById[definition.Id] = definition;
    }

    // ----- 3D model space (parallel to the 2D Teilbilder) -----

    public IReadOnlyList<Model3DObject> Models => _models;

    public Model3DObject AddModelObject(Model3DObject model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _models.Add(model);
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.ModelsChanged));
        return model;
    }

    public bool RemoveModelObject(Model3DObject model)
    {
        if (!_models.Remove(model))
            return false;

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.ModelsChanged));
        return true;
    }

    public void NotifyModelModified() => Raise(new DocumentChangedEventArgs(DocumentChangeKind.ModelsChanged));

    /// <summary>The combined world bounds of all 3D objects, or <c>null</c> if the model is empty.</summary>
    public BoundingBox3D? GetModelBounds()
    {
        BoundingBox3D? bounds = null;
        foreach (Model3DObject model in _models)
            bounds = bounds is { } b ? b.Union(model.Bounds) : model.Bounds;
        return bounds;
    }

    // ----- Dimension styles -----

    public DimStyle AddDimStyle(string name)
    {
        var style = new DimStyle(name);
        RegisterDimStyle(style);
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.DimStylesChanged));
        return style;
    }

    public bool RemoveDimStyle(DimStyle style)
    {
        if (style == DefaultDimStyle || !_dimStyles.Remove(style))
            return false;

        _dimStylesById.Remove(style.Id);

        foreach (IEntity entity in _entities)
        {
            if (entity is DimensionEntity dim && dim.DimStyleId == style.Id)
                ApplyDimStyle(dim, DefaultDimStyle);
        }

        if (ActiveDimStyle == style)
            ActiveDimStyle = DefaultDimStyle;

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.DimStylesChanged));
        return true;
    }

    public DimStyle? FindDimStyle(Guid id) => _dimStylesById.GetValueOrDefault(id);

    public void RenameDimStyle(DimStyle style, string name)
    {
        style.Name = name;
        Raise(new DocumentChangedEventArgs(DocumentChangeKind.DimStylesChanged));
    }

    /// <summary>Updates a dim style's appearance and re-applies it to every assigned dimension.</summary>
    public void UpdateDimStyle(DimStyle style, double textHeight, double arrowSize,
        double extensionOffset, double extensionOvershoot, int decimalPlaces)
    {
        style.TextHeight = textHeight <= 0.0 ? 1.0 : textHeight;
        style.ArrowSize = arrowSize < 0.0 ? 0.0 : arrowSize;
        style.ExtensionOffset = extensionOffset < 0.0 ? 0.0 : extensionOffset;
        style.ExtensionOvershoot = extensionOvershoot < 0.0 ? 0.0 : extensionOvershoot;
        style.DecimalPlaces = decimalPlaces < 0 ? 0 : decimalPlaces;

        foreach (IEntity entity in _entities)
        {
            if (entity is DimensionEntity dim && dim.DimStyleId == style.Id)
                ApplyDimStyle(dim, style);
        }

        Raise(new DocumentChangedEventArgs(DocumentChangeKind.DimStylesChanged));
    }

    /// <summary>Assigns <paramref name="style"/> to a dimension (adopting its look) and notifies.</summary>
    public void AssignDimStyle(IEntity entity, DimStyle style)
    {
        if (entity is not DimensionEntity dim)
            return;

        ApplyDimStyle(dim, style);
        Raise(DocumentChangedEventArgs.ForEntity(DocumentChangeKind.EntityModified, entity));
    }

    /// <summary>Copies a dim style's appearance and link onto a dimension entity.</summary>
    public static void ApplyDimStyle(DimensionEntity dim, DimStyle style)
    {
        dim.DimStyleId = style.Id;
        dim.TextHeight = style.TextHeight;
        dim.ArrowSize = style.ArrowSize;
        dim.ExtensionOffset = style.ExtensionOffset;
        dim.ExtensionOvershoot = style.ExtensionOvershoot;
        dim.DecimalPlaces = style.DecimalPlaces;
    }

    private void RegisterDimStyle(DimStyle style)
    {
        _dimStyles.Add(style);
        _dimStylesById[style.Id] = style;
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

        _dimStyles.Clear();
        _dimStylesById.Clear();
        foreach (DimStyle style in contents.DimStyles)
            RegisterDimStyle(style);
        if (_dimStyles.Count == 0)
            RegisterDimStyle(new DimStyle("Standard"));
        DefaultDimStyle = FindDimStyle(contents.DefaultDimStyleId) ?? _dimStyles[0];
        ActiveDimStyle = FindDimStyle(contents.ActiveDimStyleId) ?? DefaultDimStyle;

        _layerFavorites.Clear();
        _layerFavorites.AddRange(contents.LayerFavorites);

        _blockDefinitions.Clear();
        _blockDefinitionsById.Clear();
        foreach (BlockDefinition definition in contents.BlockDefinitions)
            RegisterBlockDefinition(definition);

        _models.Clear();
        _models.AddRange(contents.Models);

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
