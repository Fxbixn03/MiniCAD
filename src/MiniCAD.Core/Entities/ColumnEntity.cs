using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An architectural column (Stütze, #75): a rectangular or round vertical member at a point, with a
/// cross-section size, a height and a base elevation (Z of its underside). Like a wall, it draws in
/// plan as its cross-section and is the 2D source of a live 3D solid (box or cylinder).
/// </summary>
public sealed class ColumnEntity : Entity, IEditableEntity
{
    private double _width;
    private double _depth;
    private double _height;

    public ColumnEntity(Point2D position, bool round = false, double width = 300.0, double depth = 300.0,
        double height = 2500.0, double baseElevation = 0.0)
    {
        Position = position;
        Round = round;
        Width = width;
        Depth = depth;
        Height = height;
        BaseElevation = baseElevation;
    }

    private ColumnEntity(ColumnEntity source) : base(source)
    {
        Position = source.Position;
        Round = source.Round;
        _width = source._width;
        _depth = source._depth;
        _height = source._height;
        BaseElevation = source.BaseElevation;
    }

    public Point2D Position { get; set; }

    /// <summary>True for a round column (a circle of diameter <see cref="Width"/>); else rectangular.</summary>
    public bool Round { get; set; }

    /// <summary>Cross-section width (X), or the diameter when <see cref="Round"/>; always positive.</summary>
    public double Width
    {
        get => _width;
        set => _width = value <= 0 ? 1.0 : value;
    }

    /// <summary>Cross-section depth (Y) for a rectangular column; always positive.</summary>
    public double Depth
    {
        get => _depth;
        set => _depth = value <= 0 ? 1.0 : value;
    }

    public double Height
    {
        get => _height;
        set => _height = value <= 0 ? 1.0 : value;
    }

    /// <summary>Z of the column's underside.</summary>
    public double BaseElevation { get; set; }

    public double TopElevation => BaseElevation + _height;

    public override Rect2D Bounds
    {
        get
        {
            double hx = Round ? _width / 2 : _width / 2;
            double hy = Round ? _width / 2 : _depth / 2;
            return new Rect2D(Position.X - hx, Position.Y - hy, Position.X + hx, Position.Y + hy);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            Rect2D b = Bounds;
            yield return new Point2D(b.MinX, b.MinY);
            yield return new Point2D(b.MaxX, b.MinY);
            yield return new Point2D(b.MaxX, b.MaxY);
            yield return new Point2D(b.MinX, b.MaxY);
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
    {
        if (Round)
            return (point - Position).Length <= _width / 2 + tolerance;
        return Bounds.Inflate(tolerance).Contains(point);
    }

    public override bool IntersectsRect(Rect2D rect) => rect.Intersects(Bounds);

    public override void Transform(in Matrix2D matrix)
    {
        Position = matrix.Transform(Position);
        double scale = matrix.UniformScale;
        Width *= scale;
        Depth *= scale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        if (Round)
        {
            surface.DrawCircle(Position, _width / 2, stroke);
        }
        else
        {
            double hx = _width / 2, hy = _depth / 2;
            var corners = new[]
            {
                new Point2D(Position.X - hx, Position.Y - hy),
                new Point2D(Position.X + hx, Position.Y - hy),
                new Point2D(Position.X + hx, Position.Y + hy),
                new Point2D(Position.X - hx, Position.Y + hy),
            };
            surface.DrawPolyline(corners, closed: true, stroke);
        }
    }

    public override IEntity Clone() => new ColumnEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState() => (Position, Round, _width, _depth, _height, BaseElevation);

    public void RestoreState(object state)
        => (Position, Round, _width, _depth, _height, BaseElevation) = ((Point2D, bool, double, double, double, double))state;
}
