using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A closed planar region: an outer boundary plus zero or more island (hole) loops. Produced by
/// <see cref="RegionFinder"/> from a pick point and used as an extrusion profile or hatch
/// boundary.
/// </summary>
public sealed class Region2D
{
    public Region2D(IReadOnlyList<Point2D> outer, IReadOnlyList<IReadOnlyList<Point2D>> holes)
    {
        Outer = outer;
        Holes = holes;
    }

    public IReadOnlyList<Point2D> Outer { get; }

    public IReadOnlyList<IReadOnlyList<Point2D>> Holes { get; }
}
