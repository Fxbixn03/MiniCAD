namespace MiniCAD.Core.Commands;

/// <summary>
/// Groups several commands into one undo step. Sub-commands are executed in order and
/// undone in reverse order, so e.g. "delete selection" becomes a single history entry.
/// </summary>
public sealed class CompositeCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _commands;

    public CompositeCommand(string name, IReadOnlyList<IUndoableCommand> commands)
    {
        Name = name;
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public string Name { get; }

    public void Execute()
    {
        for (int i = 0; i < _commands.Count; i++)
            _commands[i].Execute();
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
            _commands[i].Undo();
    }
}
