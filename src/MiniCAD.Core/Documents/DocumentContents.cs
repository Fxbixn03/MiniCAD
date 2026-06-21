using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>
/// A full snapshot of a document's structure, used to load a document in place via
/// <see cref="CadDocument.LoadContents"/>.
/// </summary>
public sealed record DocumentContents(
    IReadOnlyList<Layer> Layers,
    Guid DefaultLayerId,
    Guid ActiveLayerId,
    IReadOnlyList<PartialDrawing> PartialDrawings,
    Guid ActivePartialDrawingId,
    IReadOnlyList<IEntity> Entities)
{
    /// <summary>Project-specific hatch patterns (the global library is always available too).</summary>
    public IReadOnlyList<HatchPattern> Patterns { get; init; } = Array.Empty<HatchPattern>();

    /// <summary>The document's named text styles.</summary>
    public IReadOnlyList<TextStyle> TextStyles { get; init; } = Array.Empty<TextStyle>();

    public Guid DefaultTextStyleId { get; init; }

    public Guid ActiveTextStyleId { get; init; }

    /// <summary>The document's named dimension styles.</summary>
    public IReadOnlyList<DimStyle> DimStyles { get; init; } = Array.Empty<DimStyle>();

    public Guid DefaultDimStyleId { get; init; }

    public Guid ActiveDimStyleId { get; init; }

    /// <summary>Saved layer-state favorites.</summary>
    public IReadOnlyList<LayerFavorite> LayerFavorites { get; init; } = Array.Empty<LayerFavorite>();

    /// <summary>Block definitions (reusable symbols).</summary>
    public IReadOnlyList<BlockDefinition> BlockDefinitions { get; init; } = Array.Empty<BlockDefinition>();

    /// <summary>The user-defined origin (Nullpunkt) in absolute world coordinates.</summary>
    public Point3D Origin { get; init; } = Point3D.Origin;
}
