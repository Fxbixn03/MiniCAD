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

public class ImageTests
{
    private sealed class ImageRecordingSurface : IRenderSurface
    {
        public int ImageCalls { get; private set; }
        public double LastWidth { get; private set; }

        public void DrawLine(Point2D a, Point2D b, in StrokeStyle stroke) { }
        public void DrawPolyline(IReadOnlyList<Point2D> points, bool closed, in StrokeStyle stroke) { }
        public void DrawCircle(Point2D center, double radius, in StrokeStyle stroke) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, in StrokeStyle stroke) { }
        public void DrawText(string text, Point2D position, double height, double rotation,
            TextHAlign h, TextVAlign v, string? fontFamily, double widthFactor, in StrokeStyle stroke) { }
        public void DrawImage(byte[] encodedImage, Point2D origin, double width, double height, double rotation)
        {
            ImageCalls++;
            LastWidth = width;
        }
    }

    private static readonly byte[] FakeData = { 1, 2, 3, 4 };

    [Fact]
    public void ImageEntity_BoundsAndHitTest_CoverThePlacementBox()
    {
        var image = new ImageEntity(FakeData, new Point2D(10, 20), 100, 50);

        Rect2D b = image.Bounds;
        b.MinX.Should().BeApproximately(10, 1e-9);
        b.MinY.Should().BeApproximately(20, 1e-9);
        b.MaxX.Should().BeApproximately(110, 1e-9);
        b.MaxY.Should().BeApproximately(70, 1e-9);

        image.HitTest(new Point2D(60, 45), 0.1).Should().BeTrue();   // inside
        image.HitTest(new Point2D(200, 200), 0.1).Should().BeFalse();
    }

    [Fact]
    public void ImageEntity_Render_CallsDrawImage()
    {
        var image = new ImageEntity(FakeData, Point2D.Origin, 80, 40);
        var surface = new ImageRecordingSurface();

        image.Render(surface, StrokeStyle.Default);

        surface.ImageCalls.Should().Be(1);
        surface.LastWidth.Should().Be(80);
    }

    [Fact]
    public void ImageEntity_Transform_MovesAndScales()
    {
        var image = new ImageEntity(FakeData, new Point2D(1, 1), 10, 20);

        image.Transform(Matrix2D.Scaling(2, 2, Point2D.Origin));

        image.Origin.Should().Be(new Point2D(2, 2));
        image.Width.Should().BeApproximately(20, 1e-9);
        image.Height.Should().BeApproximately(40, 1e-9);
    }

    [Fact]
    public void ImageEntity_SurvivesPersistenceRoundTrip()
    {
        var doc = new CadDocument();
        doc.AddEntity(new ImageEntity(FakeData, new Point2D(5, 6), 30, 15, 0.2));

        DocumentDto dto = DocumentMapper.ToDto(doc);
        var restored = new CadDocument();
        DocumentMapper.Apply(dto, restored);

        var image = restored.Entities.OfType<ImageEntity>().Should().ContainSingle().Subject;
        image.Data.Should().Equal(FakeData);
        image.Origin.Should().Be(new Point2D(5, 6));
        image.Width.Should().Be(30);
        image.Height.Should().Be(15);
        image.Rotation.Should().Be(0.2);
    }
}
