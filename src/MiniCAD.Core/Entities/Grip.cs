using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>The role a grip plays, which decides how dragging it reshapes the entity.</summary>
public enum GripKind
{
    /// <summary>A single control point (line endpoint, polyline vertex, circle centre).</summary>
    Vertex,

    /// <summary>The midpoint of a segment; dragging it slides the whole edge.</summary>
    Edge,

    /// <summary>A handle that resizes (e.g. a circle's radius).</summary>
    Radius,
}

/// <summary>
/// An editable handle on an entity, exposed so the select tool can draw it and drag it. The
/// <see cref="Index"/> identifies which part of the entity the grip refers to; its meaning is
/// private to the owning entity, which interprets it in <see cref="IEditableEntity.MoveGrip"/>.
/// </summary>
public readonly record struct Grip(Point2D Position, GripKind Kind, int Index);
