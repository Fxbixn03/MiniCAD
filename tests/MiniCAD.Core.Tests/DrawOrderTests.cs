using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class DrawOrderTests
{
    private static LineEntity L() => new(Point2D.Origin, new Point2D(1, 1));

    [Fact]
    public void BringToFront_MovesSelectionToEnd()
    {
        var a = L();
        var b = L();
        var c = L();
        var order = new IEntity[] { a, b, c };

        IReadOnlyList<IEntity> result = DrawOrder.BringToFront(order, new[] { a });

        result.Should().ContainInOrder(b, c, a);
    }

    [Fact]
    public void SendToBack_MovesSelectionToStart()
    {
        var a = L();
        var b = L();
        var c = L();

        IReadOnlyList<IEntity> result = DrawOrder.SendToBack(new IEntity[] { a, b, c }, new[] { c });

        result.Should().ContainInOrder(c, a, b);
    }

    [Fact]
    public void BringForward_MovesOneStepTowardEnd()
    {
        var a = L();
        var b = L();
        var c = L();

        IReadOnlyList<IEntity> result = DrawOrder.BringForward(new IEntity[] { a, b, c }, new[] { a });

        result.Should().ContainInOrder(b, a, c);
    }

    [Fact]
    public void SendBackward_MovesOneStepTowardStart()
    {
        var a = L();
        var b = L();
        var c = L();

        IReadOnlyList<IEntity> result = DrawOrder.SendBackward(new IEntity[] { a, b, c }, new[] { c });

        result.Should().ContainInOrder(a, c, b);
    }

    [Fact]
    public void BringForward_KeepsSelectedBlockTogether()
    {
        var a = L();
        var b = L();
        var c = L();
        var d = L();

        // a and b selected; both should advance one step, staying adjacent.
        IReadOnlyList<IEntity> result = DrawOrder.BringForward(new IEntity[] { a, b, c, d }, new[] { a, b });

        result.Should().ContainInOrder(c, a, b, d);
    }

    [Fact]
    public void ReorderCommand_IsUndoable()
    {
        var doc = new CadDocument();
        var a = L();
        var b = L();
        doc.AddEntity(a);
        doc.AddEntity(b);
        var manager = new UndoRedoManager();

        IReadOnlyList<IEntity> before = doc.GetEntityOrder();
        IReadOnlyList<IEntity> after = DrawOrder.BringToFront(before, new[] { a });
        manager.Execute(new ReorderEntitiesCommand(doc, before, after));

        doc.Entities.Should().ContainInOrder(b, a);

        manager.Undo();
        doc.Entities.Should().ContainInOrder(a, b);
    }

    [Fact]
    public void SetEntityOrder_RejectsNonPermutation()
    {
        var doc = new CadDocument();
        doc.AddEntity(L());

        Action bad = () => doc.SetEntityOrder(new[] { L(), L() });

        bad.Should().Throw<ArgumentException>();
    }
}
