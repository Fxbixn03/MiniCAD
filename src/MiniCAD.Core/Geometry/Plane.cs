namespace MiniCAD.Core.Geometry;

/// <summary>An infinite plane <c>Normal · p + D = 0</c> with a unit normal (double precision).</summary>
public readonly struct Plane
{
    public Plane(Vector3D normal, double d)
    {
        Normal = normal;
        D = d;
    }

    public Vector3D Normal { get; }

    public double D { get; }

    public static Plane FromPointNormal(Point3D point, Vector3D normal)
    {
        Vector3D n = normal.Normalized();
        return new Plane(n, -n.Dot(point.ToVector3D()));
    }

    /// <summary>The plane through three points (normal by the right-hand rule a→b→c).</summary>
    public static Plane FromPoints(Point3D a, Point3D b, Point3D c)
        => FromPointNormal(a, (b - a).Cross(c - a));

    /// <summary>Signed distance from <paramref name="point"/> (positive on the normal's side).</summary>
    public double SignedDistance(Point3D point) => Normal.Dot(point.ToVector3D()) + D;

    /// <summary>The orthogonal projection of <paramref name="point"/> onto the plane.</summary>
    public Point3D Project(Point3D point) => point - Normal * SignedDistance(point);

    /// <summary>Intersects a ray with the plane; <c>false</c> if parallel.</summary>
    public bool IntersectRay(Ray3D ray, out double t, out Point3D point)
    {
        double denom = Normal.Dot(ray.Direction);
        if (Math.Abs(denom) <= GeometryMath.Epsilon)
        {
            t = 0;
            point = ray.Origin;
            return false;
        }

        t = -(Normal.Dot(ray.Origin.ToVector3D()) + D) / denom;
        point = ray.PointAt(t);
        return true;
    }
}
