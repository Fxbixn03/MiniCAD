namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable 2D position in world coordinates (double precision).
/// </summary>
public readonly struct Point2D : IEquatable<Point2D>
{
    public static readonly Point2D Origin = new(0.0, 0.0);

    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }

    /// <summary>The vector pointing from this point to <paramref name="other"/>.</summary>
    public Vector2D VectorTo(Point2D other) => new(other.X - X, other.Y - Y);

    public double DistanceTo(Point2D other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceSquaredTo(Point2D other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Linear interpolation: <c>t = 0</c> returns this point, <c>t = 1</c> returns <paramref name="other"/>.</summary>
    public Point2D Lerp(Point2D other, double t) => new(X + (other.X - X) * t, Y + (other.Y - Y) * t);

    public static Point2D operator +(Point2D point, Vector2D offset) => new(point.X + offset.X, point.Y + offset.Y);

    public static Point2D operator -(Point2D point, Vector2D offset) => new(point.X - offset.X, point.Y - offset.Y);

    /// <summary>The displacement vector between two points.</summary>
    public static Vector2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);

    public bool Equals(Point2D other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override bool Equals(object? obj) => obj is Point2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Point2D a, Point2D b) => a.Equals(b);

    public static bool operator !=(Point2D a, Point2D b) => !a.Equals(b);

    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}
