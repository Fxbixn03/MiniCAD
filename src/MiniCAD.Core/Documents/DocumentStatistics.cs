using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Documents;

/// <summary>A named tally used in the statistics report (e.g. one entity type, or one layer).</summary>
public sealed record NamedCount(string Name, int Count);

/// <summary>
/// A read-only snapshot of a document's contents: how many objects there are (overall, by type,
/// by layer and by Teilbild), how many definitions of each kind are defined, and the drawing's
/// 2D/3D extents. Pure aggregation — computing it never mutates the document (#235).
/// </summary>
public sealed class DocumentStatistics
{
    public int EntityCount { get; init; }

    /// <summary>How many of the entities are construction (Hilfskonstruktion) geometry.</summary>
    public int ConstructionCount { get; init; }

    /// <summary>Object counts per entity type, most numerous first.</summary>
    public IReadOnlyList<NamedCount> CountsByType { get; init; } = Array.Empty<NamedCount>();

    /// <summary>Object counts per layer, in document layer order (includes empty layers).</summary>
    public IReadOnlyList<NamedCount> CountsByLayer { get; init; } = Array.Empty<NamedCount>();

    /// <summary>Object counts per Teilbild, in stacking order (includes empty Teilbilder).</summary>
    public IReadOnlyList<NamedCount> CountsByPartialDrawing { get; init; } = Array.Empty<NamedCount>();

    public int LayerCount { get; init; }

    public int PartialDrawingCount { get; init; }

    public int TextStyleCount { get; init; }

    public int DimStyleCount { get; init; }

    public int BlockDefinitionCount { get; init; }

    public int PatternCount { get; init; }

    public int ModelCount { get; init; }

    /// <summary>Bounding box of all visible 2D geometry, or <c>null</c> if there is none.</summary>
    public Rect2D? ContentBounds { get; init; }

    /// <summary>Bounding box of all 3D model objects, or <c>null</c> if the model space is empty.</summary>
    public BoundingBox3D? ModelBounds { get; init; }

    public static DocumentStatistics Compute(ICadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IReadOnlyList<IEntity> entities = document.Entities;

        var byType = entities
            .GroupBy(EntityDisplayName.Of)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var byLayer = document.Layers
            .Select(layer => new NamedCount(layer.Name, entities.Count(e => e.LayerId == layer.Id)))
            .ToList();

        var byPartialDrawing = document.PartialDrawings
            .Select(pd => new NamedCount(pd.Name, entities.Count(e => e.PartialDrawingId == pd.Id)))
            .ToList();

        return new DocumentStatistics
        {
            EntityCount = entities.Count,
            ConstructionCount = entities.Count(e => e.IsConstruction),
            CountsByType = byType,
            CountsByLayer = byLayer,
            CountsByPartialDrawing = byPartialDrawing,
            LayerCount = document.Layers.Count,
            PartialDrawingCount = document.PartialDrawings.Count,
            TextStyleCount = document.TextStyles.Count,
            DimStyleCount = document.DimStyles.Count,
            BlockDefinitionCount = document.BlockDefinitions.Count,
            PatternCount = document.Patterns.Count,
            ModelCount = document.Models.Count,
            ContentBounds = document.GetContentBounds(),
            ModelBounds = document.GetModelBounds(),
        };
    }
}
