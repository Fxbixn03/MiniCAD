using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Rotates the selection about a base point: click the base (pivot) point, then a second point
/// whose direction from the pivot sets the rotation angle (measured from the positive X axis).
/// Holding Shift snaps the angle to 45° steps.
/// </summary>
public sealed class RotateTool : SelectionTransformTool
{
    public override string Name => "Drehen";

    protected override int RequiredPoints => 2;

    protected override Point2D ResolvePoint(in ToolPointerInput input)
        => FirstPoint is { } pivot ? ResolveSegmentPoint(pivot, input) : Snap(input);

    protected override bool TryBuildTransform(IReadOnlyList<Point2D> points, out Matrix2D transform)
    {
        Point2D pivot = points[0];
        Vector2D direction = points[1] - pivot;
        if (direction.LengthSquared <= GeometryMath.Epsilon)
        {
            transform = Matrix2D.Identity;
            return false;
        }

        transform = Matrix2D.Rotation(Math.Atan2(direction.Y, direction.X), pivot);
        return true;
    }

    protected override void AppendGuides(List<OverlayItem> items, IReadOnlyList<Point2D> points)
        => items.Add(new OverlayItem(new LineEntity(points[0], points[1]), ToolStyle.Preview));
}
