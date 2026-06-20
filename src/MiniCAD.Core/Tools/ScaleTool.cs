using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Uniformly scales the selection about a base point using a reference length: click the base
/// point, then a reference point (the "before" length) and finally a target point (the "after"
/// length). The scale factor is the ratio of the two distances, which makes the gesture fully
/// interactive without typing a number.
/// </summary>
public sealed class ScaleTool : SelectionTransformTool
{
    public override string Name => "Skalieren";

    protected override int RequiredPoints => 3;

    protected override bool TryBuildTransform(IReadOnlyList<Point2D> points, out Matrix2D transform)
    {
        Point2D basePoint = points[0];
        double reference = basePoint.DistanceTo(points[1]);
        double target = basePoint.DistanceTo(points[2]);

        if (reference <= GeometryMath.Epsilon || target <= GeometryMath.Epsilon)
        {
            transform = Matrix2D.Identity;
            return false;
        }

        double factor = target / reference;
        transform = Matrix2D.Scaling(factor, factor, basePoint);
        return true;
    }

    protected override void AppendGuides(List<OverlayItem> items, IReadOnlyList<Point2D> points)
    {
        // Green = the reference length being grabbed; blue = the new (target) length.
        items.Add(new OverlayItem(new LineEntity(points[0], points[1]), ToolStyle.SnapMarker));
        items.Add(new OverlayItem(new LineEntity(points[0], points[2]), ToolStyle.Preview));
    }
}
