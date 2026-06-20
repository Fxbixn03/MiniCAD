using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tools;

/// <summary>
/// A request from a tool to open the host's inline text editor. The host shows a field
/// anchored at <see cref="AnchorWorld"/>, seeds it with <see cref="InitialText"/>, and on
/// confirm/abort invokes <see cref="Commit"/>/<see cref="Cancel"/>. This keeps the editor UI
/// in the App while several tools (text, leader, …) can drive it without knowing each other.
/// </summary>
public sealed class TextEditRequest
{
    public TextEditRequest(Point2D anchorWorld, string initialText, bool multiline,
        Action<string> commit, Action cancel)
    {
        AnchorWorld = anchorWorld;
        InitialText = initialText ?? string.Empty;
        Multiline = multiline;
        Commit = commit ?? throw new ArgumentNullException(nameof(commit));
        Cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
    }

    /// <summary>Insertion point in world coordinates the editor should be anchored at.</summary>
    public Point2D AnchorWorld { get; }

    /// <summary>Text to pre-fill (empty for a new entity).</summary>
    public string InitialText { get; }

    /// <summary>Whether the editor should accept newlines.</summary>
    public bool Multiline { get; }

    /// <summary>Called with the typed text when the user confirms.</summary>
    public Action<string> Commit { get; }

    /// <summary>Called when the user aborts the edit.</summary>
    public Action Cancel { get; }
}
