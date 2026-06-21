namespace MiniCAD.Core.Viewing;

/// <summary>How the 3D view draws the model.</summary>
public enum Render3DMode
{
    /// <summary>All edges, no occlusion.</summary>
    Wireframe,

    /// <summary>Edges with hidden ones removed (faces occlude lines).</summary>
    HiddenLine,

    /// <summary>Solid flat-shaded faces with depth sorting and a light.</summary>
    Shaded,
}
