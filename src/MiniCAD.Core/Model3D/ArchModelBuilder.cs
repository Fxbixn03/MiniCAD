using System.Linq;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Materials;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Builds the live 3D solids of the architectural model (#72) from the 2D building elements: walls
/// (with recesses/openings subtracted via CSG, #126), columns (#75), slabs and beams (#144). The
/// results are derived <see cref="Model3DObject"/>s, rebuilt whenever the 2D model changes. Keeping
/// the rule in Core makes it unit-testable independently of the App.
/// </summary>
public static class ArchModelBuilder
{
    private static readonly MaterialLibrary Library = MaterialLibrary.Default;
    private static readonly MaterialDefinition WallMaterial =
        Library.TryGet("Kalksandstein", out MaterialDefinition w) ? w : MaterialLibrary.Concrete;
    private static readonly MaterialDefinition ConcreteMaterial = MaterialLibrary.Concrete;

    public static List<Model3DObject> Build(IEnumerable<IEntity> entities)
    {
        var list = entities.ToList();
        var openings = list.OfType<OpeningEntity>().ToList();
        var models = new List<Model3DObject>();

        foreach (WallEntity wall in list.OfType<WallEntity>())
        {
            Mesh3D mesh = WallModelBuilder.BuildMesh(wall);
            foreach (OpeningEntity opening in openings)
            {
                if (!Overlaps(wall.Bounds, wall.BaseElevation, wall.TopElevation, opening))
                    continue;
                mesh = Csg.Subtract(mesh, OpeningCutter(opening));
            }
            models.Add(Derived(mesh, WallMaterial));
        }

        foreach (ColumnEntity column in list.OfType<ColumnEntity>())
            models.Add(Derived(ColumnMesh(column), ConcreteMaterial));

        foreach (SlabEntity slab in list.OfType<SlabEntity>())
            if (SlabMesh(slab) is { } mesh)
                models.Add(Derived(mesh, ConcreteMaterial));

        foreach (BeamEntity beam in list.OfType<BeamEntity>())
            models.Add(Derived(BeamMesh(beam), ConcreteMaterial));

        return models;
    }

    private static Model3DObject Derived(Mesh3D mesh, MaterialDefinition material)
        => new(mesh, material.Name) { Color = material.DiffuseColor, Material = material, IsDerived = true };

    // ----- Per-element meshes -----

    private static Mesh3D ColumnMesh(ColumnEntity c)
    {
        var center = new Point3D(c.Position.X, c.Position.Y, c.BaseElevation + c.Height / 2);
        return c.Round
            ? Mesh3D.Cylinder(c.Width / 2, c.Height, 24, center)
            : Mesh3D.Box(c.Width, c.Depth, c.Height, center);
    }

    private static Mesh3D? SlabMesh(SlabEntity slab)
    {
        if (slab.Outline.Count < 3)
            return null;
        return Extruder.Extrude(slab.Outline, slab.BaseElevation, slab.Thickness);
    }

    private static Mesh3D BeamMesh(BeamEntity beam)
        => Extruder.Extrude(beam.Footprint(), beam.BaseElevation, beam.Height);

    private static Mesh3D OpeningCutter(OpeningEntity opening)
    {
        // Extend slightly in Z so the cutter faces never sit coplanar with the wall's top/bottom
        // (coincident faces are unreliable for BSP-CSG); the overhang is outside the wall.
        const double overhang = 1.0;
        return Extruder.Extrude(opening.Footprint(), opening.BaseElevation - overhang, opening.Height + 2 * overhang);
    }

    private static bool Overlaps(Rect2D bounds, double baseZ, double topZ, OpeningEntity opening)
    {
        if (!bounds.Intersects(opening.Bounds))
            return false;
        return opening.BaseElevation < topZ && opening.TopElevation > baseZ;
    }
}
