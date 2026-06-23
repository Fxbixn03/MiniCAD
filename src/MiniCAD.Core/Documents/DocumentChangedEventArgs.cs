using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Documents;

public enum DocumentChangeKind
{
    EntityAdded,
    EntityRemoved,
    EntityModified,
    LayerAdded,
    LayerRemoved,
    LayerModified,
    PartialDrawingAdded,
    PartialDrawingRemoved,
    PartialDrawingModified,
    Cleared,

    /// <summary>The project's hatch pattern set changed.</summary>
    PatternsChanged,

    /// <summary>The document's text styles changed (added/removed/edited).</summary>
    TextStylesChanged,

    /// <summary>The document's dimension styles changed (added/removed/edited).</summary>
    DimStylesChanged,

    /// <summary>The document's layer favorites changed (saved/removed).</summary>
    LayerFavoritesChanged,

    /// <summary>The document's block definitions changed (added/removed).</summary>
    BlocksChanged,

    /// <summary>The 3D model space changed (object added/removed/modified).</summary>
    ModelsChanged,

    /// <summary>The transient isolate/hide set changed (entities shown or hidden, #231).</summary>
    VisibilityChanged,

    /// <summary>The whole document was replaced (e.g. a project was loaded).</summary>
    Reloaded,
}

/// <summary>
/// Describes a single change to a document. The renderer and view models subscribe to
/// <see cref="ICadDocument.Changed"/> to invalidate or refresh in response.
/// </summary>
public sealed class DocumentChangedEventArgs : EventArgs
{
    public DocumentChangedEventArgs(
        DocumentChangeKind kind,
        IEntity? entity = null,
        Layer? layer = null,
        PartialDrawing? partialDrawing = null)
    {
        Kind = kind;
        Entity = entity;
        Layer = layer;
        PartialDrawing = partialDrawing;
    }

    public DocumentChangeKind Kind { get; }

    public IEntity? Entity { get; }

    public Layer? Layer { get; }

    public PartialDrawing? PartialDrawing { get; }

    public static DocumentChangedEventArgs ForEntity(DocumentChangeKind kind, IEntity entity) => new(kind, entity);

    public static DocumentChangedEventArgs ForLayer(DocumentChangeKind kind, Layer layer) => new(kind, layer: layer);

    public static DocumentChangedEventArgs ForPartialDrawing(DocumentChangeKind kind, PartialDrawing partialDrawing)
        => new(kind, partialDrawing: partialDrawing);
}
