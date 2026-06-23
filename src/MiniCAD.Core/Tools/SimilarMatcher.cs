using MiniCAD.Core.Entities;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tools;

/// <summary>Which properties two entities must share to count as "similar" (#230).</summary>
[Flags]
public enum SimilarityCriteria
{
    Type = 1,
    Layer = 2,
    PartialDrawing = 4,
    Stroke = 8,
    Construction = 16,

    /// <summary>The default Allplan/AutoCAD behaviour: same kind of object on the same layer.</summary>
    TypeAndLayer = Type | Layer,
}

/// <summary>
/// "Select similar": given a reference entity, finds the other entities that match it on a chosen
/// set of properties (type, layer, Teilbild, stroke, construction flag). Pure — the caller decides
/// the candidate set (e.g. only visible entities) and how strokes resolve (#230).
/// </summary>
public static class SimilarMatcher
{
    public static IReadOnlyList<IEntity> Match(
        IEntity reference,
        IEnumerable<IEntity> candidates,
        SimilarityCriteria criteria = SimilarityCriteria.TypeAndLayer,
        Func<IEntity, StrokeStyle>? strokeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates.Where(c => IsSimilar(reference, c, criteria, strokeResolver)).ToList();
    }

    public static bool IsSimilar(
        IEntity a,
        IEntity b,
        SimilarityCriteria criteria,
        Func<IEntity, StrokeStyle>? strokeResolver = null)
    {
        if (criteria.HasFlag(SimilarityCriteria.Type) && a.GetType() != b.GetType())
            return false;
        if (criteria.HasFlag(SimilarityCriteria.Layer) && a.LayerId != b.LayerId)
            return false;
        if (criteria.HasFlag(SimilarityCriteria.PartialDrawing) && a.PartialDrawingId != b.PartialDrawingId)
            return false;
        if (criteria.HasFlag(SimilarityCriteria.Construction) && a.IsConstruction != b.IsConstruction)
            return false;
        if (criteria.HasFlag(SimilarityCriteria.Stroke))
        {
            StrokeStyle strokeA = strokeResolver?.Invoke(a) ?? a.StrokeOverride ?? default;
            StrokeStyle strokeB = strokeResolver?.Invoke(b) ?? b.StrokeOverride ?? default;
            if (!strokeA.Equals(strokeB))
                return false;
        }

        return true;
    }
}
