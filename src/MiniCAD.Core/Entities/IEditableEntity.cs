using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An entity that can be reshaped in place by dragging individual grips, instead of only
/// being moved as a whole. This is the seam the select tool uses for direct editing — e.g.
/// pulling one side of a rectangle while the others stay put, or dragging a line endpoint.
/// The geometry memento (<see cref="CaptureState"/>/<see cref="RestoreState"/>) lets an edit
/// be recorded as a single undoable step.
/// </summary>
public interface IEditableEntity
{
    /// <summary>The handles the user can grab, in world coordinates.</summary>
    IReadOnlyList<Grip> GetGrips();

    /// <summary>Moves the given grip so that it lands on <paramref name="newPosition"/>.</summary>
    void MoveGrip(Grip grip, Point2D newPosition);

    /// <summary>Captures an opaque snapshot of the editable geometry for later restore.</summary>
    object CaptureState();

    /// <summary>Restores geometry previously captured with <see cref="CaptureState"/>.</summary>
    void RestoreState(object state);
}
