using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class DocumentStatisticsTests
{
    [Fact]
    public void Compute_CountsEntitiesByType()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(10, 0)));
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(0, 10)));
        document.AddEntity(new CircleEntity(new Point2D(5, 5), 2));

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.EntityCount.Should().Be(3);
        stats.CountsByType.Should().HaveCount(2);
        // Lines are the most numerous, so they sort first.
        stats.CountsByType[0].Should().Be(new NamedCount("Linie", 2));
        stats.CountsByType[1].Should().Be(new NamedCount("Kreis", 1));
    }

    [Fact]
    public void Compute_CountsEntitiesPerLayer_IncludingEmptyLayers()
    {
        var document = new CadDocument();
        Layer detail = document.AddLayer("Detail", new StrokeStyle(Color.Cyan, 1));
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = detail.Id });

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.CountsByLayer.Should().ContainEquivalentOf(new NamedCount("0", 0));
        stats.CountsByLayer.Should().ContainEquivalentOf(new NamedCount("Detail", 1));
    }

    [Fact]
    public void Compute_ReportsContentBounds()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(new Point2D(0, 0), new Point2D(10, 4)));

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.ContentBounds.Should().NotBeNull();
        stats.ContentBounds!.Value.Width.Should().BeApproximately(10, 1e-9);
        stats.ContentBounds!.Value.Height.Should().BeApproximately(4, 1e-9);
    }

    [Fact]
    public void Compute_CountsConstructionGeometrySeparately()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 0)));
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(0, 1)) { IsConstruction = true });

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.EntityCount.Should().Be(2);
        stats.ConstructionCount.Should().Be(1);
    }

    [Fact]
    public void Compute_ReportsDefinitionCounts()
    {
        var document = new CadDocument();

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.LayerCount.Should().Be(1);
        stats.PartialDrawingCount.Should().Be(1);
        stats.TextStyleCount.Should().Be(1);
        stats.DimStyleCount.Should().Be(1);
        stats.BlockDefinitionCount.Should().Be(0);
    }

    [Fact]
    public void Compute_EmptyDocument_HasNoBounds()
    {
        var document = new CadDocument();

        DocumentStatistics stats = DocumentStatistics.Compute(document);

        stats.EntityCount.Should().Be(0);
        stats.ContentBounds.Should().BeNull();
        stats.CountsByType.Should().BeEmpty();
    }
}
