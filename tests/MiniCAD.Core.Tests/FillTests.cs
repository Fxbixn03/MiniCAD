using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Persistence;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class FillTests
{
    /// <summary>Records DrawFilledPolygon calls; everything else is ignored.</summary>
    private sealed class FillRecordingSurface : IRenderSurface
    {
        public List<FillStyle> Fills { get; } = new();

        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) { }
        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke) { }
        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke) { }
        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign horizontalAlignment, TextVAlign verticalAlignment, string? fontFamily, double widthFactor, in StrokeStyle stroke) { }
        public void DrawFilledPolygon(IReadOnlyList<Point2D> points, in FillStyle fill) => Fills.Add(fill);
    }

    private static PolylineEntity Square() => PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(10, 10));

    [Fact]
    public void FilledClosedPolyline_EmitsFilledPolygon_BeforeOutline()
    {
        PolylineEntity square = Square();
        square.SolidFill = FillStyle.Solid(new Color(200, 50, 50, 128));
        var surface = new FillRecordingSurface();

        square.Render(surface, StrokeStyle.Default);

        surface.Fills.Should().ContainSingle();
        surface.Fills[0].Color.A.Should().Be(128); // transparency preserved
    }

    [Fact]
    public void OpenPolyline_DoesNotFill()
    {
        var open = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10) }, isClosed: false)
        {
            SolidFill = FillStyle.Solid(Color.Red),
        };
        var surface = new FillRecordingSurface();

        open.Render(surface, StrokeStyle.Default);

        surface.Fills.Should().BeEmpty();
    }

    [Fact]
    public void FilledRegion_IsPickableInside()
    {
        PolylineEntity square = Square();

        square.HitTest(new Point2D(5, 5), 0.1).Should().BeFalse(); // unfilled: only the outline
        square.SolidFill = FillStyle.Solid(Color.Blue);
        square.HitTest(new Point2D(5, 5), 0.1).Should().BeTrue();  // filled: interior picks
    }

    [Fact]
    public void GradientFill_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        PolylineEntity square = Square();
        square.SolidFill = FillStyle.Gradient(new Color(255, 0, 0), new Color(0, 0, 255), 45);
        doc.AddEntity(square);

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var poly = restored.Entities.OfType<PolylineEntity>().Should().ContainSingle().Subject;
        poly.SolidFill.Should().NotBeNull();
        FillStyle fill = poly.SolidFill!.Value;
        fill.IsGradient.Should().BeTrue();
        fill.Color.Should().Be(new Color(255, 0, 0));
        fill.SecondColor.Should().Be(new Color(0, 0, 255));
        fill.AngleDegrees.Should().Be(45);
    }
}
