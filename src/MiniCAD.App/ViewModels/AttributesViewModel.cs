using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Commands;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;
using CoreColor = MiniCAD.Core.Styling.Color;
using CoreStroke = MiniCAD.Core.Styling.StrokeStyle;
using CoreFill = MiniCAD.Core.Styling.FillStyle;
using LineType = MiniCAD.Core.Styling.LineType;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The docked "Eigenschaften" (attributes) panel. It reflects the current selection and lets
/// the user retarget its layer/Teilbild, give it an explicit stroke override and — for a closed
/// object — fill it with a hatch pattern. Every edit is applied through an
/// <see cref="IUndoableCommand"/> so it lands on the shared undo history.
/// </summary>
public partial class AttributesViewModel : ViewModelBase
{
    private readonly CadDocument _document;
    private readonly Selection _selection;
    private readonly UndoRedoManager _commands;
    private bool _suppress;

    public AttributesViewModel(CadDocument document, Selection selection, UndoRedoManager commands)
    {
        _document = document;
        _selection = selection;
        _commands = commands;

        _selection.Changed += (_, _) => Reload();
        _document.Changed += OnDocumentChanged;
        Reload();
    }

    public ObservableCollection<Layer> LayerOptions { get; } = new();

    public ObservableCollection<PartialDrawing> PartialDrawingOptions { get; } = new();

    public ObservableCollection<PatternOptionViewModel> FillOptions { get; } = new();

    public ObservableCollection<TextStyle> TextStyleOptions { get; } = new();

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _summary = "Keine Auswahl";

    [ObservableProperty]
    private Layer? _selectedLayer;

    [ObservableProperty]
    private PartialDrawing? _selectedPartialDrawing;

    [ObservableProperty]
    private bool _useStrokeOverride;

    [ObservableProperty]
    private double _strokeRed;

    [ObservableProperty]
    private double _strokeGreen;

    [ObservableProperty]
    private double _strokeBlue;

    [ObservableProperty]
    private double _strokeWidth = 1.0;

    [ObservableProperty]
    private LineType _strokeLineType;

    public LineType[] StrokeLineTypeOptions { get; } = Enum.GetValues<LineType>();

    [ObservableProperty]
    private double _strokeLineWeight;

    public double[] StrokeLineWeightOptions { get; } = StyleOptions.LineWeights;

    [ObservableProperty]
    private bool _canFill;

    [ObservableProperty]
    private PatternOptionViewModel? _selectedFill;

    [ObservableProperty]
    private bool _canSetTextStyle;

    [ObservableProperty]
    private TextStyle? _selectedTextStyle;

    // ----- Block instance attributes -----

    public ObservableCollection<BlockAttributeRowViewModel> BlockAttributes { get; } = new();

    [ObservableProperty]
    private bool _isBlockReference;

    [ObservableProperty]
    private string _newAttributeKey = string.Empty;

    // ----- Parametric symbol parameters -----

    public ObservableCollection<ParametricParamRowViewModel> SymbolParameters { get; } = new();

    [ObservableProperty]
    private bool _isParametricSymbol;

    // ----- Area fill (solid/gradient) for a closed polyline -----

    [ObservableProperty]
    private bool _useAreaFill;

    [ObservableProperty]
    private double _areaRed;

    [ObservableProperty]
    private double _areaGreen;

    [ObservableProperty]
    private double _areaBlue;

    [ObservableProperty]
    private double _areaAlpha = 255;

    [ObservableProperty]
    private bool _areaGradient;

    [ObservableProperty]
    private double _area2Red;

    [ObservableProperty]
    private double _area2Green;

    [ObservableProperty]
    private double _area2Blue;

    [ObservableProperty]
    private double _areaAngle;

    public IBrush StrokeBrush => new SolidColorBrush(Color.FromRgb((byte)StrokeRed, (byte)StrokeGreen, (byte)StrokeBlue));

    public IBrush AreaBrush => new SolidColorBrush(Color.FromArgb((byte)AreaAlpha, (byte)AreaRed, (byte)AreaGreen, (byte)AreaBlue));

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (_suppress)
            return;

        // Structural changes can add/remove layers, Teilbilder or patterns shown in the combos.
        if (e.Kind is DocumentChangeKind.LayerAdded or DocumentChangeKind.LayerRemoved
            or DocumentChangeKind.PartialDrawingAdded or DocumentChangeKind.PartialDrawingRemoved
            or DocumentChangeKind.PatternsChanged or DocumentChangeKind.TextStylesChanged
            or DocumentChangeKind.Reloaded)
        {
            Reload();
        }
    }

    /// <summary>Rebuilds the panel from the current selection.</summary>
    private void Reload()
    {
        _suppress = true;

        RebuildOptions();

        IReadOnlyList<IEntity> items = _selection.Items;
        HasSelection = items.Count > 0;

        if (items.Count == 0)
        {
            Summary = "Keine Auswahl";
            SelectedLayer = null;
            SelectedPartialDrawing = null;
            UseStrokeOverride = false;
            CanFill = false;
            SelectedFill = null;
            CanSetTextStyle = false;
            SelectedTextStyle = null;
            IsBlockReference = false;
            BlockAttributes.Clear();
            IsParametricSymbol = false;
            SymbolParameters.Clear();
            _suppress = false;
            return;
        }

        Summary = items.Count == 1 ? TypeName(items[0]) : $"{items.Count} Objekte";

        // Common layer / Teilbild across the selection, else null (mixed).
        Guid? layerId = Common(items, e => e.LayerId);
        SelectedLayer = layerId is { } lid ? _document.FindLayer(lid) : null;

        Guid? partialId = Common(items, e => e.PartialDrawingId);
        SelectedPartialDrawing = partialId is { } pid ? _document.FindPartialDrawing(pid) : null;

        // Stroke override: shown when every selected entity carries the same one.
        CoreStroke? overrideStroke = items[0].StrokeOverride;
        bool sameOverride = items.All(e => Nullable.Equals(e.StrokeOverride, overrideStroke));
        if (sameOverride && overrideStroke is { } stroke)
        {
            UseStrokeOverride = true;
            StrokeRed = stroke.Color.R;
            StrokeGreen = stroke.Color.G;
            StrokeBlue = stroke.Color.B;
            StrokeWidth = stroke.Width;
            StrokeLineType = stroke.LineType;
            StrokeLineWeight = stroke.LineWeightMm;
        }
        else
        {
            UseStrokeOverride = false;
        }

        // Fill is only meaningful for a single closed polyline ("eine Wand").
        CanFill = items.Count == 1 && items[0] is PolylineEntity { IsClosed: true };
        SelectedFill = CanFill
            ? FillOptions.FirstOrDefault(o => o.Pattern?.Id == (items[0] as PolylineEntity)?.Fill?.Id)
              ?? FillOptions[0]
            : null;

        // Text style assignment is offered for a single text/MText entity.
        CanSetTextStyle = items.Count == 1 && items[0] is ITextEntity;
        SelectedTextStyle = items[0] is ITextEntity text
            ? TextStyleOptions.FirstOrDefault(s => s.Id == text.TextStyleId)
            : null;

        IsBlockReference = items.Count == 1 && items[0] is BlockReferenceEntity;
        BuildBlockAttributes();

        IsParametricSymbol = items.Count == 1 && items[0] is ParametricSymbolEntity;
        BuildSymbolParameters();

        // Area fill: a single closed polyline (same condition as the hatch fill).
        if (CanFill && items[0] is PolylineEntity { SolidFill: { } areaFill })
        {
            UseAreaFill = true;
            AreaRed = areaFill.Color.R;
            AreaGreen = areaFill.Color.G;
            AreaBlue = areaFill.Color.B;
            AreaAlpha = areaFill.Color.A;
            AreaGradient = areaFill.IsGradient;
            Area2Red = areaFill.SecondColor.R;
            Area2Green = areaFill.SecondColor.G;
            Area2Blue = areaFill.SecondColor.B;
            AreaAngle = areaFill.AngleDegrees;
        }
        else
        {
            UseAreaFill = false;
        }

        _suppress = false;
    }

    private void RebuildOptions()
    {
        LayerOptions.Clear();
        foreach (Layer layer in _document.Layers)
            LayerOptions.Add(layer);

        PartialDrawingOptions.Clear();
        foreach (PartialDrawing partialDrawing in _document.PartialDrawings)
            PartialDrawingOptions.Add(partialDrawing);

        FillOptions.Clear();
        FillOptions.Add(new PatternOptionViewModel(null, "Keine Füllung"));
        foreach (var pattern in Core.Styling.PatternLibrary.All)
            FillOptions.Add(new PatternOptionViewModel(pattern, pattern.Name));
        foreach (var pattern in _document.Patterns)
            FillOptions.Add(new PatternOptionViewModel(pattern, pattern.Name + " (Projekt)"));

        TextStyleOptions.Clear();
        foreach (TextStyle style in _document.TextStyles)
            TextStyleOptions.Add(style);
    }

    partial void OnSelectedLayerChanged(Layer? value)
    {
        if (_suppress || value is null)
            return;

        ApplyToSelection("Layer ändern", entity =>
        {
            Guid old = entity.LayerId;
            return (() => entity.LayerId = value.Id, () => entity.LayerId = old);
        });
    }

    partial void OnSelectedPartialDrawingChanged(PartialDrawing? value)
    {
        if (_suppress || value is null)
            return;

        ApplyToSelection("Teilbild ändern", entity =>
        {
            Guid old = entity.PartialDrawingId;
            return (() => entity.PartialDrawingId = value.Id, () => entity.PartialDrawingId = old);
        });
    }

    partial void OnUseStrokeOverrideChanged(bool value) => ApplyStrokeOverride();

    partial void OnStrokeRedChanged(double value) => ApplyStrokeOverride();

    partial void OnStrokeGreenChanged(double value) => ApplyStrokeOverride();

    partial void OnStrokeBlueChanged(double value) => ApplyStrokeOverride();

    partial void OnStrokeWidthChanged(double value) => ApplyStrokeOverride();

    partial void OnStrokeLineTypeChanged(LineType value) => ApplyStrokeOverride();

    partial void OnStrokeLineWeightChanged(double value) => ApplyStrokeOverride();

    partial void OnSelectedFillChanged(PatternOptionViewModel? value)
    {
        if (_suppress || value is null)
            return;
        if (_selection.Items.Count != 1 || _selection.Items[0] is not PolylineEntity poly)
            return;

        var old = poly.Fill;
        var next = value.Pattern;
        if (ReferenceEquals(old, next))
            return;

        _commands.Execute(new SetEntityPropertyCommand("Füllung ändern", _document, poly,
            () => poly.Fill = next, () => poly.Fill = old));
    }

    private void BuildBlockAttributes()
    {
        BlockAttributes.Clear();
        if (!IsBlockReference || _selection.Items[0] is not BlockReferenceEntity reference)
            return;

        var keys = new List<string>();
        if (reference.Definition is { } definition)
            keys.AddRange(definition.AttributeKeys);
        foreach (string key in reference.Attributes.Keys)
            if (!keys.Contains(key))
                keys.Add(key);

        foreach (string key in keys)
            BlockAttributes.Add(new BlockAttributeRowViewModel(reference, _document, key));
    }

    private void BuildSymbolParameters()
    {
        SymbolParameters.Clear();
        if (!IsParametricSymbol || _selection.Items[0] is not ParametricSymbolEntity symbol)
            return;

        foreach (string name in symbol.Parameters.Keys)
            SymbolParameters.Add(new ParametricParamRowViewModel(symbol, _document, name));
    }

    /// <summary>Adds a new attribute field to the selected block (and its definition).</summary>
    [RelayCommand]
    private void AddAttributeKey()
    {
        if (string.IsNullOrWhiteSpace(NewAttributeKey)
            || _selection.Items.Count != 1 || _selection.Items[0] is not BlockReferenceEntity reference)
        {
            return;
        }

        string key = NewAttributeKey.Trim();
        if (reference.Definition is { } definition && !definition.AttributeKeys.Contains(key))
            definition.AttributeKeys.Add(key);
        if (!reference.Attributes.ContainsKey(key))
            reference.Attributes[key] = string.Empty;

        NewAttributeKey = string.Empty;
        BuildBlockAttributes();
    }

    partial void OnSelectedTextStyleChanged(TextStyle? value)
    {
        if (_suppress || value is null)
            return;
        if (_selection.Items.Count != 1 || _selection.Items[0] is not ITextEntity)
            return;

        ApplyToSelection("Textstil ändern", entity =>
        {
            var text = (ITextEntity)entity;
            (Guid oldId, string oldFont, double oldHeight, double oldWidth) =
                (text.TextStyleId, text.FontFamily, text.Height, text.WidthFactor);
            return (
                () => CadDocument.ApplyTextStyle(text, value),
                () =>
                {
                    text.TextStyleId = oldId;
                    text.FontFamily = oldFont;
                    text.Height = oldHeight;
                    text.WidthFactor = oldWidth;
                });
        });
    }

    partial void OnUseAreaFillChanged(bool value) => ApplyAreaFill();
    partial void OnAreaRedChanged(double value) => ApplyAreaFill();
    partial void OnAreaGreenChanged(double value) => ApplyAreaFill();
    partial void OnAreaBlueChanged(double value) => ApplyAreaFill();
    partial void OnAreaAlphaChanged(double value) => ApplyAreaFill();
    partial void OnAreaGradientChanged(bool value) => ApplyAreaFill();
    partial void OnArea2RedChanged(double value) => ApplyAreaFill();
    partial void OnArea2GreenChanged(double value) => ApplyAreaFill();
    partial void OnArea2BlueChanged(double value) => ApplyAreaFill();
    partial void OnAreaAngleChanged(double value) => ApplyAreaFill();

    private void ApplyAreaFill()
    {
        OnPropertyChanged(nameof(AreaBrush));
        if (_suppress)
            return;
        if (_selection.Items.Count != 1 || _selection.Items[0] is not PolylineEntity poly)
            return;

        CoreFill? next = UseAreaFill
            ? new CoreFill(
                new CoreColor((byte)AreaRed, (byte)AreaGreen, (byte)AreaBlue, (byte)AreaAlpha),
                new CoreColor((byte)Area2Red, (byte)Area2Green, (byte)Area2Blue),
                AreaGradient, AreaAngle)
            : null;

        CoreFill? old = poly.SolidFill;
        if (Nullable.Equals(old, next))
            return;

        _commands.Execute(new SetEntityPropertyCommand("Flächenfüllung ändern", _document, poly,
            () => poly.SolidFill = next, () => poly.SolidFill = old));
    }

    private void ApplyStrokeOverride()
    {
        OnPropertyChanged(nameof(StrokeBrush));
        if (_suppress)
            return;
        if (_selection.IsEmpty)
            return;

        CoreStroke? next = UseStrokeOverride
            ? new CoreStroke(new CoreColor((byte)StrokeRed, (byte)StrokeGreen, (byte)StrokeBlue), StrokeWidth, StrokeLineType, StrokeLineWeight)
            : null;

        ApplyToSelection("Linienstil ändern", entity =>
        {
            CoreStroke? old = entity.StrokeOverride;
            return (() => entity.StrokeOverride = next, () => entity.StrokeOverride = old);
        });
    }

    private void ApplyToSelection(string name, System.Func<IEntity, (System.Action apply, System.Action revert)> factory)
    {
        var commands = new List<IUndoableCommand>();
        foreach (IEntity entity in _selection.Items)
        {
            (System.Action apply, System.Action revert) = factory(entity);
            commands.Add(new SetEntityPropertyCommand(name, _document, entity, apply, revert));
        }

        if (commands.Count == 0)
            return;

        _suppress = true;
        _commands.Execute(commands.Count == 1 ? commands[0] : new CompositeCommand(name, commands));
        _suppress = false;
    }

    private static Guid? Common(IReadOnlyList<IEntity> items, System.Func<IEntity, Guid> selector)
    {
        Guid first = selector(items[0]);
        return items.All(e => selector(e) == first) ? first : null;
    }

    private static string TypeName(IEntity entity) => entity switch
    {
        LineEntity => "Linie",
        CircleEntity => "Kreis",
        ArcEntity => "Bogen",
        PolylineEntity { IsClosed: true } => "Geschlossene Polylinie",
        PolylineEntity => "Polylinie",
        _ => entity.GetType().Name,
    };
}
