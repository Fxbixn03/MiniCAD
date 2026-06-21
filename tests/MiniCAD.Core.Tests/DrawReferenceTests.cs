using FluentAssertions;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Tools;

namespace MiniCAD.Core.Tests;

public class DrawReferenceTests
{
    private static readonly Point2D Start = new(0, 0);
    private static readonly Point2D End = new(10, 0); // drawing along +X; left of travel is +Y

    [Fact]
    public void Center_HasNoOffset()
    {
        DrawReferenceMath.CenterlineOffset(Start, End, 240, DrawReference.Center)
            .Should().Be(Vector2D.Zero);
    }

    [Fact]
    public void LeftReference_PutsBodyToTheRightOfTheDrawnLine()
    {
        // Drawing along the left face → centerline shifts to the right of travel (−Y here) by t/2.
        Vector2D off = DrawReferenceMath.CenterlineOffset(Start, End, 240, DrawReference.Left);
        off.X.Should().BeApproximately(0, 1e-9);
        off.Y.Should().BeApproximately(-120, 1e-9);
    }

    [Fact]
    public void RightReference_PutsBodyToTheLeftOfTheDrawnLine()
    {
        Vector2D off = DrawReferenceMath.CenterlineOffset(Start, End, 240, DrawReference.Right);
        off.X.Should().BeApproximately(0, 1e-9);
        off.Y.Should().BeApproximately(120, 1e-9);
    }

    [Fact]
    public void Offset_IsHalfThickness_PerpendicularToDirection()
    {
        Vector2D off = DrawReferenceMath.CenterlineOffset(new Point2D(0, 0), new Point2D(0, 5), 300, DrawReference.Left);
        // direction +Y → left is −X; Left reference offsets to the right (+X) by 150.
        off.X.Should().BeApproximately(150, 1e-9);
        off.Y.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void DegenerateSegment_HasNoOffset()
    {
        DrawReferenceMath.CenterlineOffset(Start, Start, 240, DrawReference.Left)
            .Should().Be(Vector2D.Zero);
    }
}
