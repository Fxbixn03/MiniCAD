using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// An object in the 3D model space: a <see cref="Mesh3D"/> in local coordinates plus a world
/// <see cref="Transform"/> and a display color. This is the 3D counterpart to a 2D entity, kept
/// in a parallel model so the 2D path is unaffected.
/// </summary>
public sealed class Model3DObject
{
    public Model3DObject(Mesh3D mesh, string name = "Körper")
        : this(Guid.NewGuid(), name, mesh, Matrix4.Identity, Color.White)
    {
    }

    public Model3DObject(Guid id, string name, Mesh3D mesh, Matrix4 transform, Color color)
    {
        Id = id;
        Name = name;
        Mesh = mesh;
        Transform = transform;
        Color = color;
    }

    public Guid Id { get; }

    public string Name { get; set; }

    /// <summary>The geometry in the object's local coordinates.</summary>
    public Mesh3D Mesh { get; set; }

    /// <summary>The world placement of the object.</summary>
    public Matrix4 Transform { get; set; }

    public Color Color { get; set; }

    /// <summary>The mesh placed into world coordinates.</summary>
    public Mesh3D WorldMesh() => Mesh.Transformed(Transform);

    public BoundingBox3D Bounds => WorldMesh().Bounds;

    public override string ToString() => Name;
}
