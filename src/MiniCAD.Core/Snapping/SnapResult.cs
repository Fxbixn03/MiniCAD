using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Snapping;

/// <summary>The kind of target the cursor snapped to (drives the on-screen marker).</summary>
public enum SnapKind
{
    None,
    Grid,
    Point,
}

/// <summary>
/// Outcome of a snap query: either the original point unchanged, or a snapped point with
/// the kind of target that was hit.
/// </summary>
public readonly struct SnapResult
{
    public SnapResult(Point2D point, SnapKind kind)
    {
        Point = point;
        Kind = kind;
    }

    public Point2D Point { get; }

    public SnapKind Kind { get; }

    public bool IsSnapped => Kind != SnapKind.None;

    /// <summary>An unsnapped result that carries the original cursor point through.</summary>
    public static SnapResult Unsnapped(Point2D point) => new(point, SnapKind.None);
}
