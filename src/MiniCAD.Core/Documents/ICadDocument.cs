using MiniCAD.Core.Coordinates;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>
/// The document abstraction the rest of the system programs against. Keeping mutation and
/// notification behind an interface lets commands, renderers and tools stay decoupled from
/// the concrete <see cref="CadDocument"/> implementation.
/// </summary>
public interface ICadDocument
{
    IReadOnlyList<Layer> Layers { get; }

    /// <summary>The partial drawings (Teilbilder), in stacking order.</summary>
    IReadOnlyList<PartialDrawing> PartialDrawings { get; }

    IReadOnlyList<IEntity> Entities { get; }

    /// <summary>The layer assigned to newly added entities that don't specify one.</summary>
    Layer ActiveLayer { get; set; }

    /// <summary>The always-present fallback layer ("0").</summary>
    Layer DefaultLayer { get; }

    /// <summary>The Teilbild new entities are drawn into.</summary>
    PartialDrawing ActivePartialDrawing { get; set; }

    /// <summary>The reference frame carrying the user-defined origin (Nullpunkt).</summary>
    CoordinateSystem CoordinateSystem { get; }

    /// <summary>The hatch patterns defined for this project (in addition to the global library).</summary>
    IReadOnlyList<HatchPattern> Patterns { get; }

    /// <summary>The named text styles defined for this document.</summary>
    IReadOnlyList<TextStyle> TextStyles { get; }

    /// <summary>The text style applied to newly created text.</summary>
    TextStyle ActiveTextStyle { get; set; }

    TextStyle? FindTextStyle(Guid id);

    /// <summary>The block definitions (reusable symbols) defined for this document.</summary>
    IReadOnlyList<BlockDefinition> BlockDefinitions { get; }

    BlockDefinition? FindBlockDefinition(Guid id);

    /// <summary>The 3D model-space objects (parallel to the 2D Teilbilder).</summary>
    IReadOnlyList<Model3D.Model3DObject> Models { get; }

    /// <summary>The named dimension styles defined for this document.</summary>
    IReadOnlyList<DimStyle> DimStyles { get; }

    /// <summary>The dimension style applied to newly created dimensions.</summary>
    DimStyle ActiveDimStyle { get; set; }

    DimStyle? FindDimStyle(Guid id);

    /// <summary>Looks up a pattern by id across the project patterns and the global library.</summary>
    HatchPattern? FindPattern(Guid id);

    /// <summary>Raised after every structural change. See <see cref="DocumentChangeKind"/>.</summary>
    event EventHandler<DocumentChangedEventArgs>? Changed;

    void AddEntity(IEntity entity);

    bool RemoveEntity(IEntity entity);

    /// <summary>Signals that an entity's geometry changed in place (e.g. after a transform).</summary>
    void NotifyEntityModified(IEntity entity);

    Layer AddLayer(string name, StrokeStyle stroke);

    bool RemoveLayer(Layer layer);

    Layer? FindLayer(Guid id);

    PartialDrawing? FindPartialDrawing(Guid id);

    /// <summary>True if the entity's layer and Teilbild are both visible (Active or Locked).</summary>
    bool IsEntityVisible(IEntity entity);

    /// <summary>True if the entity's layer and Teilbild are both editable (Active).</summary>
    bool IsEntityEditable(IEntity entity);

    /// <summary>Resolves the effective stroke for an entity: override → layer → default.</summary>
    StrokeStyle ResolveStroke(IEntity entity);

    /// <summary>The union of all visible entity bounds, or <c>null</c> if there is nothing to show.</summary>
    Rect2D? GetContentBounds();

    void Clear();
}
