using MiniCAD.Core.Entities;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Documents;

/// <summary>Selects which kinds of unused definition a purge should remove (#233).</summary>
public sealed class PurgeOptions
{
    public bool Layers { get; set; } = true;

    public bool EmptyPartialDrawings { get; set; } = true;

    public bool TextStyles { get; set; } = true;

    public bool DimStyles { get; set; } = true;

    public bool BlockDefinitions { get; set; } = true;

    public bool Patterns { get; set; } = true;

    /// <summary>Purge everything that is unused (the default).</summary>
    public static PurgeOptions All => new();
}

/// <summary>
/// The set of unused definitions a purge found (or removed). Holds the actual instances so the
/// operation can be previewed, reported and undone (re-inserting the very same objects).
/// </summary>
public sealed class PurgeReport
{
    public IReadOnlyList<Layer> Layers { get; init; } = Array.Empty<Layer>();

    public IReadOnlyList<PartialDrawing> PartialDrawings { get; init; } = Array.Empty<PartialDrawing>();

    public IReadOnlyList<TextStyle> TextStyles { get; init; } = Array.Empty<TextStyle>();

    public IReadOnlyList<DimStyle> DimStyles { get; init; } = Array.Empty<DimStyle>();

    public IReadOnlyList<BlockDefinition> BlockDefinitions { get; init; } = Array.Empty<BlockDefinition>();

    public IReadOnlyList<HatchPattern> Patterns { get; init; } = Array.Empty<HatchPattern>();

    public int Total =>
        Layers.Count + PartialDrawings.Count + TextStyles.Count +
        DimStyles.Count + BlockDefinitions.Count + Patterns.Count;

    public bool IsEmpty => Total == 0;

    /// <summary>A per-category summary (only non-empty categories), e.g. "2 Layer, 1 Textstil".</summary>
    public string Summary()
    {
        var parts = new List<string>();
        if (Layers.Count > 0) parts.Add($"{Layers.Count} Layer");
        if (PartialDrawings.Count > 0) parts.Add($"{PartialDrawings.Count} leere Teilbilder");
        if (TextStyles.Count > 0) parts.Add($"{TextStyles.Count} Textstile");
        if (DimStyles.Count > 0) parts.Add($"{DimStyles.Count} Maßstile");
        if (BlockDefinitions.Count > 0) parts.Add($"{BlockDefinitions.Count} Blöcke");
        if (Patterns.Count > 0) parts.Add($"{Patterns.Count} Muster");
        return parts.Count == 0 ? "Nichts zu bereinigen" : string.Join(", ", parts);
    }
}

/// <summary>
/// Determines which document definitions (layers, styles, blocks, patterns, empty Teilbilder) are
/// not referenced by any geometry and can be safely removed. Protected entries — the "Standard"
/// styles, the default and active layer/Teilbild — are never reported. Block references are
/// resolved transitively, so a block used only by another (also unused) block is itself unused.
/// </summary>
public static class PurgeAnalyzer
{
    public static PurgeReport FindPurgeable(CadDocument document, PurgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= PurgeOptions.All;

        HashSet<Guid> usedBlockIds = CollectUsedBlockDefinitions(document);
        List<IEntity> live = CollectLiveEntities(document, usedBlockIds);

        var usedLayerIds = new HashSet<Guid>(live.Select(e => e.LayerId));
        var usedTextStyleIds = new HashSet<Guid>(live.OfType<ITextEntity>().Select(t => t.TextStyleId));
        var usedDimStyleIds = new HashSet<Guid>(live.OfType<DimensionEntity>().Select(d => d.DimStyleId));

        var usedPatternIds = new HashSet<Guid>();
        foreach (IEntity entity in live)
        {
            if (entity is PolylineEntity { Fill: { } fill })
                usedPatternIds.Add(fill.Id);
        }

        // Teilbilder are containers, not referenced styles: a Teilbild is purgeable only when no
        // top-level entity lives on it (block-internal geometry doesn't keep a Teilbild "occupied").
        var occupiedPartialDrawingIds = new HashSet<Guid>(document.Entities.Select(e => e.PartialDrawingId));

        return new PurgeReport
        {
            Layers = options.Layers
                ? document.Layers
                    .Where(l => l != document.DefaultLayer && l != document.ActiveLayer && !usedLayerIds.Contains(l.Id))
                    .ToList()
                : Array.Empty<Layer>(),
            PartialDrawings = options.EmptyPartialDrawings
                ? document.PartialDrawings
                    .Where(pd => pd != document.DefaultPartialDrawing && pd != document.ActivePartialDrawing
                        && !occupiedPartialDrawingIds.Contains(pd.Id))
                    .ToList()
                : Array.Empty<PartialDrawing>(),
            TextStyles = options.TextStyles
                ? document.TextStyles
                    .Where(s => s != document.DefaultTextStyle && s != document.ActiveTextStyle && !usedTextStyleIds.Contains(s.Id))
                    .ToList()
                : Array.Empty<TextStyle>(),
            DimStyles = options.DimStyles
                ? document.DimStyles
                    .Where(s => s != document.DefaultDimStyle && s != document.ActiveDimStyle && !usedDimStyleIds.Contains(s.Id))
                    .ToList()
                : Array.Empty<DimStyle>(),
            BlockDefinitions = options.BlockDefinitions
                ? document.BlockDefinitions.Where(b => !usedBlockIds.Contains(b.Id)).ToList()
                : Array.Empty<BlockDefinition>(),
            Patterns = options.Patterns
                ? document.Patterns.Where(p => !usedPatternIds.Contains(p.Id)).ToList()
                : Array.Empty<HatchPattern>(),
        };
    }

    private static HashSet<Guid> CollectUsedBlockDefinitions(CadDocument document)
    {
        var used = new HashSet<Guid>();
        var pending = new Queue<Guid>();

        foreach (IEntity entity in document.Entities)
        {
            if (entity is BlockReferenceEntity reference && used.Add(reference.DefinitionId))
                pending.Enqueue(reference.DefinitionId);
        }

        while (pending.Count > 0)
        {
            BlockDefinition? definition = document.FindBlockDefinition(pending.Dequeue());
            if (definition is null)
                continue;

            foreach (IEntity entity in definition.Entities)
            {
                if (entity is BlockReferenceEntity reference && used.Add(reference.DefinitionId))
                    pending.Enqueue(reference.DefinitionId);
            }
        }

        return used;
    }

    private static List<IEntity> CollectLiveEntities(CadDocument document, HashSet<Guid> usedBlockIds)
    {
        var live = new List<IEntity>(document.Entities);
        foreach (Guid id in usedBlockIds)
        {
            if (document.FindBlockDefinition(id) is { } definition)
                live.AddRange(definition.Entities);
        }

        return live;
    }
}
