using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Coordinates;

/// <summary>
/// A work plane / user coordinate system (UCS): an origin plus two orthonormal in-plane axes
/// (the normal is their cross product). It maps between in-plane 2D coordinates and world 3D
/// coordinates — the bridge that lets planar operations live on arbitrary planes in space
/// (#115). The default <see cref="Top"/> plane is the world XY plane, so the existing 2D path is
/// unchanged.
/// </summary>
public readonly struct WorkPlane
{
    public WorkPlane(Point3D origin, Vector3D xAxis, Vector3D yAxis)
    {
        Origin = origin;
        XAxis = xAxis.Normalized();
        YAxis = yAxis.Normalized();
    }

    public Point3D Origin { get; }
    public Vector3D XAxis { get; }
    public Vector3D YAxis { get; }

    public Vector3D Normal => XAxis.Cross(YAxis).Normalized();

    /// <summary>The world XY ground plane (default).</summary>
    public static WorkPlane Top => new(Point3D.Origin, Vector3D.UnitX, Vector3D.UnitY);

    /// <summary>The world XZ plane (front elevation).</summary>
    public static WorkPlane Front => new(Point3D.Origin, Vector3D.UnitX, Vector3D.UnitZ);

    /// <summary>The world YZ plane (side elevation).</summary>
    public static WorkPlane Right => new(Point3D.Origin, Vector3D.UnitY, Vector3D.UnitZ);

    /// <summary>A plane through three points: origin = a, X toward b, normal from a→b × a→c.</summary>
    public static WorkPlane FromThreePoints(Point3D a, Point3D b, Point3D c)
    {
        Vector3D x = (b - a).Normalized();
        Vector3D normal = (b - a).Cross(c - a).Normalized();
        Vector3D y = normal.Cross(x).Normalized();
        return new WorkPlane(a, x, y);
    }

    /// <summary>Lifts an in-plane point to world coordinates.</summary>
    public Point3D ToWorld(Point2D planar) => Origin + XAxis * planar.X + YAxis * planar.Y;

    /// <summary>Projects a world point onto the plane's 2D coordinates.</summary>
    public Point2D ToPlane(Point3D world)
    {
        Vector3D d = world - Origin;
        return new Point2D(d.Dot(XAxis), d.Dot(YAxis));
    }
}
