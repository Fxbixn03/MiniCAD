using FluentAssertions;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class TextSearchTests
{
    [Fact]
    public void Matches_IsCaseInsensitive_ByDefault()
    {
        TextSearch.Matches("Hello World", "world", matchCase: false, wholeWord: false).Should().BeTrue();
        TextSearch.Matches("Hello World", "world", matchCase: true, wholeWord: false).Should().BeFalse();
    }

    [Fact]
    public void Matches_WholeWord_RequiresWordBoundaries()
    {
        TextSearch.Matches("category", "cat", matchCase: false, wholeWord: true).Should().BeFalse();
        TextSearch.Matches("the cat sat", "cat", matchCase: false, wholeWord: true).Should().BeTrue();
    }

    [Fact]
    public void Replace_ReplacesAllOccurrences()
    {
        TextSearch.Replace("a-a-a", "a", "b", matchCase: false, wholeWord: false).Should().Be("b-b-b");
    }

    [Fact]
    public void Replace_TreatsReplacementLiterally()
    {
        TextSearch.Replace("price", "price", "$5", matchCase: false, wholeWord: false).Should().Be("$5");
    }

    [Fact]
    public void CountOccurrences_CountsMatches()
    {
        TextSearch.CountOccurrences("na na na", "na", matchCase: false, wholeWord: false).Should().Be(3);
    }

    [Fact]
    public void Find_ReturnsOnlyMatchingTextEntities()
    {
        var match = new TextEntity(Point2D.Origin, "Raum 101");
        var noMatch = new TextEntity(Point2D.Origin, "Flur");
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1));

        IReadOnlyList<ITextEntity> found = TextSearch.Find(
            new IEntity[] { match, noMatch, line }, "raum", matchCase: false, wholeWord: false);

        found.Should().ContainSingle().Which.Should().BeSameAs(match);
    }
}
