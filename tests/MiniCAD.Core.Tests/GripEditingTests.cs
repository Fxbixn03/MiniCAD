using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class GripEditingTests
{
    [Fact]
    public void LineVertexGrip_MovesSingleEndpoint()
    {
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        Grip end = line.GetGrips().Single(g => g.Kind == GripKind.Vertex && g.Index == 1);

        line.MoveGrip(end, new Point2D(10, 5));

        line.Start.Should().Be(new Point2D(0, 0));
        line.End.Should().Be(new Point2D(10, 5));
    }

    [Fact]
    public void RectangleEdgeGrip_MovesOnlyThatSide_OppositeStaysPut()
    {
        // Rectangle corners: (0,0)(4,0)(4,3)(0,3). The right side is segment 1 (P1→P2).
        var rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 3));
        Grip rightEdge = rect.GetGrips().Single(g => g.Kind == GripKind.Edge && g.Index == rect.Points.Count + 1);

        rect.MoveGrip(rightEdge, new Point2D(6, 1.5)); // pull the right side outward by 2

        rect.Points[1].Should().Be(new Point2D(6, 0)); // right side moved
        rect.Points[2].Should().Be(new Point2D(6, 3));
        rect.Points[0].Should().Be(new Point2D(0, 0)); // left side untouched
        rect.Points[3].Should().Be(new Point2D(0, 3));
    }

    [Fact]
    public void CircleRadiusGrip_Resizes_WithoutMovingCenter()
    {
        var circle = new CircleEntity(new Point2D(0, 0), 5);
        Grip radius = circle.GetGrips().Single(g => g.Kind == GripKind.Radius);

        circle.MoveGrip(radius, new Point2D(8, 0));

        circle.Center.Should().Be(new Point2D(0, 0));
        circle.Radius.Should().Be(8);
    }

    [Fact]
    public void EditEntityCommand_UndoRestoresGeometry()
    {
        var document = new CadDocument();
        var line = new LineEntity(new Point2D(0, 0), new Point2D(10, 0));
        document.AddEntity(line);

        object before = line.CaptureState();
        line.MoveGrip(line.GetGrips()[1], new Point2D(10, 7));
        object after = line.CaptureState();

        var command = new EditEntityCommand(document, line, before, after);
        command.Undo();
        line.End.Should().Be(new Point2D(10, 0));
        command.Execute();
        line.End.Should().Be(new Point2D(10, 7));
    }
}
