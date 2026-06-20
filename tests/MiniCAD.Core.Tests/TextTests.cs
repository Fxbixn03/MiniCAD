using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Snapping;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class TextTests
{
    private sealed class TestContext : IToolContext
    {
        public TestContext(CadDocument document) => Document = document;

        public ICadDocument Document { get; }

        public Selection Selection { get; } = new();

        public double PickTolerance { get; set; } = 0.5;

        public StrokeStyle? DefaultStrokeOverride { get; set; }

        public UndoRedoManager Commands { get; } = new();

        public void Execute(IUndoableCommand command) => Commands.Execute(command);

        public void Push(IUndoableCommand command) => Commands.Push(command);

        public SnapResult Snap(Point2D world) => SnapResult.Unsnapped(world);

        public void RequestRedraw()
        {
        }

        public void ReturnToDefaultTool()
        {
        }
    }

    /// <summary>Captures DrawText calls so entity rendering can be asserted without SkiaSharp.</summary>
    private sealed class RecordingSurface : IRenderSurface
    {
        public List<(string Text, Point2D Position, double Height, double Rotation, TextHAlign H, TextVAlign V)> Texts { get; } = new();

        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) { }
        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke) { }
        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke) { }

        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign horizontalAlignment, TextVAlign verticalAlignment, in StrokeStyle stroke)
            => Texts.Add((text, position, height, rotation, horizontalAlignment, verticalAlignment));
    }

    // ----- TextEntity -----

    [Fact]
    public void TextEntity_SnapsToAndHitTestsAroundItsInsertionPoint()
    {
        var text = new TextEntity(new Point2D(2, 3), "Hallo", 10, 0, TextHAlign.Left, TextVAlign.Baseline);

        text.SnapPoints.Should().Contain(new Point2D(2, 3));
        text.HitTest(new Point2D(2, 3), 0.5).Should().BeTrue();         // at the insertion point
        text.HitTest(new Point2D(2 + 5, 3 + 5), 0.5).Should().BeTrue(); // inside the box
        text.HitTest(new Point2D(-50, -50), 0.5).Should().BeFalse();
    }

    [Fact]
    public void TextEntity_Render_EmitsASingleDrawTextWithItsProperties()
    {
        var text = new TextEntity(new Point2D(1, 1), "Abc", 8, 0.5, TextHAlign.Center, TextVAlign.Middle);
        var surface = new RecordingSurface();

        text.Render(surface, StrokeStyle.Default);

        surface.Texts.Should().ContainSingle();
        var call = surface.Texts[0];
        call.Text.Should().Be("Abc");
        call.Position.Should().Be(new Point2D(1, 1));
        call.Height.Should().Be(8);
        call.Rotation.Should().Be(0.5);
        call.H.Should().Be(TextHAlign.Center);
        call.V.Should().Be(TextVAlign.Middle);
    }

    [Fact]
    public void TextEntity_Transform_MovesScalesAndRotates()
    {
        var text = new TextEntity(new Point2D(1, 0), "x", 10);

        text.Transform(Matrix2D.Rotation(System.Math.PI / 2) * Matrix2D.Scaling(2, 2));

        text.Position.X.Should().BeApproximately(0, 1e-9);
        text.Position.Y.Should().BeApproximately(2, 1e-9);
        text.Height.Should().BeApproximately(20, 1e-9);
        text.Rotation.Should().BeApproximately(System.Math.PI / 2, 1e-9);
    }

    [Fact]
    public void TextEntity_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new TextEntity(new Point2D(4, 5), "Plan", 9, 0.25, TextHAlign.Right, TextVAlign.Top));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var text = restored.Entities.OfType<TextEntity>().Should().ContainSingle().Subject;
        text.Position.Should().Be(new Point2D(4, 5));
        text.Text.Should().Be("Plan");
        text.Height.Should().Be(9);
        text.Rotation.Should().Be(0.25);
        text.HorizontalAlignment.Should().Be(TextHAlign.Right);
        text.VerticalAlignment.Should().Be(TextVAlign.Top);
    }

    // ----- MTextEntity -----

    [Fact]
    public void MTextEntity_WrapsOnExplicitNewlines()
    {
        var mtext = new MTextEntity(Point2D.Origin, "a\nb\nc", 10);

        mtext.WrappedLines().Should().Equal("a", "b", "c");
    }

    [Fact]
    public void MTextEntity_WordWrapsToWidth()
    {
        // Glyph width estimate is 0.6 × height = 6 units/char. "aaaa bbbb" = 9 chars ≈ 54 ≤ 60.
        var mtext = new MTextEntity(Point2D.Origin, "aaaa bbbb cccc", 10, width: 60);

        mtext.WrappedLines().Should().Equal("aaaa bbbb", "cccc");
    }

    [Fact]
    public void MTextEntity_Render_EmitsOneDrawTextPerLine()
    {
        var mtext = new MTextEntity(Point2D.Origin, "one\ntwo", 10, 0, 0, TextHAlign.Left, TextVAlign.Top);
        var surface = new RecordingSurface();

        mtext.Render(surface, StrokeStyle.Default);

        surface.Texts.Select(t => t.Text).Should().Equal("one", "two");
        // Lines flow downward (descending world Y) from the anchor.
        surface.Texts[1].Position.Y.Should().BeLessThan(surface.Texts[0].Position.Y);
    }

    [Fact]
    public void MTextEntity_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new MTextEntity(new Point2D(1, 2), "Zeile 1\nZeile 2", 7, 120, 0.1, TextHAlign.Center, TextVAlign.Middle));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var mtext = restored.Entities.OfType<MTextEntity>().Should().ContainSingle().Subject;
        mtext.Position.Should().Be(new Point2D(1, 2));
        mtext.Text.Should().Be("Zeile 1\nZeile 2");
        mtext.Height.Should().Be(7);
        mtext.Width.Should().Be(120);
        mtext.Rotation.Should().Be(0.1);
        mtext.HorizontalAlignment.Should().Be(TextHAlign.Center);
        mtext.VerticalAlignment.Should().Be(TextVAlign.Middle);
    }

    // ----- TextTool -----

    [Fact]
    public void TextTool_Click_RaisesEditRequestForNewText()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new TextTool { Height = 10 };
        tool.Activate(ctx);

        TextEditRequest? request = null;
        tool.EditRequested += r => request = r;

        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 6), Button = ToolButton.Left });

        request.Should().NotBeNull();
        request!.Value.AnchorWorld.Should().Be(new Point2D(5, 6));
        request.Value.InitialText.Should().BeEmpty();
        doc.Entities.Should().BeEmpty(); // nothing committed yet
    }

    [Fact]
    public void TextTool_Commit_AddsConfiguredTextEntity()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new TextTool { Height = 14, HorizontalAlignment = TextHAlign.Center, VerticalAlignment = TextVAlign.Middle };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(5, 6), Button = ToolButton.Left });
        tool.Commit("Hallo Welt");

        var text = doc.Entities.OfType<TextEntity>().Should().ContainSingle().Subject;
        text.Position.Should().Be(new Point2D(5, 6));
        text.Text.Should().Be("Hallo Welt");
        text.Height.Should().Be(14);
        text.HorizontalAlignment.Should().Be(TextHAlign.Center);
        text.VerticalAlignment.Should().Be(TextVAlign.Middle);
    }

    [Fact]
    public void TextTool_CommitEmptyText_AddsNothing()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new TextTool();
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(1, 1), Button = ToolButton.Left });
        tool.Commit(string.Empty);

        doc.Entities.Should().BeEmpty();
    }

    [Fact]
    public void TextTool_Multiline_CreatesMTextEntity()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new TextTool { Multiline = true };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.Commit("line 1\nline 2");

        doc.Entities.OfType<MTextEntity>().Should().ContainSingle()
            .Which.Text.Should().Be("line 1\nline 2");
    }

    [Fact]
    public void TextTool_ClickOnExistingText_EditsItUndoably()
    {
        var doc = new CadDocument();
        var existing = new TextEntity(new Point2D(0, 0), "alt", 10);
        doc.AddEntity(existing);

        var ctx = new TestContext(doc);
        var tool = new TextTool();
        tool.Activate(ctx);

        TextEditRequest? request = null;
        tool.EditRequested += r => request = r;

        // Click on the existing text's insertion point edits it instead of creating a new one.
        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        request!.Value.InitialText.Should().Be("alt");

        tool.Commit("neu");

        doc.Entities.OfType<TextEntity>().Should().ContainSingle().Which.Text.Should().Be("neu");

        ctx.Commands.Undo();
        existing.Text.Should().Be("alt");
    }
}
