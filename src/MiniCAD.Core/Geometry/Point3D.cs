namespace MiniCAD.Core.Geometry;

/// <summary>
/// An immutable 3D position in world coordinates (double precision). The drawing model is
/// currently 2D, so the <see cref="Z"/> axis is carried but stays zero for planar geometry;
/// it exists so the coordinate system is "open" for a future move to full 3D without
/// reshaping the public API.
/// </summary>
public readonly struct Point3D : IEquatable<Point3D>
{
    public static readonly Point3D Origin = new(0.0, 0.0, 0.0);

    public Point3D(double x, double y, double z = 0.0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    /// <summary>Drops the <see cref="Z"/> component to obtain the planar projection.</summary>
    public Point2D ToPoint2D() => new(X, Y);

    /// <summary>Lifts a planar point into 3D at the given height (default ground plane).</summary>
    public static Point3D FromPoint2D(Point2D point, double z = 0.0) => new(point.X, point.Y, z);

    /// <summary>The position as a vector from the origin.</summary>
    public Vector3D ToVector3D() => new(X, Y, Z);

    public double DistanceTo(Point3D other) => (this - other).Length;

    public Point3D Lerp(Point3D other, double t)
        => new(X + (other.X - X) * t, Y + (other.Y - Y) * t, Z + (other.Z - Z) * t);

    public static Point3D operator +(Point3D p, Vector3D v) => new(p.X + v.X, p.Y + v.Y, p.Z + v.Z);

    public static Point3D operator -(Point3D p, Vector3D v) => new(p.X - v.X, p.Y - v.Y, p.Z - v.Z);

    public static Vector3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public bool Equals(Point3D other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

    public override bool Equals(object? obj) => obj is Point3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Point3D a, Point3D b) => a.Equals(b);

    public static bool operator !=(Point3D a, Point3D b) => !a.Equals(b);

    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}
