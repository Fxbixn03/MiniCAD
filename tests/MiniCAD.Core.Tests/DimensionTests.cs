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

public class DimensionTests
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
        public void RequestRedraw() { }
        public void ReturnToDefaultTool() { }
    }

    private sealed class CountingSurface : IRenderSurface
    {
        public int Lines { get; private set; }
        public int Texts { get; private set; }
        public string? LastText { get; private set; }

        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) => Lines++;
        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke) { }
        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke) { }
        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign h, TextVAlign v, string? fontFamily, double widthFactor, in StrokeStyle stroke)
        {
            Texts++;
            LastText = text;
        }
    }

    // ----- #25 DimStyle -----

    [Fact]
    public void Document_HasADefaultDimStyle()
    {
        var doc = new CadDocument();
        doc.DimStyles.Should().ContainSingle();
        doc.ActiveDimStyle.Should().Be(doc.DefaultDimStyle);
    }

    [Fact]
    public void UpdateDimStyle_PropagatesToAssignedDimensions()
    {
        var doc = new CadDocument();
        DimStyle style = doc.AddDimStyle("Groß");
        var dim = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(100, 0), new Point2D(0, 20));
        CadDocument.ApplyDimStyle(dim, style);
        doc.AddEntity(dim);

        doc.UpdateDimStyle(style, textHeight: 25, arrowSize: 15, extensionOffset: 2, extensionOvershoot: 4, decimalPlaces: 2);

        dim.TextHeight.Should().Be(25);
        dim.ArrowSize.Should().Be(15);
        dim.DecimalPlaces.Should().Be(2);
    }

    // ----- #26 Linear dimension -----

    [Fact]
    public void AlignedDimension_MeasuresTrueDistance()
    {
        var dim = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(3, 4), new Point2D(0, 10),
            LinearDimensionKind.Aligned);

        dim.MeasuredValue.Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public void HorizontalAndVerticalDimensions_MeasureAxisDistance()
    {
        var horizontal = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(3, 4), new Point2D(0, -10),
            LinearDimensionKind.Horizontal);
        var vertical = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(3, 4), new Point2D(-10, 0),
            LinearDimensionKind.Vertical);

        horizontal.MeasuredValue.Should().BeApproximately(3, 1e-9);
        vertical.MeasuredValue.Should().BeApproximately(4, 1e-9);
    }

    [Fact]
    public void DisplayText_RoundsToDecimalPlaces_AndHonoursOverride()
    {
        var dim = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(12.345, 0), new Point2D(0, 10),
            LinearDimensionKind.Horizontal) { DecimalPlaces = 1 };

        dim.DisplayText.Should().Be("12.3");
        dim.TextOverride = "≈ 12";
        dim.DisplayText.Should().Be("≈ 12");
    }

    [Fact]
    public void Render_EmitsExtensionLines_DimensionLine_AndText()
    {
        var dim = new LinearDimensionEntity(new Point2D(0, 0), new Point2D(50, 0), new Point2D(0, 20),
            LinearDimensionKind.Horizontal);
        var surface = new CountingSurface();

        dim.Render(surface, StrokeStyle.Default);

        surface.Lines.Should().BeGreaterThanOrEqualTo(3); // 2 extension lines + dimension line (+ arrows are polylines)
        surface.Texts.Should().Be(1);
        surface.LastText.Should().Be("50");
    }

    [Fact]
    public void LinearDimensionTool_ThreeClicks_CreatesDimension()
    {
        var doc = new CadDocument();
        var ctx = new TestContext(doc);
        var tool = new LinearDimensionTool { Kind = LinearDimensionKind.Horizontal };
        tool.Activate(ctx);

        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 0), Button = ToolButton.Left });
        tool.PointerDown(new ToolPointerInput { World = new Point2D(40, 0), Button = ToolButton.Left });
        doc.Entities.Should().BeEmpty();
        tool.PointerDown(new ToolPointerInput { World = new Point2D(0, 15), Button = ToolButton.Left });

        var dim = doc.Entities.OfType<LinearDimensionEntity>().Should().ContainSingle().Subject;
        dim.MeasuredValue.Should().BeApproximately(40, 1e-9);
        dim.DimStyleId.Should().Be(doc.ActiveDimStyle.Id);
    }

    [Fact]
    public void LinearDimension_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        DimStyle style = doc.AddDimStyle("Plan");
        var dim = new LinearDimensionEntity(new Point2D(1, 2), new Point2D(11, 2), new Point2D(1, 12),
            LinearDimensionKind.Vertical) { TextOverride = "VAR" };
        CadDocument.ApplyDimStyle(dim, style);
        doc.AddEntity(dim);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var loaded = restored.Entities.OfType<LinearDimensionEntity>().Should().ContainSingle().Subject;
        loaded.P1.Should().Be(new Point2D(1, 2));
        loaded.P2.Should().Be(new Point2D(11, 2));
        loaded.DimLinePoint.Should().Be(new Point2D(1, 12));
        loaded.Kind.Should().Be(LinearDimensionKind.Vertical);
        loaded.TextOverride.Should().Be("VAR");
        loaded.DimStyleId.Should().Be(style.Id);
        restored.DimStyles.Should().Contain(s => s.Name == "Plan");
    }
}
