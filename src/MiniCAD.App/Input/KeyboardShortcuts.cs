using System;
using System.Collections.Generic;
using System.Text.Json;
using Avalonia.Input;
using MiniCAD.App.Configuration;

namespace MiniCAD.App.Input;

/// <summary>
/// The customizable keyboard-shortcut map. It seeds sensible defaults, overlays the user's
/// overrides loaded from <see cref="AppConfig"/>, and resolves a pressed key to the action it
/// triggers. Gestures are stored as Avalonia gesture strings (e.g. "Ctrl+S", "L"), which keeps
/// the persisted form human-readable and round-trips cleanly through <see cref="KeyGesture"/>.
/// </summary>
public sealed class KeyboardShortcuts
{
    /// <summary>Display label + default gesture for every bindable action, in display order.</summary>
    public static readonly IReadOnlyList<(ShortcutAction Action, string Label, string Default)> Definitions = new[]
    {
        (ShortcutAction.Select, "Auswahl", ""),
        (ShortcutAction.Line, "Linie", "L"),
        (ShortcutAction.Rectangle, "Rechteck", "R"),
        (ShortcutAction.Circle, "Kreis", "C"),
        (ShortcutAction.Arc, "Bogen", "A"),
        (ShortcutAction.Ellipse, "Ellipse", "E"),
        (ShortcutAction.Polyline, "Polylinie", "P"),
        (ShortcutAction.Spline, "Spline", "K"),
        (ShortcutAction.Point, "Punkt", "N"),
        (ShortcutAction.Text, "Text", "X"),
        (ShortcutAction.Leader, "Führungslinie", "G"),
        (ShortcutAction.LinearDimension, "Maß (linear)", "D"),
        (ShortcutAction.AngularDimension, "Maß (Winkel)", "W"),
        (ShortcutAction.RadialDimension, "Maß (Radius/⌀)", "Shift+D"),
        (ShortcutAction.ElevationDimension, "Höhenkote", "H"),
        (ShortcutAction.OrdinateDimension, "Koordinatenmaß", "I"),
        (ShortcutAction.Move, "Verschieben", "M"),
        (ShortcutAction.Copy, "Kopieren", "Shift+C"),
        (ShortcutAction.Rotate, "Drehen", "Shift+R"),
        (ShortcutAction.Mirror, "Spiegeln", "Shift+M"),
        (ShortcutAction.Scale, "Skalieren", "Shift+S"),
        (ShortcutAction.Offset, "Parallele", "O"),
        (ShortcutAction.Trim, "Stutzen/Dehnen", "T"),
        (ShortcutAction.Stretch, "Dehnen (Fenster)", "S"),
        (ShortcutAction.Fillet, "Abrundung/Fase", "Shift+F"),
        (ShortcutAction.Array, "Array", "Shift+A"),
        (ShortcutAction.Delete, "Löschen", "Delete"),
        (ShortcutAction.Undo, "Rückgängig", "Ctrl+Z"),
        (ShortcutAction.Redo, "Wiederholen", "Ctrl+Y"),
        (ShortcutAction.ZoomToFit, "Zoom anpassen", "F"),
        (ShortcutAction.ToggleSnap, "Fang ein/aus", "F3"),
        (ShortcutAction.Ortho, "Ortho ein/aus", "F8"),
        (ShortcutAction.Polar, "Polar-Tracking ein/aus", "F10"),
        (ShortcutAction.SetNullPoint, "Nullpunkt setzen", "U"),
        (ShortcutAction.NewProject, "Neues Projekt", "Ctrl+N"),
        (ShortcutAction.OpenProject, "Projekt öffnen", "Ctrl+O"),
        (ShortcutAction.Save, "Speichern", "Ctrl+S"),
        (ShortcutAction.SaveAs, "Speichern unter", "Ctrl+Shift+S"),
    };

    private static readonly Dictionary<ShortcutAction, string> DefaultGestures = BuildDefaults();

    private readonly Dictionary<ShortcutAction, string> _gestures = new();

    public KeyboardShortcuts()
    {
        foreach ((ShortcutAction action, _, string @default) in Definitions)
            _gestures[action] = @default;

        Load();
    }

    /// <summary>Raised whenever a binding changes, so the key dispatcher can refresh.</summary>
    public event EventHandler? Changed;

    public static string DefaultFor(ShortcutAction action) => DefaultGestures.GetValueOrDefault(action, "");

    public string Get(ShortcutAction action) => _gestures.GetValueOrDefault(action, "");

    public void Set(ShortcutAction action, string gesture)
    {
        gesture = (gesture ?? string.Empty).Trim();
        if (_gestures.GetValueOrDefault(action, "") == gesture)
            return;

        _gestures[action] = gesture;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Resolves a pressed key to the action it is bound to, if any.</summary>
    public bool TryResolve(KeyEventArgs e, out ShortcutAction action)
    {
        foreach ((ShortcutAction candidate, string gesture) in _gestures)
        {
            if (gesture.Length == 0)
                continue;

            if (TryParse(gesture, out KeyGesture parsed) && parsed.Matches(e))
            {
                action = candidate;
                return true;
            }
        }

        action = default;
        return false;
    }

    private void Load()
    {
        string raw = AppConfig.Instance.Shortcuts;
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (stored is null)
                return;

            foreach ((string key, string value) in stored)
            {
                if (Enum.TryParse(key, out ShortcutAction action))
                    _gestures[action] = value ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // Corrupt config — fall back to the defaults already seeded.
        }
    }

    private void Save()
    {
        var map = new Dictionary<string, string>();
        foreach ((ShortcutAction action, string gesture) in _gestures)
            map[action.ToString()] = gesture;

        AppConfig.Instance.Shortcuts = JsonSerializer.Serialize(map);
    }

    private static bool TryParse(string gesture, out KeyGesture parsed)
    {
        try
        {
            parsed = KeyGesture.Parse(gesture);
            return true;
        }
        catch (Exception)
        {
            parsed = null!;
            return false;
        }
    }

    private static Dictionary<ShortcutAction, string> BuildDefaults()
    {
        var map = new Dictionary<ShortcutAction, string>();
        foreach ((ShortcutAction action, _, string @default) in Definitions)
            map[action] = @default;
        return map;
    }
}
