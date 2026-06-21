using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.App.ViewModels;

/// <summary>One editable attribute (key + value) of a selected block instance.</summary>
public partial class BlockAttributeRowViewModel : ViewModelBase
{
    private readonly BlockReferenceEntity _entity;
    private readonly CadDocument _document;

    public BlockAttributeRowViewModel(BlockReferenceEntity entity, CadDocument document, string key)
    {
        _entity = entity;
        _document = document;
        Key = key;
        _value = entity.Attributes.GetValueOrDefault(key, string.Empty);
    }

    public string Key { get; }

    [ObservableProperty]
    private string _value;

    partial void OnValueChanged(string value)
    {
        _entity.Attributes[Key] = value;
        _document.NotifyEntityModified(_entity);
    }
}
