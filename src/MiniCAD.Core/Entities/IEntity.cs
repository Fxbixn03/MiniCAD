using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// A drawable geometric object in a document. Entities are the primary extension point of
/// the model: a new shape only has to implement this interface, expressing its appearance
/// through the backend-agnostic <see cref="IRenderSurface"/>.
/// </summary>
public interface IEntity
{
    /// <summary>Stable identity, assigned once at construction.</summary>
    EntityId Id { get; }

    /// <summary>The id of the owning <c>Layer</c>; <see cref="Guid.Empty"/> means "the document's active layer".</summary>
    Guid LayerId { get; set; }

    /// <summary>The id of the owning <c>PartialDrawing</c> (Teilbild); <see cref="Guid.Empty"/> means "the active Teilbild".</summary>
    Guid PartialDrawingId { get; set; }

    /// <summary>Group membership; entities sharing a non-empty id are selected/moved as a unit.</summary>
    Guid GroupId { get; set; }

    /// <summary>An optional per-entity stroke that overrides the layer default when set.</summary>
    StrokeStyle? StrokeOverride { get; set; }

    /// <summary>
    /// True for construction geometry (Hilfskonstruktion): drawn in a distinct style and
    /// snappable, but excluded from printing/export. Can be toggled to convert to/from normal
    /// geometry.
    /// </summary>
    bool IsConstruction { get; set; }

    /// <summary>The axis-aligned bounding box in world coordinates.</summary>
    Rect2D Bounds { get; }

    /// <summary>
    /// Characteristic points the cursor can snap to (endpoints, vertices, centers, …).
    /// Adding a snap-aware shape is just a matter of returning the right points here.
    /// </summary>
    IEnumerable<Point2D> SnapPoints { get; }

    /// <summary>Returns <c>true</c> if <paramref name="point"/> lies within <paramref name="tolerance"/> world units of the geometry.</summary>
    bool HitTest(Point2D point, double tolerance);

    /// <summary>
    /// Returns <c>true</c> if the geometry crosses or lies within <paramref name="rect"/>. This is
    /// the "crossing" box-selection test (left→right window selection uses <see cref="Bounds"/>
    /// containment instead).
    /// </summary>
    bool IntersectsRect(Rect2D rect);

    /// <summary>Applies an affine transform to the entity in place.</summary>
    void Transform(in Matrix2D matrix);

    /// <summary>Emits the entity's geometry to <paramref name="surface"/> using the resolved <paramref name="stroke"/>.</summary>
    void Render(IRenderSurface surface, in StrokeStyle stroke);

    /// <summary>Creates a deep copy with a fresh <see cref="Id"/>.</summary>
    IEntity Clone();
}
