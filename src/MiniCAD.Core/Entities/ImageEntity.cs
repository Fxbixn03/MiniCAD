using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A raster image placed as an underlay (e.g. a scanned plan to trace over). It carries the
/// encoded image bytes plus a world-space placement: <see cref="Origin"/> is the lower-left
/// corner, <see cref="Width"/>/<see cref="Height"/> the size in world units and
/// <see cref="Rotation"/> the CCW angle. Rendering goes through <see cref="IRenderSurface.DrawImage"/>;
/// the box geometry reuses the shared <see cref="TextBox"/> helpers.
/// </summary>
public sealed class ImageEntity : Entity, IEditableEntity
{
    private double _width;
    private double _height;

    public ImageEntity(byte[] data, Point2D origin, double width, double height, double rotation = 0.0)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Origin = origin;
        Width = width;
        Height = height;
        Rotation = rotation;
    }

    private ImageEntity(ImageEntity source) : base(source)
    {
        Data = source.Data;
        Origin = source.Origin;
        _width = source._width;
        _height = source._height;
        Rotation = source.Rotation;
    }

    /// <summary>The encoded image bytes (PNG/JPEG/…), shared by clones.</summary>
    public byte[] Data { get; }

    /// <summary>Lower-left corner of the image in world coordinates.</summary>
    public Point2D Origin { get; set; }

    public double Width
    {
        get => _width;
        set => _width = Math.Abs(value);
    }

    public double Height
    {
        get => _height;
        set => _height = Math.Abs(value);
    }

    /// <summary>Rotation in radians, CCW about <see cref="Origin"/>.</summary>
    public double Rotation { get; set; }

    /// <summary>Transform from the unrotated image-local frame (origin = lower-left) to world.</summary>
    private Matrix2D ToWorld => Matrix2D.Rotation(Rotation) * Matrix2D.Translation(Origin.X, Origin.Y);

    private Rect2D LocalBox => new(0, 0, _width, _height);

    private Point2D[] Corners() => TextBox.Corners(LocalBox, ToWorld);

    public override Rect2D Bounds => TextBox.Bounds(Corners());

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            foreach (Point2D corner in Corners())
                yield return corner;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => TextBox.Contains(LocalBox, ToWorld, point, tolerance);

    public override bool IntersectsRect(Rect2D rect) => TextBox.IntersectsRect(Corners(), rect);

    public override void Transform(in Matrix2D matrix)
    {
        var axis = new Vector2D(Math.Cos(Rotation), Math.Sin(Rotation));
        Vector2D transformedAxis = matrix.TransformVector(axis);

        Origin = matrix.Transform(Origin);
        Width *= matrix.UniformScale;
        Height *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
        => surface.DrawImage(Data, Origin, Width, Height, Rotation);

    public override IEntity Clone() => new ImageEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips()
    {
        Point2D[] corners = Corners();
        return new[]
        {
            new Grip(corners[0], GripKind.Vertex, 0), // lower-left (move origin)
            new Grip(corners[2], GripKind.Radius, 2), // upper-right (resize)
        };
    }

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
        {
            Origin = newPosition;
            return;
        }

        // Resize so the far corner follows the cursor, measured in the image-local frame.
        if (ToWorld.TryInvert(out Matrix2D toLocal))
        {
            Point2D local = toLocal.Transform(newPosition);
            Width = local.X;
            Height = local.Y;
        }
    }

    public object CaptureState() => (Origin, Width, Height, Rotation);

    public void RestoreState(object state) => (Origin, Width, Height, Rotation) = ((Point2D, double, double, double))state;
}
