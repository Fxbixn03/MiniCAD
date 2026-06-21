using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A named, reusable collection of geometry (a block/symbol) with a base point. One definition
/// can be placed as many <see cref="BlockReferenceEntity"/> instances; the stored entities are
/// in world coordinates as captured, and the instance transform maps the base point onto the
/// insertion point.
/// </summary>
public sealed class BlockDefinition
{
    private readonly List<IEntity> _entities;

    public BlockDefinition(string name, Point2D basePoint, IEnumerable<IEntity> entities)
        : this(Guid.NewGuid(), name, basePoint, entities)
    {
    }

    public BlockDefinition(Guid id, string name, Point2D basePoint, IEnumerable<IEntity> entities)
    {
        Id = id;
        Name = name;
        BasePoint = basePoint;
        _entities = new List<IEntity>(entities);
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>The reference point that lands on a reference's insertion point.</summary>
    public Point2D BasePoint { get; set; }

    /// <summary>Names of the labelled attribute fields instances of this block carry.</summary>
    public List<string> AttributeKeys { get; } = new();

    /// <summary>The block's geometry (in the coordinates captured at definition time).</summary>
    public IReadOnlyList<IEntity> Entities => _entities;

    public override string ToString() => Name;
}
