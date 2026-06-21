using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Extracts a closed 2D contour (profile) from an entity, for extrusion/revolve/sweep.</summary>
public static class ProfileExtractor
{
    private const int CircleSegments = 48;

    /// <summary>The closed profile of a closed polyline, circle or full ellipse; null otherwise.</summary>
    public static IReadOnlyList<Point2D>? FromEntity(IEntity entity)
    {
        switch (entity)
        {
            case PolylineEntity { IsClosed: true } poly when poly.Points.Count >= 3:
                return poly.Points;

            case CircleEntity circle:
            {
                var points = new List<Point2D>(CircleSegments);
                for (int i = 0; i < CircleSegments; i++)
                {
                    double a = 2 * Math.PI * i / CircleSegments;
                    points.Add(new Point2D(
                        circle.Center.X + circle.Radius * Math.Cos(a),
                        circle.Center.Y + circle.Radius * Math.Sin(a)));
                }
                return points;
            }

            case EllipseEntity { IsFull: true } ellipse:
            {
                var points = new List<Point2D>(CircleSegments);
                for (int i = 0; i < CircleSegments; i++)
                    points.Add(ellipse.PointAt(2 * Math.PI * i / CircleSegments));
                return points;
            }

            default:
                return null;
        }
    }
}
