using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Moves (or, in <see cref="SelectionTransformTool.Copy"/> mode, copies) the selection by a
/// translation: click the base point, then the target point. Holding Shift constrains the
/// displacement to the ortho/diagonal directions.
/// </summary>
public sealed class MoveTool : SelectionTransformTool
{
    public MoveTool(bool copy = false) : base(copy)
    {
    }

    public override string Name => Copy ? "Kopieren" : "Verschieben";

    protected override int RequiredPoints => 2;

    protected override Point2D ResolvePoint(in ToolPointerInput input)
        => FirstPoint is { } basePoint ? ResolveSegmentPoint(basePoint, input) : Snap(input);

    protected override bool TryBuildTransform(IReadOnlyList<Point2D> points, out Matrix2D transform)
    {
        transform = Matrix2D.Translation(points[1] - points[0]);
        return transform != Matrix2D.Identity;
    }

    protected override void AppendGuides(List<OverlayItem> items, IReadOnlyList<Point2D> points)
        => items.Add(new OverlayItem(new LineEntity(points[0], points[1]), ToolStyle.Preview));
}
