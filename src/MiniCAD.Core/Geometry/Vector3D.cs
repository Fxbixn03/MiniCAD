namespace MiniCAD.Core.Geometry;

/// <summary>An immutable 3D vector (double precision), the 3D counterpart to <see cref="Vector2D"/>.</summary>
public readonly struct Vector3D : IEquatable<Vector3D>
{
    public static readonly Vector3D Zero = new(0.0, 0.0, 0.0);
    public static readonly Vector3D UnitX = new(1.0, 0.0, 0.0);
    public static readonly Vector3D UnitY = new(0.0, 1.0, 0.0);
    public static readonly Vector3D UnitZ = new(0.0, 0.0, 1.0);

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public double LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3D Normalized()
    {
        double length = Length;
        return length <= GeometryMath.Epsilon ? Zero : new Vector3D(X / length, Y / length, Z / length);
    }

    public double Dot(Vector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vector3D Cross(Vector3D other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator -(Vector3D v) => new(-v.X, -v.Y, -v.Z);

    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);

    public static Vector3D operator *(double s, Vector3D v) => v * s;

    public static Vector3D operator /(Vector3D v, double s) => new(v.X / s, v.Y / s, v.Z / s);

    public bool Equals(Vector3D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    public override bool Equals(object? obj) => obj is Vector3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Vector3D a, Vector3D b) => a.Equals(b);

    public static bool operator !=(Vector3D a, Vector3D b) => !a.Equals(b);

    public override string ToString() => $"<{X:0.###}, {Y:0.###}, {Z:0.###}>";
}
