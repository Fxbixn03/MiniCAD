namespace MiniCAD.Core.Geometry;

/// <summary>A 3D ray with an origin and a (normalized) direction — used for 3D picking.</summary>
public readonly struct Ray3D
{
    public Ray3D(Point3D origin, Vector3D direction)
    {
        Origin = origin;
        Direction = direction.Normalized();
    }

    public Point3D Origin { get; }

    public Vector3D Direction { get; }

    public Point3D PointAt(double t) => Origin + Direction * t;

    /// <summary>
    /// Möller–Trumbore ray/triangle intersection; returns the distance <paramref name="t"/> along
    /// the ray when it hits, ignoring back/parallel cases.
    /// </summary>
    public bool IntersectTriangle(Point3D a, Point3D b, Point3D c, out double t)
    {
        t = 0;
        Vector3D edge1 = b - a;
        Vector3D edge2 = c - a;
        Vector3D h = Direction.Cross(edge2);
        double det = edge1.Dot(h);
        if (Math.Abs(det) <= GeometryMath.Epsilon)
            return false; // parallel

        double invDet = 1.0 / det;
        Vector3D s = Origin - a;
        double u = invDet * s.Dot(h);
        if (u < 0 || u > 1)
            return false;

        Vector3D q = s.Cross(edge1);
        double v = invDet * Direction.Dot(q);
        if (v < 0 || u + v > 1)
            return false;

        t = invDet * edge2.Dot(q);
        return t > GeometryMath.Epsilon;
    }
}
