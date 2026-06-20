namespace MiniCAD.Core.Commands;

/// <summary>
/// A reversible document edit. Commands are the single channel through which the model is
/// mutated by the UI, which is what makes a uniform undo/redo history possible.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>A short, human-readable label (e.g. for an undo menu item).</summary>
    string Name { get; }

    void Execute();

    void Undo();
}
