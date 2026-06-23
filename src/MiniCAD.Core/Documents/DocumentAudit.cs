using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Documents;

public enum AuditSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>A single problem the audit found, with whether it was repaired.</summary>
public sealed record AuditFinding(AuditSeverity Severity, string Description, bool Repaired);

/// <summary>
/// Checks a document for inconsistencies and optionally repairs them (#234): references that point
/// nowhere (a missing layer/Teilbild/style/block), degenerate geometry (NaN or empty), and
/// duplicate object ids. Reference problems are healed by falling back to the document defaults;
/// unsalvageable objects (a block reference without a definition, corrupt geometry, a duplicate id)
/// are removed. The same checks run automatically on load via <see cref="CadDocument.LoadContents"/>,
/// so a damaged file opens repaired instead of crashing.
/// </summary>
public sealed class DocumentAudit
{
    private DocumentAudit(IReadOnlyList<AuditFinding> findings) => Findings = findings;

    public IReadOnlyList<AuditFinding> Findings { get; }

    public bool HasIssues => Findings.Count > 0;

    public int RepairedCount => Findings.Count(f => f.Repaired);

    public static DocumentAudit Run(CadDocument document, bool repair)
    {
        ArgumentNullException.ThrowIfNull(document);

        var findings = new List<AuditFinding>();
        var toRemove = new HashSet<IEntity>();

        foreach (IEntity entity in document.Entities)
        {
            if (entity.LayerId != Guid.Empty && document.FindLayer(entity.LayerId) is null)
            {
                if (repair)
                {
                    entity.LayerId = document.DefaultLayer.Id;
                    document.NotifyEntityModified(entity);
                }

                findings.Add(new AuditFinding(AuditSeverity.Warning,
                    $"{EntityDisplayName.Of(entity)} verweist auf einen fehlenden Layer.", repair));
            }

            if (entity.PartialDrawingId != Guid.Empty && document.FindPartialDrawing(entity.PartialDrawingId) is null)
            {
                if (repair)
                {
                    entity.PartialDrawingId = document.DefaultPartialDrawing.Id;
                    document.NotifyEntityModified(entity);
                }

                findings.Add(new AuditFinding(AuditSeverity.Warning,
                    $"{EntityDisplayName.Of(entity)} verweist auf ein fehlendes Teilbild.", repair));
            }

            if (entity is ITextEntity text && text.TextStyleId != Guid.Empty && document.FindTextStyle(text.TextStyleId) is null)
            {
                if (repair)
                {
                    CadDocument.ApplyTextStyle(text, document.DefaultTextStyle);
                    document.NotifyEntityModified(entity);
                }

                findings.Add(new AuditFinding(AuditSeverity.Warning,
                    $"{EntityDisplayName.Of(entity)} verweist auf einen fehlenden Textstil.", repair));
            }

            if (entity is DimensionEntity dim && dim.DimStyleId != Guid.Empty && document.FindDimStyle(dim.DimStyleId) is null)
            {
                if (repair)
                {
                    CadDocument.ApplyDimStyle(dim, document.DefaultDimStyle);
                    document.NotifyEntityModified(entity);
                }

                findings.Add(new AuditFinding(AuditSeverity.Warning,
                    $"{EntityDisplayName.Of(entity)} verweist auf einen fehlenden Maßstil.", repair));
            }

            if (IsOrphanBlockReference(document, entity) && toRemove.Add(entity))
                findings.Add(new AuditFinding(AuditSeverity.Error,
                    $"{EntityDisplayName.Of(entity)}: Blockreferenz ohne Definition – entfernt.", repair));
            else if (IsDegenerate(entity) && toRemove.Add(entity))
                findings.Add(new AuditFinding(AuditSeverity.Error,
                    $"{EntityDisplayName.Of(entity)}: entartete Geometrie (leer/NaN) – entfernt.", repair));
        }

        foreach (IGrouping<Guid, IEntity> group in document.Entities.GroupBy(e => e.Id.Value).Where(g => g.Count() > 1))
        {
            foreach (IEntity duplicate in group.Skip(1))
            {
                if (toRemove.Add(duplicate))
                    findings.Add(new AuditFinding(AuditSeverity.Error,
                        $"{EntityDisplayName.Of(duplicate)}: doppelte Objekt-ID – entfernt.", repair));
            }
        }

        if (repair)
        {
            foreach (IEntity entity in toRemove)
                document.RemoveEntity(entity);
        }

        return new DocumentAudit(findings);
    }

    internal static bool IsOrphanBlockReference(CadDocument document, IEntity entity)
        => entity is BlockReferenceEntity reference
           && reference.Definition is null
           && document.FindBlockDefinition(reference.DefinitionId) is null;

    internal static bool IsDegenerate(IEntity entity)
    {
        if (entity is PolylineEntity polyline && polyline.Points.Count < 2)
            return true;

        Rect2D bounds = entity.Bounds;
        return !IsFinite(bounds.MinX) || !IsFinite(bounds.MinY)
            || !IsFinite(bounds.MaxX) || !IsFinite(bounds.MaxY);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
