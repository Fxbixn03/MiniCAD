using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Produces the transforms for an array (repeated copy) of a selection. Returns only the copy
/// transforms — the original (the identity at row/column 0 or angle 0) is excluded — so the
/// caller can clone each target once per transform.
/// </summary>
public static class ArrayGenerator
{
    /// <summary>A rectangular grid: <paramref name="rows"/> × <paramref name="columns"/> at the given spacing.</summary>
    public static IReadOnlyList<Matrix2D> Rectangular(int rows, int columns, double spacingX, double spacingY)
    {
        rows = Math.Max(1, rows);
        columns = Math.Max(1, columns);

        var transforms = new List<Matrix2D>(rows * columns - 1);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (r == 0 && c == 0)
                    continue;
                transforms.Add(Matrix2D.Translation(c * spacingX, r * spacingY));
            }
        }

        return transforms;
    }

    /// <summary>
    /// A polar array of <paramref name="count"/> items (including the original) spread over
    /// <paramref name="totalAngle"/> radians about <paramref name="center"/>; the step is
    /// <c>totalAngle / count</c>, so a full turn spaces them evenly.
    /// </summary>
    public static IReadOnlyList<Matrix2D> Polar(Point2D center, int count, double totalAngle)
    {
        count = Math.Max(1, count);
        double step = totalAngle / count;

        var transforms = new List<Matrix2D>(count - 1);
        for (int i = 1; i < count; i++)
            transforms.Add(Matrix2D.Rotation(i * step, center));

        return transforms;
    }
}
