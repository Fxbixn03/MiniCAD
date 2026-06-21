using MiniCAD.Core.Entities;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Derives the live 3D solid of an architectural wall (#73): the wall footprint is extruded from
/// its base elevation up by its height. The result is a plain <see cref="Mesh3D"/>/<see cref="Model3DObject"/>
/// so it renders through the existing 3D pipeline; it is marked <see cref="Model3DObject.IsDerived"/>
/// so the App can rebuild it whenever the 2D wall changes and keep it out of persistence.
/// </summary>
public static class WallModelBuilder
{
    private static readonly Color WallColor = new(205, 200, 190);

    public static Mesh3D BuildMesh(WallEntity wall)
        => Extruder.Extrude(wall.Footprint(), wall.BaseElevation, wall.Height);

    public static Model3DObject BuildModel(WallEntity wall)
        => new(BuildMesh(wall), "Wand") { Color = WallColor, IsDerived = true };
}
