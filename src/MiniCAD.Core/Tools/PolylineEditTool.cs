using MiniCAD.Core.Commands;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;

namespace MiniCAD.Core.Tools;

/// <summary>The action a <see cref="PolylineEditTool"/> performs on each click.</summary>
public enum PolylineEditMode
{
    /// <summary>Insert a vertex on the segment under the cursor.</summary>
    AddVertex,

    /// <summary>Delete the vertex nearest the cursor.</summary>
    RemoveVertex,

    /// <summary>Toggle the segment under the cursor between straight and a circular arc.</summary>
    ToggleArc,
}

/// <summary>
/// Edits the vertices of a polyline directly (#188): add a vertex on a segment, delete a vertex, or
/// convert a straight segment to/from a circular-arc segment (bulge). The first click picks the
/// target polyline (or it adopts the current selection); each subsequent click applies the active
/// <see cref="Mode"/> as one undoable step. Vertices can also be dragged via the normal grips.
/// </summary>
public sealed class PolylineEditTool : ToolBase
{
    private const double DefaultBulge = 0.5; // ~53° arc when toggling a segment to a curve

    private PolylineEntity? _target;

    public PolylineEditMode Mode { get; set; } = PolylineEditMode.AddVertex;

    /// <summary>Optional polyline to edit, set when the tool is activated with a selection.</summary>
    public PolylineEntity? InitialTarget { get; set; }

    public override string Name => "Polylinie bearbeiten";

    public override void Activate(IToolContext context)
    {
        base.Activate(context);
        _target = InitialTarget;
    }

    protected override bool HasActiveOperation => _target is not null;

    public override void PointerDown(in ToolPointerInput input)
    {
        if (input.Button == ToolButton.Right)
        {
            Cancel();
            return;
        }

        if (input.Button != ToolButton.Left)
            return;

        if (_target is null)
        {
            _target = PickPolyline(input.World);
            Context.RequestRedraw();
            return;
        }

        Point2D world = Snap(input);
        object before = _target.CaptureState();
        bool changed = Mode switch
        {
            PolylineEditMode.AddVertex => AddVertex(_target, world),
            PolylineEditMode.RemoveVertex => RemoveVertex(_target, world),
            PolylineEditMode.ToggleArc => ToggleArc(_target, world),
            _ => false,
        };

        if (changed)
        {
            object after = _target.CaptureState();
            Context.Execute(new EditEntityCommand(Context.Document, _target, before, after));
        }

        ClearSnap();
        Context.RequestRedraw();
    }

    public override void PointerMove(in ToolPointerInput input)
    {
        Snap(input);
        Context.RequestRedraw();
    }

    private static bool AddVertex(PolylineEntity poly, Point2D world)
    {
        int seg = NearestSegment(poly, world, out Point2D onSegment);
        if (seg < 0)
            return false;
        poly.InsertVertex(seg, onSegment);
        return true;
    }

    private static bool RemoveVertex(PolylineEntity poly, Point2D world)
    {
        if (poly.Points.Count <= 2)
            return false;

        int best = -1;
        double bestDist = double.MaxValue;
        for (int i = 0; i < poly.Points.Count; i++)
        {
            double d = poly.Points[i].DistanceTo(world);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        if (best < 0)
            return false;
        poly.RemoveVertex(best);
        return true;
    }

    private static bool ToggleArc(PolylineEntity poly, Point2D world)
    {
        int seg = NearestSegment(poly, world, out _);
        if (seg < 0)
            return false;
        poly.SetBulge(seg, Math.Abs(poly.GetBulge(seg)) > 1e-9 ? 0.0 : DefaultBulge);
        return true;
    }

    private static int NearestSegment(PolylineEntity poly, Point2D world, out Point2D onSegment)
    {
        onSegment = world;
        IReadOnlyList<Point2D> pts = poly.Points;
        int count = poly.IsClosed ? pts.Count : pts.Count - 1;
        int best = -1;
        double bestDist = double.MaxValue;

        for (int i = 0; i < count; i++)
        {
            double d = GeometryMath.DistancePointToSegment(world, pts[i], pts[(i + 1) % pts.Count], out Point2D closest);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
                onSegment = closest;
            }
        }

        return best;
    }

    public override void Cancel()
    {
        _target = null;
        InitialTarget = null;
        ClearSnap();
        Context.RequestRedraw();
    }

    public override IReadOnlyList<OverlayItem> GetOverlay()
    {
        var items = new List<OverlayItem>();
        if (_target is { } target)
        {
            items.Add(new OverlayItem(target, ToolStyle.Selection));
            foreach (Point2D p in target.Points)
                items.Add(new OverlayItem(new PointEntity(p, 6, PointStyle.Square), ToolStyle.Preview));
        }

        AddSnapMarker(items);
        return items;
    }

    private PolylineEntity? PickPolyline(Point2D world)
    {
        double tolerance = Context.PickTolerance;
        IReadOnlyList<IEntity> entities = Context.Document.Entities;
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            if (entities[i] is PolylineEntity poly && Context.Document.IsEntityEditable(poly)
                && poly.HitTest(world, tolerance))
                return poly;
        }

        return null;
    }
}
