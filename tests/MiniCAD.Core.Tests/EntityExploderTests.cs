using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class EntityExploderTests
{
    [Fact]
    public void Explode_OpenPolyline_YieldsOneLinePerSegment()
    {
        var poly = new PolylineEntity(new[]
        {
            new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10),
        });

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(poly);

        parts.Should().HaveCount(2);
        parts.Should().AllBeOfType<LineEntity>();
        var first = (LineEntity)parts[0];
        first.Start.Should().Be(new Point2D(0, 0));
        first.End.Should().Be(new Point2D(10, 0));
    }

    [Fact]
    public void Explode_ClosedPolyline_IncludesClosingSegment()
    {
        PolylineEntity rect = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(4, 3));

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(rect);

        parts.Should().HaveCount(4); // four sides incl. the closing edge
        parts.Should().AllBeOfType<LineEntity>();
    }

    [Fact]
    public void Explode_Polyline_PartsInheritLayerAndPartialDrawing()
    {
        var layerId = Guid.NewGuid();
        var pdId = Guid.NewGuid();
        var poly = new PolylineEntity(new[] { new Point2D(0, 0), new Point2D(5, 5) })
        {
            LayerId = layerId,
            PartialDrawingId = pdId,
            IsConstruction = true,
        };

        IEntity part = EntityExploder.Explode(poly).Single();
        part.LayerId.Should().Be(layerId);
        part.PartialDrawingId.Should().Be(pdId);
        part.IsConstruction.Should().BeTrue();
    }

    [Fact]
    public void Explode_HatchedPolyline_AlsoDissolvesTheHatchIntoLines()
    {
        var hatch = new HatchPattern("Test",
            new[] { new HatchLineDefinition(0, 1.0, 0) }, Color.Black);
        var poly = PolylineEntity.Rectangle(new Point2D(0, 0), new Point2D(10, 10));
        poly.Fill = hatch;

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(poly);

        parts.Should().AllBeOfType<LineEntity>();
        parts.Count.Should().BeGreaterThan(4); // 4 outline edges + hatch lines
    }

    [Fact]
    public void Explode_BlockReference_YieldsTheContainedEntities()
    {
        var document = new CadDocument();
        var def = new BlockDefinition("Sym", Point2D.Origin, new IEntity[]
        {
            new LineEntity(new Point2D(0, 0), new Point2D(1, 0)),
            new CircleEntity(new Point2D(0, 0), 2),
        });
        document.AddBlockDefinition(def);
        var reference = new BlockReferenceEntity(def, new Point2D(100, 100));

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(reference);

        parts.Should().HaveCount(2);
        parts.Select(p => p.GetType()).Should().Contain(new[] { typeof(LineEntity), typeof(CircleEntity) });
        // Placed at the instance position.
        parts.OfType<CircleEntity>().Single().Center.Should().Be(new Point2D(100, 100));
    }

    [Fact]
    public void Explode_Leader_YieldsLinesAndText()
    {
        var leader = new LeaderEntity(
            new[] { new Point2D(0, 0), new Point2D(10, 10), new Point2D(20, 10) }, "Hinweis");

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(leader);

        parts.OfType<TextEntity>().Should().ContainSingle(t => t.Text == "Hinweis");
        parts.Any(p => p is LineEntity or PolylineEntity).Should().BeTrue();
    }

    [Fact]
    public void Explode_LinearDimension_YieldsLinesAndMeasurementText()
    {
        var dim = new LinearDimensionEntity(
            new Point2D(0, 0), new Point2D(100, 0), new Point2D(0, 20), LinearDimensionKind.Horizontal);

        IReadOnlyList<IEntity> parts = EntityExploder.Explode(dim);

        parts.Should().NotBeEmpty();
        parts.OfType<TextEntity>().Should().NotBeEmpty();          // the measurement text
        parts.Any(p => p is LineEntity or PolylineEntity).Should().BeTrue(); // ext/dim lines + arrows
    }

    [Fact]
    public void Explode_AtomicEntity_ReturnsNothing()
    {
        EntityExploder.CanExplode(new LineEntity(Point2D.Origin, new Point2D(1, 1))).Should().BeFalse();
        EntityExploder.Explode(new CircleEntity(Point2D.Origin, 5)).Should().BeEmpty();
    }
}
