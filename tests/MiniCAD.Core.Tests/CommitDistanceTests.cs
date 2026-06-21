using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;
using MiniCAD.Core.Viewing;

namespace MiniCAD.Core.Tests;

public class CommitDistanceTests
{
    private static (ToolManager tools, CadDocument doc) Setup()
    {
        var doc = new CadDocument();
        var tools = new ToolManager(doc, new UndoRedoManager(), new Viewport());
        tools.SetActiveTool(new LineTool());
        return (tools, doc);
    }

    private static void PlaceStart(ToolManager tools, Point2D start)
        => tools.PointerDown(new ToolPointerInput { World = start, Button = ToolButton.Left, ClickCount = 1 });

    [Fact]
    public void CommitDistance_DrawsExactLengthAlongCursorDirection()
    {
        var (tools, doc) = Setup();
        PlaceStart(tools, new Point2D(0, 0));

        tools.CommitDistance(10, new Point2D(5, 0)); // aim +X, length 10

        var line = doc.Entities.OfType<LineEntity>().Single();
        line.Start.Should().Be(new Point2D(0, 0));
        line.End.X.Should().BeApproximately(10, 1e-6);
        line.End.Y.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void CommitDistance_UsesDirectionNotCursorDistance()
    {
        var (tools, doc) = Setup();
        PlaceStart(tools, new Point2D(0, 0));

        // Cursor only 1 unit away diagonally, but typed length is 100 → end is 100 along that dir.
        tools.CommitDistance(100, new Point2D(1, 1));

        var line = doc.Entities.OfType<LineEntity>().Single();
        double len = line.Start.DistanceTo(line.End);
        len.Should().BeApproximately(100, 1e-6);
        line.End.X.Should().BeApproximately(line.End.Y, 1e-6); // 45° direction preserved
    }

    [Fact]
    public void CommitDistance_DoesNothing_WhenNoStartOrZeroLengthOrCursorOnAnchor()
    {
        var (tools, doc) = Setup();

        tools.CommitDistance(10, new Point2D(5, 0)); // no start placed yet
        doc.Entities.Should().BeEmpty();

        PlaceStart(tools, new Point2D(0, 0));
        tools.CommitDistance(0, new Point2D(5, 0));            // zero length
        tools.CommitDistance(10, new Point2D(0, 0));           // cursor on the anchor → no direction
        doc.Entities.Should().BeEmpty();
    }
}
