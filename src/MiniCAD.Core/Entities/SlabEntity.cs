using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An architectural slab (Decke, #72): a closed outline polygon in plan with a thickness and a base
/// elevation (Z of its underside). It draws as its outline and is the 2D source of a live 3D slab
/// solid (the outline extruded by its thickness).
/// </summary>
public sealed class SlabEntity : Entity, IEditableEntity
{
    private readonly List<Point2D> _outline;
    private double _thickness;

    public SlabEntity(IEnumerable<Point2D> outline, double thickness = 200.0, double baseElevation = 0.0)
    {
        _outline = new List<Point2D>(outline);
        Thickness = thickness;
        BaseElevation = baseElevation;
    }

    private SlabEntity(SlabEntity source) : base(source)
    {
        _outline = new List<Point2D>(source._outline);
        _thickness = source._thickness;
        BaseElevation = source.BaseElevation;
    }

    /// <summary>The slab boundary in plan (a closed polygon).</summary>
    public IReadOnlyList<Point2D> Outline => _outline;

    public double Thickness
    {
        get => _thickness;
        set => _thickness = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z of the slab's underside.</summary>
    public double BaseElevation { get; set; }

    public double TopElevation => BaseElevation + _thickness;

    public override Rect2D Bounds
    {
        get
        {
            if (_outline.Count == 0)
                return new Rect2D(0, 0, 0, 0);
            Rect2D bounds = Rect2D.FromPoints(_outline[0], _outline[0]);
            foreach (Point2D p in _outline)
                bounds = bounds.Union(p);
            return bounds;
        }
    }

    public override IEnumerable<Point2D> SnapPoints => _outline;

    public override bool HitTest(Point2D point, double tolerance)
    {
        if (_outline.Count < 2)
            return false;
        for (int i = 0; i < _outline.Count; i++)
        {
            if (GeometryMath.DistancePointToSegment(point, _outline[i], _outline[(i + 1) % _outline.Count], out _) <= tolerance)
                return true;
        }
        return _outline.Count >= 3 && GeometryMath.PointInPolygon(_outline, point);
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        for (int i = 0; i < _outline.Count; i++)
        {
            if (GeometryMath.SegmentIntersectsRect(_outline[i], _outline[(i + 1) % _outline.Count], rect))
                return true;
        }
        return _outline.Count > 0 && rect.Contains(_outline[0]);
    }

    public override void Transform(in Matrix2D matrix)
    {
        for (int i = 0; i < _outline.Count; i++)
            _outline[i] = matrix.Transform(_outline[i]);
        Thickness *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        if (_outline.Count >= 2)
            surface.DrawPolyline(_outline, closed: true, stroke);
    }

    public override IEntity Clone() => new SlabEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips()
    {
        var grips = new Grip[_outline.Count];
        for (int i = 0; i < _outline.Count; i++)
            grips[i] = new Grip(_outline[i], GripKind.Vertex, i);
        return grips;
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index >= 0 && grip.Index < _outline.Count)
            _outline[grip.Index] = newPosition;
    }

    public object CaptureState() => (new List<Point2D>(_outline), _thickness, BaseElevation);

    public void RestoreState(object state)
    {
        var (outline, thickness, baseElevation) = ((List<Point2D>, double, double))state;
        _outline.Clear();
        _outline.AddRange(outline);
        _thickness = thickness;
        BaseElevation = baseElevation;
    }
}
