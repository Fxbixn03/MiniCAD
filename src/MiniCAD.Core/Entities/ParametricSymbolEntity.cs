using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An instance of a parametric catalog symbol (Smartsymbol). Its geometry is regenerated from
/// the <see cref="ParametricSymbolCatalog"/> using the per-instance <see cref="Parameters"/>,
/// then placed at the insertion point with a uniform scale and rotation. Changing a parameter
/// regenerates the geometry. Only the symbol key and parameter values are persisted.
/// </summary>
public sealed class ParametricSymbolEntity : Entity, IEditableEntity
{
    public ParametricSymbolEntity(string symbolKey, Point2D position, IDictionary<string, double>? parameters = null,
        double scale = 1.0, double rotation = 0.0)
    {
        SymbolKey = symbolKey;
        Position = position;
        Scale = scale;
        Rotation = rotation;
        Parameters = parameters is null
            ? ParametricSymbolCatalog.Defaults(symbolKey)
            : new Dictionary<string, double>(parameters);
    }

    private ParametricSymbolEntity(ParametricSymbolEntity source) : base(source)
    {
        SymbolKey = source.SymbolKey;
        Position = source.Position;
        Scale = source.Scale;
        Rotation = source.Rotation;
        Parameters = new Dictionary<string, double>(source.Parameters);
    }

    public string SymbolKey { get; }

    public Point2D Position { get; set; }

    public double Scale { get; set; } = 1.0;

    public double Rotation { get; set; }

    /// <summary>Per-instance parameter values driving the generated geometry.</summary>
    public Dictionary<string, double> Parameters { get; }

    private Matrix2D InstanceMatrix
        => Matrix2D.Scaling(Scale, Scale) * Matrix2D.Rotation(Rotation) * Matrix2D.Translation(Position.X, Position.Y);

    /// <summary>The generated geometry placed under the instance transform.</summary>
    private IEnumerable<IEntity> Instantiate()
    {
        Matrix2D matrix = InstanceMatrix;
        foreach (IEntity entity in ParametricSymbolCatalog.Generate(SymbolKey, Parameters))
        {
            entity.Transform(matrix);
            yield return entity;
        }
    }

    /// <summary>The generated geometry as independent world-space entities (used by Explode, #185).</summary>
    public IReadOnlyList<IEntity> Explode() => Instantiate().ToList();

    public override Rect2D Bounds
    {
        get
        {
            Rect2D? bounds = null;
            foreach (IEntity child in Instantiate())
                bounds = bounds is { } b ? b.Union(child.Bounds) : child.Bounds;
            return bounds ?? Rect2D.FromCenter(Position, 0, 0);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            foreach (IEntity child in Instantiate())
                foreach (Point2D p in child.SnapPoints)
                    yield return p;
        }
    }

    public override bool HitTest(Point2D point, double tolerance) => Instantiate().Any(c => c.HitTest(point, tolerance));

    public override bool IntersectsRect(Rect2D rect) => Instantiate().Any(c => c.IntersectsRect(rect));

    public override void Transform(in Matrix2D matrix)
    {
        var axis = new Vector2D(Math.Cos(Rotation), Math.Sin(Rotation));
        Vector2D transformedAxis = matrix.TransformVector(axis);

        Position = matrix.Transform(Position);
        Scale *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        foreach (IEntity child in Instantiate())
            child.Render(surface, stroke);
    }

    public override IEntity Clone() => new ParametricSymbolEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState() => (Position, Scale, Rotation, new Dictionary<string, double>(Parameters));

    public void RestoreState(object state)
    {
        (Position, Scale, Rotation, Dictionary<string, double> parameters) =
            ((Point2D, double, double, Dictionary<string, double>))state;
        Parameters.Clear();
        foreach ((string key, double value) in parameters)
            Parameters[key] = value;
    }
}
