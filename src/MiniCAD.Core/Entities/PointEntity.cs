using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>The glyph used to draw a <see cref="PointEntity"/>.</summary>
public enum PointStyle
{
    /// <summary>An upright plus (+).</summary>
    Plus,

    /// <summary>A diagonal cross (×).</summary>
    Cross,

    /// <summary>A small ring.</summary>
    Dot,

    /// <summary>A small square.</summary>
    Square,
}

/// <summary>
/// A reference point / node drawn as a small marker (survey point, snap node, …). It carries a
/// world-space marker size and glyph style and snaps to its own position.
/// </summary>
public sealed class PointEntity : Entity, IEditableEntity
{
    private double _size;

    public PointEntity(Point2D position, double size = 6.0, PointStyle style = PointStyle.Plus)
    {
        Position = position;
        Size = size;
        Style = style;
    }

    private PointEntity(PointEntity source) : base(source)
    {
        Position = source.Position;
        _size = source._size;
        Style = source.Style;
    }

    public Point2D Position { get; set; }

    /// <summary>The marker's full extent in world units; always non-negative.</summary>
    public double Size
    {
        get => _size;
        set => _size = Math.Abs(value);
    }

    public PointStyle Style { get; set; }

    private double Half => _size * 0.5;

    public override Rect2D Bounds => Rect2D.FromCenter(Position, Half, Half);

    public override IEnumerable<Point2D> SnapPoints
    {
        get { yield return Position; }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => Position.DistanceTo(point) <= Math.Max(tolerance, Half);

    public override bool IntersectsRect(Rect2D rect) => rect.Contains(Position);

    public override void Transform(in Matrix2D matrix)
    {
        Position = matrix.Transform(Position);
        Size *= matrix.UniformScale;
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        double half = Half;
        switch (Style)
        {
            case PointStyle.Plus:
                surface.DrawLine(new Point2D(Position.X - half, Position.Y), new Point2D(Position.X + half, Position.Y), stroke);
                surface.DrawLine(new Point2D(Position.X, Position.Y - half), new Point2D(Position.X, Position.Y + half), stroke);
                break;

            case PointStyle.Cross:
                surface.DrawLine(new Point2D(Position.X - half, Position.Y - half), new Point2D(Position.X + half, Position.Y + half), stroke);
                surface.DrawLine(new Point2D(Position.X - half, Position.Y + half), new Point2D(Position.X + half, Position.Y - half), stroke);
                break;

            case PointStyle.Dot:
                surface.DrawCircle(Position, Math.Max(half * 0.4, 0.0), stroke);
                break;

            case PointStyle.Square:
                surface.DrawPolyline(new[]
                {
                    new Point2D(Position.X - half, Position.Y - half),
                    new Point2D(Position.X + half, Position.Y - half),
                    new Point2D(Position.X + half, Position.Y + half),
                    new Point2D(Position.X - half, Position.Y + half),
                }, closed: true, stroke);
                break;
        }
    }

    public override IEntity Clone() => new PointEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState() => (Position, Size, Style);

    public void RestoreState(object state) => (Position, Size, Style) = ((Point2D, double, PointStyle))state;
}
