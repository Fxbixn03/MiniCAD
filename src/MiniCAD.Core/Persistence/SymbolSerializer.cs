using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Persistence;

/// <summary>
/// Reads and writes a single <see cref="BlockDefinition"/> as a reusable library symbol file
/// (the same JSON conventions as the project format, scoped to one block).
/// </summary>
public static class SymbolSerializer
{
    public const string FileExtension = ".mcadsym";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(BlockDefinition definition)
        => JsonSerializer.Serialize(DocumentMapper.ToDto(definition), Options);

    public static BlockDefinition Deserialize(string json)
    {
        BlockDefinitionDto dto = JsonSerializer.Deserialize<BlockDefinitionDto>(json, Options)
            ?? throw new InvalidDataException("The symbol file is empty or invalid.");
        return DocumentMapper.FromDto(dto);
    }

    public static void Save(BlockDefinition definition, string path)
        => File.WriteAllText(path, Serialize(definition));

    public static BlockDefinition Load(string path) => Deserialize(File.ReadAllText(path));
}
