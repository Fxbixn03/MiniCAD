using System.IO;
using System.Text.Json;

namespace MiniCAD.Core.Persistence;

/// <summary>
/// Reads and writes the project file format (JSON). Pure (de)serialization plus thin file
/// helpers; mapping to/from the domain model is the job of <see cref="DocumentMapper"/>.
/// </summary>
public static class ProjectSerializer
{
    public const string FileExtension = ".mcad";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string Serialize(ProjectFileDto project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonSerializer.Serialize(project, Options);
    }

    public static ProjectFileDto Deserialize(string json)
        => JsonSerializer.Deserialize<ProjectFileDto>(json, Options)
           ?? throw new InvalidDataException("The project file is empty or invalid.");

    public static void Save(ProjectFileDto project, string path)
        => File.WriteAllText(path, Serialize(project));

    public static ProjectFileDto Load(string path)
        => Deserialize(File.ReadAllText(path));
}
