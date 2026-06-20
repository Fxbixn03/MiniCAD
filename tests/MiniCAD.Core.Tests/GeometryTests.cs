using FluentAssertions;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tests;

public class GeometryTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void PointSubtraction_ProducesDisplacementVector()
    {
        var a = new Point2D(4, 6);
        var b = new Point2D(1, 2);

        Vector2D delta = a - b;

        delta.X.Should().Be(3);
        delta.Y.Should().Be(4);
        delta.Length.Should().BeApproximately(5, Tolerance);
    }

    [Fact]
    public void Lerp_ReturnsMidpoint_AtHalf()
    {
        var result = new Point2D(0, 0).Lerp(new Point2D(10, 20), 0.5);

        result.X.Should().BeApproximately(5, Tolerance);
        result.Y.Should().BeApproximately(10, Tolerance);
    }

    [Fact]
    public void VectorNormalized_HasUnitLength()
    {
        Vector2D normalized = new Vector2D(3, 4).Normalized();

        normalized.Length.Should().BeApproximately(1, Tolerance);
    }

    [Fact]
    public void VectorCrossAndDot_AreComputedCorrectly()
    {
        var a = new Vector2D(1, 0);
        var b = new Vector2D(0, 1);

        a.Dot(b).Should().Be(0);
        a.Cross(b).Should().Be(1);
    }

    [Fact]
    public void RotationMatrix_RotatesUnitXToUnitY_At90Degrees()
    {
        Matrix2D rotation = Matrix2D.Rotation(GeometryMath.DegreesToRadians(90));

        Point2D rotated = rotation.Transform(new Point2D(1, 0));

        rotated.X.Should().BeApproximately(0, 1e-12);
        rotated.Y.Should().BeApproximately(1, 1e-12);
    }

    [Fact]
    public void MatrixComposition_AppliesLeftOperandFirst()
    {
        Matrix2D scaleThenTranslate = Matrix2D.Scaling(2, 2) * Matrix2D.Translation(10, 0);

        Point2D result = scaleThenTranslate.Transform(new Point2D(1, 1));

        result.X.Should().BeApproximately(12, Tolerance);
        result.Y.Should().BeApproximately(2, Tolerance);
    }

    [Fact]
    public void Invert_RoundTripsThroughIdentity()
    {
        Matrix2D m = Matrix2D.Rotation(0.7) * Matrix2D.Scaling(2, 3) * Matrix2D.Translation(5, -4);
        var point = new Point2D(3, 7);

        Point2D roundTripped = m.Invert().Transform(m.Transform(point));

        roundTripped.X.Should().BeApproximately(point.X, 1e-9);
        roundTripped.Y.Should().BeApproximately(point.Y, 1e-9);
    }

    [Fact]
    public void TryInvert_ReturnsFalse_ForSingularMatrix()
    {
        Matrix2D singular = Matrix2D.Scaling(0, 1);

        singular.TryInvert(out _).Should().BeFalse();
    }

    [Fact]
    public void RectUnion_EnclosesBothInputs()
    {
        var a = new Rect2D(0, 0, 2, 2);
        var b = new Rect2D(1, 1, 5, 4);

        Rect2D union = a.Union(b);

        union.Should().Be(new Rect2D(0, 0, 5, 4));
        union.Contains(new Point2D(4, 3)).Should().BeTrue();
        union.Contains(new Point2D(6, 3)).Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 1, 0)]    // shallow angle locks to horizontal
    [InlineData(1, 10, 90)]   // steep angle locks to vertical
    [InlineData(10, 9, 45)]   // ~diagonal locks to 45°
    public void SnapToAngleStep_LocksToNearest45Degrees(double tx, double ty, double expectedDegrees)
    {
        double step = GeometryMath.DegreesToRadians(45);

        Point2D result = GeometryMath.SnapToAngleStep(Point2D.Origin, new Point2D(tx, ty), step);

        double resultDegrees = GeometryMath.RadiansToDegrees(Math.Atan2(result.Y, result.X));
        resultDegrees.Should().BeApproximately(expectedDegrees, 1e-9);
    }

    [Fact]
    public void SnapToAngleStep_PreservesDistanceFromOrigin()
    {
        double step = GeometryMath.DegreesToRadians(45);
        var target = new Point2D(10, 4);

        Point2D result = GeometryMath.SnapToAngleStep(Point2D.Origin, target, step);

        Point2D.Origin.DistanceTo(result).Should().BeApproximately(Point2D.Origin.DistanceTo(target), 1e-9);
        result.Y.Should().BeApproximately(0, 1e-9); // nearest step is horizontal
    }

    [Fact]
    public void DistancePointToSegment_ClampsToEndpoints()
    {
        var a = new Point2D(0, 0);
        var b = new Point2D(10, 0);

        double mid = GeometryMath.DistancePointToSegment(new Point2D(5, 3), a, b, out Point2D closest);
        mid.Should().BeApproximately(3, Tolerance);
        closest.X.Should().BeApproximately(5, Tolerance);

        double beyond = GeometryMath.DistancePointToSegment(new Point2D(-4, 0), a, b, out _);
        beyond.Should().BeApproximately(4, Tolerance);
    }

    [Fact]
    public void SegmentsIntersect_DetectsCrossingAndMiss()
    {
        GeometryMath.SegmentsIntersect(
            new Point2D(0, 0), new Point2D(10, 10),
            new Point2D(0, 10), new Point2D(10, 0)).Should().BeTrue();   // an X crossing

        GeometryMath.SegmentsIntersect(
            new Point2D(0, 0), new Point2D(1, 0),
            new Point2D(0, 5), new Point2D(1, 5)).Should().BeFalse();    // parallel, apart
    }

    [Fact]
    public void SegmentIntersectsRect_TrueWhenCrossingEdgeOrInside()
    {
        var rect = new Rect2D(0, 0, 10, 10);

        // Passes straight through the box.
        GeometryMath.SegmentIntersectsRect(new Point2D(-5, 5), new Point2D(15, 5), rect).Should().BeTrue();
        // Fully inside.
        GeometryMath.SegmentIntersectsRect(new Point2D(2, 2), new Point2D(8, 8), rect).Should().BeTrue();
        // Entirely outside, no crossing.
        GeometryMath.SegmentIntersectsRect(new Point2D(-5, -5), new Point2D(-5, 15), rect).Should().BeFalse();
    }
}
