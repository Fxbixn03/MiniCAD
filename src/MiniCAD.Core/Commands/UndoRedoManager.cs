namespace MiniCAD.Core.Commands;

/// <summary>
/// Maintains the undo/redo history. All model edits should go through <see cref="Execute"/>
/// so they become reversible and discoverable through a single history.
/// </summary>
public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoName => CanUndo ? _undo.Peek().Name : null;

    public string? RedoName => CanRedo ? _redo.Peek().Name : null;

    /// <summary>Raised whenever the undo/redo availability changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Runs a command and pushes it onto the undo history, clearing the redo stack.</summary>
    public void Execute(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        OnStateChanged();
    }

    /// <summary>
    /// Records a command that was <em>already applied</em> (e.g. a live drag) onto the undo
    /// history without executing it again. Undo/redo behave exactly as for <see cref="Execute"/>.
    /// </summary>
    public void Push(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        _undo.Push(command);
        _redo.Clear();
        OnStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        IUndoableCommand command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        OnStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        IUndoableCommand command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        OnStateChanged();
    }

    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
            return;

        _undo.Clear();
        _redo.Clear();
        OnStateChanged();
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
