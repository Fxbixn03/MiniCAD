using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class CommandTests
{
    [Fact]
    public void AddEntityCommand_ExecuteAndUndo_TogglePresence()
    {
        var document = new CadDocument();
        var manager = new UndoRedoManager();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));

        manager.Execute(new AddEntityCommand(document, line));
        document.Entities.Should().ContainSingle();
        manager.CanUndo.Should().BeTrue();

        manager.Undo();
        document.Entities.Should().BeEmpty();
        manager.CanRedo.Should().BeTrue();

        manager.Redo();
        document.Entities.Should().ContainSingle();
    }

    [Fact]
    public void Execute_ClearsRedoHistory()
    {
        var document = new CadDocument();
        var manager = new UndoRedoManager();

        manager.Execute(new AddEntityCommand(document, new LineEntity(Point2D.Origin, new Point2D(1, 0))));
        manager.Undo();
        manager.CanRedo.Should().BeTrue();

        manager.Execute(new AddEntityCommand(document, new LineEntity(Point2D.Origin, new Point2D(0, 1))));

        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void TransformEntitiesCommand_Undo_RestoresOriginalPosition()
    {
        var document = new CadDocument();
        var manager = new UndoRedoManager();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(2, 0));
        document.AddEntity(line);

        manager.Execute(new TransformEntitiesCommand(document, new[] { line }, Matrix2D.Translation(5, 5)));
        line.Start.Should().Be(new Point2D(5, 5));

        manager.Undo();
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Should().Be(new Point2D(2, 0));
    }

    [Fact]
    public void TransformEntitiesCommand_RejectsNonInvertibleMatrix()
    {
        var document = new CadDocument();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));

        Action act = () => _ = new TransformEntitiesCommand(document, new[] { line }, Matrix2D.Scaling(0, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StateChanged_FiresOnExecuteUndoRedo()
    {
        var document = new CadDocument();
        var manager = new UndoRedoManager();
        int raised = 0;
        manager.StateChanged += (_, _) => raised++;

        manager.Execute(new AddEntityCommand(document, new LineEntity(Point2D.Origin, new Point2D(1, 1))));
        manager.Undo();
        manager.Redo();

        raised.Should().Be(3);
    }
}
