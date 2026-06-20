using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Documents;
using CoreColor = MiniCAD.Core.Styling.Color;
using LineType = MiniCAD.Core.Styling.LineType;

namespace MiniCAD.App.ViewModels;

/// <summary>A single layer row in the structure panel: editable name, visibility and color.</summary>
public partial class LayerItemViewModel : ViewModelBase
{
    private readonly CadDocument _document;

    public LayerItemViewModel(CadDocument document, Layer model)
    {
        _document = document;
        Model = model;
        _name = model.Name;
        _state = model.State;

        CoreColor color = model.Stroke.Color;
        _colorRed = color.R;
        _colorGreen = color.G;
        _colorBlue = color.B;
        _lineType = model.Stroke.LineType;
        _lineWeight = model.Stroke.LineWeightMm;
    }

    public Layer Model { get; }

    public LineType[] LineTypeOptions { get; } = Enum.GetValues<LineType>();

    public double[] LineWeightOptions { get; } = StyleOptions.LineWeights;

    [ObservableProperty]
    private LineType _lineType;

    [ObservableProperty]
    private double _lineWeight;

    partial void OnLineTypeChanged(LineType value) => _document.SetLayerLineType(Model, value);

    partial void OnLineWeightChanged(double value) => _document.SetLayerLineWeight(Model, value);

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ElementState _state;

    /// <summary>State as a 0/1/2 index for the dropdown (Aktiviert / Gesperrt / Deaktiviert).</summary>
    public int StateIndex
    {
        get => (int)State;
        set => State = (ElementState)value;
    }

    [ObservableProperty]
    private double _colorRed;

    [ObservableProperty]
    private double _colorGreen;

    [ObservableProperty]
    private double _colorBlue;

    /// <summary>The layer color (for the row swatch and editor preview).</summary>
    public Color Color => Color.FromRgb((byte)ColorRed, (byte)ColorGreen, (byte)ColorBlue);

    public IBrush ColorBrush => new SolidColorBrush(Color);

    public string ColorHex => $"#{(byte)ColorRed:X2}{(byte)ColorGreen:X2}{(byte)ColorBlue:X2}";

    partial void OnNameChanged(string value) => _document.RenameLayer(Model, value);

    partial void OnStateChanged(ElementState value)
    {
        OnPropertyChanged(nameof(StateIndex));
        _document.SetLayerState(Model, value);
    }

    partial void OnColorRedChanged(double value) => ApplyColor();

    partial void OnColorGreenChanged(double value) => ApplyColor();

    partial void OnColorBlueChanged(double value) => ApplyColor();

    private void ApplyColor()
    {
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(ColorBrush));
        OnPropertyChanged(nameof(ColorHex));
        _document.SetLayerColor(Model, new CoreColor((byte)ColorRed, (byte)ColorGreen, (byte)ColorBlue));
    }
}
