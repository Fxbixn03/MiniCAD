using System.Linq;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Builds the live 3D solids of the architectural model (#72): each wall is extruded and any
/// recess/opening (Aussparung, #143) that overlaps it in plan and elevation is subtracted via CSG
/// (#126). The results are derived <see cref="Model3DObject"/>s, rebuilt whenever the 2D model
/// changes. Keeping the rule in Core makes it unit-testable independently of the App.
/// </summary>
public static class ArchModelBuilder
{
    public static List<Model3DObject> Build(IEnumerable<WallEntity> walls, IEnumerable<OpeningEntity> openings)
    {
        var openingList = openings.ToList();
        var models = new List<Model3DObject>();

        foreach (WallEntity wall in walls)
        {
            Mesh3D mesh = WallModelBuilder.BuildMesh(wall);

            foreach (OpeningEntity opening in openingList)
            {
                if (!Overlaps(wall, opening))
                    continue;
                // Extend the cutter slightly in Z so its faces never sit coplanar with the wall's
                // top/bottom (coincident faces are unreliable for BSP-CSG); the overhang is outside
                // the wall and removes nothing extra.
                const double overhang = 1.0;
                Mesh3D cutter = Extruder.Extrude(opening.Footprint(),
                    opening.BaseElevation - overhang, opening.Height + 2 * overhang);
                mesh = Csg.Subtract(mesh, cutter);
            }

            models.Add(new Model3DObject(mesh, "Wand") { Color = WallModelBuilder.WallColor, IsDerived = true });
        }

        return models;
    }

    /// <summary>True if the opening intersects the wall in both plan footprint and Z range.</summary>
    private static bool Overlaps(WallEntity wall, OpeningEntity opening)
    {
        if (!wall.Bounds.Intersects(opening.Bounds))
            return false;
        return opening.BaseElevation < wall.TopElevation && opening.TopElevation > wall.BaseElevation;
    }
}
