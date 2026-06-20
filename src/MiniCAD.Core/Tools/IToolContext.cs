using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tools;

/// <summary>
/// The services a tool may use while it runs. It is the seam between a tool and the rest of
/// the editor: tools never touch the document or undo stack directly, they go through here.
/// </summary>
public interface IToolContext
{
    ICadDocument Document { get; }

    Selection Selection { get; }

    /// <summary>Hit-test tolerance in world units, scaled so it stays constant on screen.</summary>
    double PickTolerance { get; }

    /// <summary>
    /// A stroke applied as an override to entities created by drawing tools (set by the
    /// Assistant when reusing a predefined element). <c>null</c> means "use the layer default".
    /// </summary>
    StrokeStyle? DefaultStrokeOverride { get; }

    /// <summary>Switches back to the idle/select tool (e.g. after a draw is cancelled with Esc).</summary>
    void ReturnToDefaultTool();

    /// <summary>Runs a command now and records it for undo.</summary>
    void Execute(IUndoableCommand command);

    /// <summary>Records an already-applied command for undo without re-running it (live drags).</summary>
    void Push(IUndoableCommand command);

    /// <summary>Resolves a free world point to the nearest snap target (or returns it unchanged).</summary>
    SnapResult Snap(Point2D world);

    /// <summary>Asks the host to repaint, e.g. after a preview changes.</summary>
    void RequestRedraw();
}
