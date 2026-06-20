using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Styling;
using CoreColor = MiniCAD.Core.Styling.Color;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// The "Muster" panel. Shows the always-available global pattern library and lets the user
/// define project-specific patterns (a name, an angle, a spacing, optionally a crossing second
/// family and a colour). Project patterns become available wherever fills are chosen.
/// </summary>
public partial class PatternsViewModel : ViewModelBase
{
    private readonly CadDocument _document;

    public PatternsViewModel(CadDocument document)
    {
        _document = document;
        _document.Changed += OnDocumentChanged;
        RebuildProjectPatterns();
    }

    /// <summary>The shared, read-only built-in patterns.</summary>
    public IReadOnlyList<HatchPattern> GlobalPatterns => PatternLibrary.All;

    public ObservableCollection<HatchPattern> ProjectPatterns { get; } = new();

    [ObservableProperty]
    private HatchPattern? _selectedProjectPattern;

    [ObservableProperty]
    private string _newName = "Mein Muster";

    [ObservableProperty]
    private double _newAngle = 45;

    [ObservableProperty]
    private double _newSpacing = 30;

    [ObservableProperty]
    private bool _newCross;

    [ObservableProperty]
    private double _newRed = 190;

    [ObservableProperty]
    private double _newGreen = 190;

    [ObservableProperty]
    private double _newBlue = 196;

    public IBrush NewColorBrush => new SolidColorBrush(Avalonia.Media.Color.FromRgb((byte)NewRed, (byte)NewGreen, (byte)NewBlue));

    partial void OnNewRedChanged(double value) => OnPropertyChanged(nameof(NewColorBrush));

    partial void OnNewGreenChanged(double value) => OnPropertyChanged(nameof(NewColorBrush));

    partial void OnNewBlueChanged(double value) => OnPropertyChanged(nameof(NewColorBrush));

    [RelayCommand]
    private void AddProjectPattern()
    {
        double spacing = NewSpacing <= 0 ? 1 : NewSpacing;
        var lines = new List<HatchLineDefinition> { new(NewAngle, spacing) };
        if (NewCross)
            lines.Add(new HatchLineDefinition(NewAngle + 90, spacing));

        var color = new CoreColor((byte)NewRed, (byte)NewGreen, (byte)NewBlue);
        var pattern = new HatchPattern(string.IsNullOrWhiteSpace(NewName) ? "Muster" : NewName, lines, color);
        _document.AddPattern(pattern);
        SelectedProjectPattern = pattern;
    }

    [RelayCommand]
    private void RemoveProjectPattern()
    {
        if (SelectedProjectPattern is { } pattern)
            _document.RemovePattern(pattern);
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        if (e.Kind is DocumentChangeKind.PatternsChanged or DocumentChangeKind.Reloaded)
            RebuildProjectPatterns();
    }

    private void RebuildProjectPatterns()
    {
        ProjectPatterns.Clear();
        foreach (HatchPattern pattern in _document.Patterns)
            ProjectPatterns.Add(pattern);
    }
}
