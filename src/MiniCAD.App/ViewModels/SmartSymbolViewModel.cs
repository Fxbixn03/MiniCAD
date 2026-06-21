using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.App.Input;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Tools;

namespace MiniCAD.App.ViewModels;

/// <summary>One editable parameter row in the Smartsymbol form.</summary>
public partial class SmartSymbolParamRowViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public SmartSymbolParamRowViewModel(string name, double value, Action onChanged)
    {
        Name = name;
        _onChanged = onChanged;
        _valueText = CoordinateFormat.ToText(value, "0.##");
    }

    public string Name { get; }

    [ObservableProperty]
    private string _valueText;

    public double Value => CoordinateFormat.TryParse(ValueText, out double v) ? v : 0.0;

    partial void OnValueTextChanged(string value) => _onChanged();
}

/// <summary>
/// The "Smartsymbole" palette: pick a parametric catalog symbol, edit its parameters with a
/// live thumbnail preview, and insert it with the chosen values (#113/#114).
/// </summary>
public partial class SmartSymbolViewModel : ViewModelBase
{
    private const int PreviewWidth = 220;
    private const int PreviewHeight = 150;

    private readonly ToolManager _tools;
    private readonly ParametricInsertTool _tool;

    public SmartSymbolViewModel(ToolManager tools, ParametricInsertTool tool)
    {
        _tools = tools;
        _tool = tool;
        Definitions = new ObservableCollection<ParametricSymbolDefinition>(ParametricSymbolCatalog.All);
        SelectedDefinition = Definitions.FirstOrDefault();
    }

    public ObservableCollection<ParametricSymbolDefinition> Definitions { get; }

    [ObservableProperty]
    private ParametricSymbolDefinition? _selectedDefinition;

    public ObservableCollection<SmartSymbolParamRowViewModel> Parameters { get; } = new();

    [ObservableProperty]
    private Bitmap? _preview;

    partial void OnSelectedDefinitionChanged(ParametricSymbolDefinition? value)
    {
        Parameters.Clear();
        if (value is not null)
        {
            foreach (ParametricParameterSpec spec in value.Parameters)
                Parameters.Add(new SmartSymbolParamRowViewModel(spec.Name, spec.Default, UpdatePreview));
        }

        UpdatePreview();
    }

    private Dictionary<string, double> CurrentValues()
        => Parameters.ToDictionary(p => p.Name, p => p.Value);

    private void UpdatePreview()
    {
        if (SelectedDefinition is not { } definition)
        {
            Preview = null;
            return;
        }

        IReadOnlyList<IEntity> geometry = ParametricSymbolCatalog.Generate(definition.Key, CurrentValues());
        Preview = SymbolPreviewRenderer.Render(geometry, PreviewWidth, PreviewHeight);
    }

    [RelayCommand]
    private void Insert()
    {
        if (SelectedDefinition is not { } definition)
            return;

        _tool.SymbolKey = definition.Key;
        _tool.Parameters = CurrentValues();
        _tools.SetActiveTool(_tool);
    }
}
