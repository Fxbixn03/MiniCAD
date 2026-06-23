using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class SimilarMatcherTests
{
    private static LineEntity Line(Guid layer) => new(Point2D.Origin, new Point2D(1, 1)) { LayerId = layer };

    [Fact]
    public void Match_TypeAndLayer_SelectsSameTypeOnSameLayer()
    {
        var layerA = Guid.NewGuid();
        var layerB = Guid.NewGuid();
        var reference = Line(layerA);
        var sameTypeSameLayer = Line(layerA);
        var sameTypeOtherLayer = Line(layerB);
        var otherTypeSameLayer = new CircleEntity(Point2D.Origin, 1) { LayerId = layerA };

        IReadOnlyList<IEntity> matches = SimilarMatcher.Match(
            reference,
            new IEntity[] { reference, sameTypeSameLayer, sameTypeOtherLayer, otherTypeSameLayer });

        matches.Should().BeEquivalentTo(new IEntity[] { reference, sameTypeSameLayer });
    }

    [Fact]
    public void Match_TypeOnly_IgnoresLayer()
    {
        var reference = Line(Guid.NewGuid());
        var otherLayer = Line(Guid.NewGuid());
        var circle = new CircleEntity(Point2D.Origin, 1);

        IReadOnlyList<IEntity> matches = SimilarMatcher.Match(
            reference,
            new IEntity[] { reference, otherLayer, circle },
            SimilarityCriteria.Type);

        matches.Should().BeEquivalentTo(new IEntity[] { reference, otherLayer });
    }

    [Fact]
    public void Match_IncludesReferenceItself()
    {
        var reference = Line(Guid.NewGuid());

        IReadOnlyList<IEntity> matches = SimilarMatcher.Match(reference, new IEntity[] { reference });

        matches.Should().ContainSingle().Which.Should().BeSameAs(reference);
    }

    [Fact]
    public void Match_StrokeCriterion_UsesResolver()
    {
        var red = new LineEntity(Point2D.Origin, new Point2D(1, 0)) { StrokeOverride = new StrokeStyle(Color.Red, 1) };
        var alsoRed = new LineEntity(Point2D.Origin, new Point2D(2, 0)) { StrokeOverride = new StrokeStyle(Color.Red, 1) };
        var blue = new LineEntity(Point2D.Origin, new Point2D(3, 0)) { StrokeOverride = new StrokeStyle(Color.Blue, 1) };

        IReadOnlyList<IEntity> matches = SimilarMatcher.Match(
            red,
            new IEntity[] { red, alsoRed, blue },
            SimilarityCriteria.Stroke,
            e => e.StrokeOverride ?? new StrokeStyle(Color.White, 1));

        matches.Should().BeEquivalentTo(new IEntity[] { red, alsoRed });
    }

    [Fact]
    public void Match_EmptyCandidates_ReturnsEmpty()
    {
        var reference = Line(Guid.NewGuid());

        SimilarMatcher.Match(reference, Array.Empty<IEntity>()).Should().BeEmpty();
    }
}
