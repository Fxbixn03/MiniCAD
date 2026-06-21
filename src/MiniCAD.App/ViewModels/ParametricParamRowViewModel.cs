using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.App.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.App.ViewModels;

/// <summary>One editable parameter of a selected parametric symbol instance; regenerates on edit.</summary>
public partial class ParametricParamRowViewModel : ViewModelBase
{
    private readonly ParametricSymbolEntity _entity;
    private readonly CadDocument _document;

    public ParametricParamRowViewModel(ParametricSymbolEntity entity, CadDocument document, string name)
    {
        _entity = entity;
        _document = document;
        Name = name;
        _valueText = CoordinateFormat.ToText(entity.Parameters.GetValueOrDefault(name), "0.##");
    }

    public string Name { get; }

    [ObservableProperty]
    private string _valueText;

    partial void OnValueTextChanged(string value)
    {
        if (CoordinateFormat.TryParse(value, out double parsed))
        {
            _entity.Parameters[Name] = parsed;
            _document.NotifyEntityModified(_entity);
        }
    }
}
