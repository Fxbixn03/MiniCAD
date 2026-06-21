using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Persistence;

namespace MiniCAD.App;

/// <summary>
/// The cross-project symbol library: block definitions stored as files under the user's
/// application-data folder, listed and inserted via the library palette.
/// </summary>
public static class SymbolLibrary
{
    /// <summary>The folder holding the library's symbol files.</summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MiniCAD", "Symbols");

    public static IReadOnlyList<string> ListFiles()
    {
        if (!System.IO.Directory.Exists(Directory))
            return Array.Empty<string>();

        return System.IO.Directory.GetFiles(Directory, "*" + SymbolSerializer.FileExtension)
            .OrderBy(Path.GetFileNameWithoutExtension)
            .ToList();
    }

    /// <summary>Saves a block definition into the library, returning the file path.</summary>
    public static string Save(BlockDefinition definition)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string path = Path.Combine(Directory, Sanitize(definition.Name) + SymbolSerializer.FileExtension);
        SymbolSerializer.Save(definition, path);
        return path;
    }

    public static BlockDefinition Load(string path) => SymbolSerializer.Load(path);

    private static string Sanitize(string name)
    {
        string trimmed = string.IsNullOrWhiteSpace(name) ? "Symbol" : name.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(invalid, '_');
        return trimmed;
    }
}
