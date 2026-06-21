using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Viewing;

/// <summary>How a <see cref="Camera3D"/> projects the scene.</summary>
public enum ProjectionMode
{
    Perspective,
    Orthographic,
}

/// <summary>
/// A 3D camera orbiting a target point (Z is up, matching the 2D XY ground plane). It builds the
/// view/projection matrices, projects world points to device pixels, and offers orbit/pan/zoom
/// navigation plus standard views — the 3D analogue of <see cref="Viewport"/>.
/// </summary>
public sealed class Camera3D
{
    private const double MinDistance = 1e-3;
    private const double MaxPitch = 89.0 * Math.PI / 180.0;

    public Camera3D()
    {
        SetStandardView(StandardView.Iso);
    }

    public Point3D Target { get; private set; } = Point3D.Origin;

    /// <summary>Eye distance from the target.</summary>
    public double Distance { get; private set; } = 1000.0;

    /// <summary>Azimuth angle about the world Z axis (radians).</summary>
    public double Yaw { get; private set; }

    /// <summary>Elevation angle above the ground plane (radians, clamped to ±89°).</summary>
    public double Pitch { get; private set; }

    public ProjectionMode Mode { get; set; } = ProjectionMode.Perspective;

    public double FieldOfView { get; set; } = 50.0 * Math.PI / 180.0;

    public double Width { get; private set; } = 1.0;
    public double Height { get; private set; } = 1.0;

    public double NearPlane { get; set; } = 1.0;
    public double FarPlane { get; set; } = 1_000_000.0;

    public event EventHandler? Changed;

    /// <summary>The eye position derived from target, orbit angles and distance.</summary>
    public Point3D Eye
    {
        get
        {
            double cp = Math.Cos(Pitch);
            var dir = new Vector3D(cp * Math.Cos(Yaw), cp * Math.Sin(Yaw), Math.Sin(Pitch));
            return Target + dir * Distance;
        }
    }

    /// <summary>World up, swapped to Y when looking (almost) straight down Z so the basis stays valid.</summary>
    private Vector3D Up => Math.Abs(Math.Sin(Pitch)) > 0.9999 ? Vector3D.UnitY : Vector3D.UnitZ;

    public Matrix4 ViewMatrix => Matrix4.CreateLookAt(Eye, Target, Up);

    public Matrix4 ProjectionMatrix
    {
        get
        {
            double aspect = Height <= 0 ? 1.0 : Width / Height;
            if (Mode == ProjectionMode.Orthographic)
            {
                double h = OrthographicHeight;
                return Matrix4.CreateOrthographic(h * aspect, h, NearPlane, FarPlane);
            }

            return Matrix4.CreatePerspective(FieldOfView, aspect, NearPlane, FarPlane);
        }
    }

    /// <summary>View height in world units used for orthographic projection (kept consistent with zoom).</summary>
    private double OrthographicHeight => 2.0 * Distance * Math.Tan(FieldOfView * 0.5);

    public Matrix4 ViewProjection => ViewMatrix * ProjectionMatrix;

    public void Resize(double width, double height)
    {
        if (width == Width && height == Height)
            return;
        Width = width;
        Height = height;
        OnChanged();
    }

    /// <summary>Projects a world point to a device-pixel point; <paramref name="inFront"/> is false if behind the camera.</summary>
    public Point2D Project(Point3D world, out bool inFront)
    {
        Point3D clip = ViewProjection.TransformWithW(world, out double w);
        inFront = w > GeometryMath.Epsilon;
        if (!inFront)
            w = w == 0 ? GeometryMath.Epsilon : Math.Abs(w);

        double ndcX = clip.X / w;
        double ndcY = clip.Y / w;
        return new Point2D((ndcX * 0.5 + 0.5) * Width, (1.0 - (ndcY * 0.5 + 0.5)) * Height);
    }

    /// <summary>Builds the world-space ray through a device-pixel screen point (for picking).</summary>
    public Ray3D RayFromScreen(Point2D screen)
    {
        double nx = 2.0 * screen.X / Math.Max(Width, 1.0) - 1.0;
        double ny = 1.0 - 2.0 * screen.Y / Math.Max(Height, 1.0);

        if (!ViewProjection.TryInvert(out Matrix4 inv))
            return new Ray3D(Eye, (Target - Eye).Normalized());

        Point3D near = Unproject(nx, ny, 0.0, inv);
        Point3D far = Unproject(nx, ny, 1.0, inv);
        return new Ray3D(near, far - near);
    }

    private static Point3D Unproject(double nx, double ny, double nz, in Matrix4 inverseViewProjection)
    {
        Point3D h = inverseViewProjection.TransformWithW(new Point3D(nx, ny, nz), out double w);
        if (Math.Abs(w) <= GeometryMath.Epsilon)
            return h;
        return new Point3D(h.X / w, h.Y / w, h.Z / w);
    }

    public void Orbit(double deltaYaw, double deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch = Math.Clamp(Pitch + deltaPitch, -MaxPitch, MaxPitch);
        OnChanged();
    }

    /// <summary>Pans the target within the view plane by a screen-space delta (device pixels).</summary>
    public void Pan(double screenDx, double screenDy)
    {
        Matrix4 view = ViewMatrix;
        // View-space right/up axes live in the columns of the view's basis (row-vector convention).
        var right = new Vector3D(view.M11, view.M21, view.M31);
        var up = new Vector3D(view.M12, view.M22, view.M32);
        double worldPerPixel = OrthographicHeight / Math.Max(Height, 1.0);
        Target -= right * (screenDx * worldPerPixel);
        Target += up * (screenDy * worldPerPixel);
        OnChanged();
    }

    public void Zoom(double factor)
    {
        Distance = Math.Max(Distance / factor, MinDistance);
        OnChanged();
    }

    public void ZoomToFit(BoundingBox3D bounds)
    {
        Target = bounds.Center;
        double radius = Math.Max(bounds.Size.Length * 0.5, MinDistance);
        Distance = radius / Math.Max(Math.Sin(FieldOfView * 0.5), 1e-3) * 1.1;
        OnChanged();
    }

    public void SetStandardView(StandardView view)
    {
        (Yaw, Pitch) = view switch
        {
            StandardView.Top => (-Math.PI / 2, Math.PI / 2), // straight down (Up swaps to Y)
            StandardView.Front => (-Math.PI / 2, 0.0),
            StandardView.Right => (0.0, 0.0),
            _ => (-Math.PI / 4, 30.0 * Math.PI / 180.0), // Iso
        };
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Standard camera orientations.</summary>
public enum StandardView
{
    Top,
    Front,
    Right,
    Iso,
}
