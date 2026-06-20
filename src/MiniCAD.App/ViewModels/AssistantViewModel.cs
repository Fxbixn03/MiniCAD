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

    /// <summary>Captures an entity's tool kind and stroke as a new, persisted template.</summary>
    public void AddFromEntity(IEntity entity, CoreStroke resolvedStroke)
    {
        AssistantToolKind? kind = entity switch
        {
            LineEntity => AssistantToolKind.Line,
            CircleEntity => AssistantToolKind.Circle,
            PolylineEntity { IsClosed: true } => AssistantToolKind.Rectangle,
            PolylineEntity => AssistantToolKind.Polyline,
            _ => null,
        };
        if (kind is not { } toolKind)
            return;

        CoreStroke stroke = entity.StrokeOverride ?? resolvedStroke;
        string name = $"{ToolLabel(toolKind)} {(int)stroke.Width}px";
        var item = new AssistantItemViewModel(name, toolKind, stroke, isBuiltIn: false);
        Templates.Add(item);
        SelectedTemplate = item;
        SaveUserTemplates();
    }

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

    private sealed record TemplateDto(string Name, AssistantToolKind Tool, byte R, byte G, byte B, byte A, double Width);

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
            yield return new AssistantItemViewModel(dto.Name, dto.Tool, new CoreStroke(new CoreColor(dto.R, dto.G, dto.B, dto.A), dto.Width), isBuiltIn: false);
    }

    private void SaveUserTemplates()
    {
        TemplateDto[] dtos = Templates
            .Where(t => !t.IsBuiltIn && t.Stroke is not null)
            .Select(t =>
            {
                CoreStroke s = t.Stroke!.Value;
                return new TemplateDto(t.Name, t.ToolKind, s.Color.R, s.Color.G, s.Color.B, s.Color.A, s.Width);
            })
            .ToArray();

        AppConfig.Instance.AssistantTemplates = JsonSerializer.Serialize(dtos);
    }
}
