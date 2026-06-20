using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Coordinates;

/// <summary>
/// The document's reference frame. It carries a user-movable origin (the "Nullpunkt") against
/// which coordinates are reported to the user, while the geometry itself keeps living in the
/// absolute world space. The axes are modelled as full X/Y/Z so the frame is ready for 3D;
/// in the current 2D model only X and Y are meaningful and Z stays zero.
/// </summary>
public sealed class CoordinateSystem
{
    private Point3D _origin = Point3D.Origin;

    /// <summary>The user-defined zero point, in absolute world coordinates.</summary>
    public Point3D Origin
    {
        get => _origin;
        set
        {
            if (_origin == value)
                return;

            _origin = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised whenever the origin moves so views can refresh their coordinate readout.</summary>
    public event EventHandler? Changed;

    /// <summary>Convenience setter for the planar case; keeps the existing Z.</summary>
    public void SetOrigin(Point2D worldPoint) => Origin = new Point3D(worldPoint.X, worldPoint.Y, _origin.Z);

    /// <summary>Expresses an absolute world point relative to the current origin.</summary>
    public Point3D ToLocal(Point2D world) => new(world.X - _origin.X, world.Y - _origin.Y, -_origin.Z);

    /// <summary>Expresses an absolute world point relative to the current origin.</summary>
    public Point3D ToLocal(Point3D world) => new(world.X - _origin.X, world.Y - _origin.Y, world.Z - _origin.Z);

    /// <summary>Maps a coordinate that is relative to the origin back into absolute world space.</summary>
    public Point2D ToWorld(double localX, double localY) => new(_origin.X + localX, _origin.Y + localY);

    public void Reset() => Origin = Point3D.Origin;
}
