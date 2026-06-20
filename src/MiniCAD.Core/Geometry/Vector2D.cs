namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable 2D direction/displacement in world coordinates (double precision).
/// </summary>
public readonly struct Vector2D : IEquatable<Vector2D>
{
    public static readonly Vector2D Zero = new(0.0, 0.0);
    public static readonly Vector2D UnitX = new(1.0, 0.0);
    public static readonly Vector2D UnitY = new(0.0, 1.0);

    public Vector2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }

    public double Length => Math.Sqrt(X * X + Y * Y);

    public double LengthSquared => X * X + Y * Y;

    /// <summary>Returns a unit-length copy, or <see cref="Zero"/> if the vector is degenerate.</summary>
    public Vector2D Normalized()
    {
        double length = Length;
        return length <= GeometryMath.Epsilon ? Zero : new Vector2D(X / length, Y / length);
    }

    /// <summary>The vector rotated 90° counter-clockwise.</summary>
    public Vector2D Perpendicular() => new(-Y, X);

    public double Dot(Vector2D other) => X * other.X + Y * other.Y;

    /// <summary>The 2D cross product (signed area of the parallelogram).</summary>
    public double Cross(Vector2D other) => X * other.Y - Y * other.X;

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);

    public static Vector2D operator *(Vector2D v, double scalar) => new(v.X * scalar, v.Y * scalar);

    public static Vector2D operator *(double scalar, Vector2D v) => new(v.X * scalar, v.Y * scalar);

    public static Vector2D operator /(Vector2D v, double scalar) => new(v.X / scalar, v.Y / scalar);

    public bool Equals(Vector2D other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override bool Equals(object? obj) => obj is Vector2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Vector2D a, Vector2D b) => a.Equals(b);

    public static bool operator !=(Vector2D a, Vector2D b) => !a.Equals(b);

    public override string ToString() => $"<{X:0.###}, {Y:0.###}>";
}
