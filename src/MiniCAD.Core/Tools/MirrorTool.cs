using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Mirrors (or, in <see cref="SelectionTransformTool.Copy"/> mode, mirror-copies) the selection
/// across an axis defined by two clicked points. Holding Shift constrains the axis to the
/// ortho/diagonal directions.
/// </summary>
public sealed class MirrorTool : SelectionTransformTool
{
    public MirrorTool(bool copy = false) : base(copy)
    {
    }

    public override string Name => Copy ? "Spiegeln (Kopie)" : "Spiegeln";

    protected override int RequiredPoints => 2;

    protected override Point2D ResolvePoint(in ToolPointerInput input)
        => FirstPoint is { } first ? ResolveSegmentPoint(first, input) : Snap(input);

    protected override bool TryBuildTransform(IReadOnlyList<Point2D> points, out Matrix2D transform)
    {
        if (points[0].DistanceSquaredTo(points[1]) <= GeometryMath.Epsilon)
        {
            transform = Matrix2D.Identity;
            return false;
        }

        transform = Matrix2D.Reflection(points[0], points[1]);
        return true;
    }

    protected override void AppendGuides(List<OverlayItem> items, IReadOnlyList<Point2D> points)
        => items.Add(new OverlayItem(new LineEntity(points[0], points[1]), ToolStyle.SnapMarker));
}
