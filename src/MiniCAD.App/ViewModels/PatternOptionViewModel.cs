using MiniCAD.Core.Styling;

namespace MiniCAD.App.ViewModels;

/// <summary>
/// A choosable entry in the fill-pattern dropdown. Wraps a <see cref="HatchPattern"/> (or
/// <c>null</c> for "no fill") together with a friendly label.
/// </summary>
public sealed class PatternOptionViewModel
{
    public PatternOptionViewModel(HatchPattern? pattern, string label)
    {
        Pattern = pattern;
        Label = label;
    }

    public HatchPattern? Pattern { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
