using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Configuration;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using CoreColor = MiniCAD.Core.Styling.Color;
using CoreStroke = MiniCAD.Core.Styling.StrokeStyle;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The "Assistent" panel: a small window-in-window library of predefined elements. A
/// double-click on a template re-activates its tool with the stored line style, so the user
/// reuses it instantly. The current selection can be captured as a new, persisted template.
/// </summary>
public partial class AssistantViewModel : ViewModelBase
{
    public AssistantViewModel()
    {
        foreach (AssistantItemViewModel item in BuiltIns())
            Templates.Add(item);
        foreach (AssistantItemViewModel item in LoadUserTemplates())
            Templates.Add(item);
    }

    public ObservableCollection<AssistantItemViewModel> Templates { get; } = new();

    [ObservableProperty]
    private AssistantItemViewModel? _selectedTemplate;

    /// <summary>Raised when a template should be used (host activates the matching tool).</summary>
    public event Action<AssistantItemViewModel>? UseRequested;

    [RelayCommand]
    private void Use(AssistantItemViewModel? item)
    {
        item ??= SelectedTemplate;
        if (item is not null)
            UseRequested?.Invoke(item);
    }

    public void UseSelected() => Use(SelectedTemplate);

    /// <summary>Captures an entity (incl. architectural elements) as a new, persisted template.</summary>
    public void AddFromEntity(IEntity entity, CoreStroke resolvedStroke, string? layerName)
    {
        CoreStroke stroke = entity.StrokeOverride ?? resolvedStroke;

        AssistantItemViewModel? item = entity switch
        {
            WallEntity w => new AssistantItemViewModel($"Wand {(int)w.Thickness}", AssistantToolKind.Wall, stroke, false,
                layerName, thickness: w.Thickness, height: w.Height, baseElevation: w.BaseElevation),
            OpeningEntity o => new AssistantItemViewModel($"Aussparung H{(int)o.Height}", AssistantToolKind.Opening, stroke, false,
                layerName, width: o.Width, height: o.Height, baseElevation: o.BaseElevation),
            ColumnEntity c => new AssistantItemViewModel(
                c.Round ? $"Rundstütze ⌀{(int)c.Width}" : $"Stütze {(int)c.Width}×{(int)c.Depth}",
                AssistantToolKind.Column, stroke, false, layerName,
                round: c.Round, width: c.Width, depth: c.Depth, height: c.Height, baseElevation: c.BaseElevation),
            SlabEntity s => new AssistantItemViewModel($"Decke {(int)s.Thickness}", AssistantToolKind.Slab, stroke, false,
                layerName, thickness: s.Thickness, baseElevation: s.BaseElevation),
            BeamEntity b => new AssistantItemViewModel($"Unterzug {(int)b.Width}×{(int)b.Height}", AssistantToolKind.Beam, stroke, false,
                layerName, width: b.Width, height: b.Height, baseElevation: b.BaseElevation),
            LineEntity => Make(AssistantToolKind.Line, stroke, layerName),
            CircleEntity => Make(AssistantToolKind.Circle, stroke, layerName),
            PolylineEntity { IsClosed: true } => Make(AssistantToolKind.Rectangle, stroke, layerName),
            PolylineEntity => Make(AssistantToolKind.Polyline, stroke, layerName),
            _ => null,
        };
        if (item is null)
            return;

        Templates.Add(item);
        SelectedTemplate = item;
        SaveUserTemplates();
    }

    private static AssistantItemViewModel Make(AssistantToolKind kind, CoreStroke stroke, string? layerName)
        => new($"{ToolLabel(kind)} {(int)stroke.Width}px", kind, stroke, isBuiltIn: false, layerName: layerName);

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedTemplate is { IsBuiltIn: false } item)
        {
            Templates.Remove(item);
            SaveUserTemplates();
        }
    }

    private static IEnumerable<AssistantItemViewModel> BuiltIns()
    {
        CoreColor white = CoreColor.White;
        yield return new AssistantItemViewModel("Linie dünn", AssistantToolKind.Line, new CoreStroke(white, 1.0), true);
        yield return new AssistantItemViewModel("Linie mittel", AssistantToolKind.Line, new CoreStroke(white, 2.5), true);
        yield return new AssistantItemViewModel("Linie kräftig", AssistantToolKind.Line, new CoreStroke(white, 4.0), true);
        yield return new AssistantItemViewModel("Linie rot", AssistantToolKind.Line, new CoreStroke(CoreColor.Red, 1.5), true);
        yield return new AssistantItemViewModel("Hilfslinie", AssistantToolKind.Line, new CoreStroke(new CoreColor(120, 120, 140), 0.5), true);
        yield return new AssistantItemViewModel("Rechteck", AssistantToolKind.Rectangle, new CoreStroke(white, 1.5), true);
        yield return new AssistantItemViewModel("Kreis", AssistantToolKind.Circle, new CoreStroke(white, 1.5), true);

        // Architectural elements (Allplan-style assistants): full geometry + layer + line weight.
        var wallColor = new CoreColor(210, 205, 195);
        yield return new AssistantItemViewModel("Außenwand 36,5", AssistantToolKind.Wall, new CoreStroke(wallColor, 2.0), true,
            layerName: "Wände tragend", thickness: 365, height: 2750);
        yield return new AssistantItemViewModel("Tragwand 24", AssistantToolKind.Wall, new CoreStroke(wallColor, 2.0), true,
            layerName: "Wände tragend", thickness: 240, height: 2750);
        yield return new AssistantItemViewModel("Innenwand 11,5", AssistantToolKind.Wall, new CoreStroke(wallColor, 1.0), true,
            layerName: "Wände nichttragend", thickness: 115, height: 2750);
        yield return new AssistantItemViewModel("Tür 2,01", AssistantToolKind.Opening, new CoreStroke(white, 1.0), true,
            layerName: "Türen", width: 400, height: 2010, baseElevation: 0);
        yield return new AssistantItemViewModel("Fenster 1,26", AssistantToolKind.Opening, new CoreStroke(white, 1.0), true,
            layerName: "Fenster", width: 400, height: 1260, baseElevation: 900);
        yield return new AssistantItemViewModel("Stütze 30×30", AssistantToolKind.Column, new CoreStroke(wallColor, 1.5), true,
            layerName: "Stützen", width: 300, depth: 300, height: 2750);
        yield return new AssistantItemViewModel("Rundstütze ⌀30", AssistantToolKind.Column, new CoreStroke(wallColor, 1.5), true,
            layerName: "Stützen", round: true, width: 300, height: 2750);
        yield return new AssistantItemViewModel("Decke 20", AssistantToolKind.Slab, new CoreStroke(wallColor, 1.5), true,
            layerName: "Decken", thickness: 200, baseElevation: 0);
        yield return new AssistantItemViewModel("Unterzug 24/40", AssistantToolKind.Beam, new CoreStroke(wallColor, 1.5), true,
            layerName: "Unterzüge", width: 240, height: 400, baseElevation: 2750);
    }

    private static string ToolLabel(AssistantToolKind kind) => kind switch
    {
        AssistantToolKind.Line => "Linie",
        AssistantToolKind.Rectangle => "Rechteck",
        AssistantToolKind.Circle => "Kreis",
        AssistantToolKind.Polyline => "Polylinie",
        _ => kind.ToString(),
    };

    // ----- Persistence -----

    private sealed record TemplateDto(string Name, AssistantToolKind Tool, byte R, byte G, byte B, byte A, double Width,
        string? LayerName = null, bool Round = false, double Thickness = 0, double Height = 0,
        double BaseElevation = 0, double ArchWidth = 0, double Depth = 0);

    private static IEnumerable<AssistantItemViewModel> LoadUserTemplates()
    {
        string raw = AppConfig.Instance.AssistantTemplates;
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        TemplateDto[]? dtos = null;
        try
        {
            dtos = JsonSerializer.Deserialize<TemplateDto[]>(raw);
        }
        catch (JsonException)
        {
            // Ignore corrupt config.
        }

        if (dtos is null)
            yield break;

        foreach (TemplateDto dto in dtos)
            yield return new AssistantItemViewModel(dto.Name, dto.Tool,
                new CoreStroke(new CoreColor(dto.R, dto.G, dto.B, dto.A), dto.Width), isBuiltIn: false,
                dto.LayerName, dto.Round, dto.Thickness, dto.Height, dto.BaseElevation, dto.ArchWidth, dto.Depth);
    }

    private void SaveUserTemplates()
    {
        TemplateDto[] dtos = Templates
            .Where(t => !t.IsBuiltIn && t.Stroke is not null)
            .Select(t =>
            {
                CoreStroke s = t.Stroke!.Value;
                return new TemplateDto(t.Name, t.ToolKind, s.Color.R, s.Color.G, s.Color.B, s.Color.A, s.Width,
                    t.LayerName, t.Round, t.Thickness, t.Height, t.BaseElevation, t.Width, t.Depth);
            })
            .ToArray();

        AppConfig.Instance.AssistantTemplates = JsonSerializer.Serialize(dtos);
    }
}
