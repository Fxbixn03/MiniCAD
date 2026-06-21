using System.Globalization;
using System.Linq;
using System.Text;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Interop;

/// <summary>
/// Reads and writes triangle meshes in the common ASCII formats STL (3D printing) and OBJ
/// (universal) — the tractable half of #216. Geometry round-trips without loss; STL has no shared
/// vertices, OBJ shares them. (glTF/COLLADA/STEP/IFC remain bigger, separate efforts.)
/// </summary>
public static class MeshExchange
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Concatenates meshes into one (offsetting indices) — used to export the whole model.</summary>
    public static Mesh3D Combine(IEnumerable<Mesh3D> meshes)
    {
        var verts = new List<Point3D>();
        var tris = new List<int>();
        foreach (Mesh3D m in meshes)
        {
            int offset = verts.Count;
            verts.AddRange(m.Vertices);
            tris.AddRange(m.Indices.Select(i => i + offset));
        }
        return new Mesh3D(verts, tris);
    }

    // ----- STL (ASCII) -----

    public static string WriteStl(Mesh3D mesh, string name = "minicad")
    {
        var sb = new StringBuilder();
        sb.Append("solid ").AppendLine(name);
        IReadOnlyList<Point3D> v = mesh.Vertices;
        IReadOnlyList<int> idx = mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            Point3D a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
            Vector3D n = (b - a).Cross(c - a);
            if (n.Length > 1e-12)
                n = n.Normalized();
            sb.AppendLine($"  facet normal {F(n.X)} {F(n.Y)} {F(n.Z)}");
            sb.AppendLine("    outer loop");
            sb.AppendLine($"      vertex {F(a.X)} {F(a.Y)} {F(a.Z)}");
            sb.AppendLine($"      vertex {F(b.X)} {F(b.Y)} {F(b.Z)}");
            sb.AppendLine($"      vertex {F(c.X)} {F(c.Y)} {F(c.Z)}");
            sb.AppendLine("    endloop");
            sb.AppendLine("  endfacet");
        }
        sb.Append("endsolid ").AppendLine(name);
        return sb.ToString();
    }

    public static Mesh3D ReadStl(string text)
    {
        var verts = new List<Point3D>();
        var tris = new List<int>();
        foreach (string line in text.Split('\n'))
        {
            string t = line.Trim();
            if (!t.StartsWith("vertex", System.StringComparison.OrdinalIgnoreCase))
                continue;
            string[] p = t.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 4 && TryD(p[1], out double x) && TryD(p[2], out double y) && TryD(p[3], out double z))
            {
                tris.Add(verts.Count);
                verts.Add(new Point3D(x, y, z));
            }
        }
        return new Mesh3D(verts, tris);
    }

    // ----- OBJ -----

    public static string WriteObj(IReadOnlyList<(Mesh3D Mesh, string Name)> objects)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MiniCAD OBJ export");
        int baseIndex = 1; // OBJ indices are 1-based and global across the file
        foreach ((Mesh3D mesh, string name) in objects)
        {
            sb.Append("o ").AppendLine(name);
            foreach (Point3D v in mesh.Vertices)
                sb.AppendLine($"v {F(v.X)} {F(v.Y)} {F(v.Z)}");
            IReadOnlyList<int> idx = mesh.Indices;
            for (int i = 0; i + 2 < idx.Count; i += 3)
                sb.AppendLine($"f {baseIndex + idx[i]} {baseIndex + idx[i + 1]} {baseIndex + idx[i + 2]}");
            baseIndex += mesh.Vertices.Count;
        }
        return sb.ToString();
    }

    public static Mesh3D ReadObj(string text)
    {
        var verts = new List<Point3D>();
        var tris = new List<int>();
        foreach (string line in text.Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("v ", System.StringComparison.Ordinal))
            {
                string[] p = t.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4 && TryD(p[1], out double x) && TryD(p[2], out double y) && TryD(p[3], out double z))
                    verts.Add(new Point3D(x, y, z));
            }
            else if (t.StartsWith("f ", System.StringComparison.Ordinal))
            {
                string[] p = t.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries);
                var face = new List<int>();
                for (int i = 1; i < p.Length; i++)
                {
                    // "v", "v/vt", "v/vt/vn", "v//vn" → take the (1-based) vertex index.
                    string token = p[i].Split('/')[0];
                    if (int.TryParse(token, NumberStyles.Integer, Inv, out int vi))
                        face.Add(vi < 0 ? verts.Count + vi : vi - 1); // negative = relative
                }
                for (int i = 2; i < face.Count; i++) // fan-triangulate a polygon face
                {
                    tris.Add(face[0]);
                    tris.Add(face[i - 1]);
                    tris.Add(face[i]);
                }
            }
        }
        return new Mesh3D(verts, tris);
    }

    private static string F(double value) => value.ToString("0.######", Inv);

    private static bool TryD(string s, out double value) => double.TryParse(s, NumberStyles.Float, Inv, out value);
}
