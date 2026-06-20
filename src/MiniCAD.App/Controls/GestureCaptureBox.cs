using Avalonia.Controls;
using Avalonia.Input;

namespace MiniCAD.App.Controls;

/// <summary>
/// A read-only text box that captures a key combination when focused: press the desired keys
/// and it records the matching gesture string (e.g. "Ctrl+S"). Backspace/Delete clears the
/// binding, Escape leaves it unchanged. Its <see cref="TextBox.Text"/> is meant to be bound
/// two-way to the persisted gesture string.
/// </summary>
public sealed class GestureCaptureBox : TextBox
{
    public GestureCaptureBox()
    {
        IsReadOnly = true;
        PlaceholderText = "Taste drücken…";
    }

    // Render with the stock TextBox style.
    protected override System.Type StyleKeyOverride => typeof(TextBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Swallow lone modifier presses; wait for the actual key.
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            SetCurrentValue(TextProperty, string.Empty);
            e.Handled = true;
            return;
        }

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        SetCurrentValue(TextProperty, gesture.ToString());
        e.Handled = true;
    }
}
