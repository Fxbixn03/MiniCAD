using System.Text.RegularExpressions;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Find &amp; replace over annotation text (#238). Pure string/entity helpers: the caller decides
/// the entity scope and wraps replacements in undoable commands.
/// </summary>
public static class TextSearch
{
    public static bool Matches(string text, string query, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(query))
            return false;

        return Regex.IsMatch(text ?? string.Empty, BuildPattern(query, wholeWord), Options(matchCase));
    }

    /// <summary>Replaces every occurrence of <paramref name="query"/> with <paramref name="replacement"/>.</summary>
    public static string Replace(string text, string query, string replacement, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return text ?? string.Empty;

        // A MatchEvaluator inserts the replacement literally (so '$' etc. aren't special).
        return Regex.Replace(text, BuildPattern(query, wholeWord), _ => replacement ?? string.Empty, Options(matchCase));
    }

    public static int CountOccurrences(string text, string query, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return 0;

        return Regex.Matches(text, BuildPattern(query, wholeWord), Options(matchCase)).Count;
    }

    /// <summary>The text entities in <paramref name="entities"/> that contain <paramref name="query"/>.</summary>
    public static IReadOnlyList<ITextEntity> Find(
        IEnumerable<IEntity> entities, string query, bool matchCase, bool wholeWord)
    {
        ArgumentNullException.ThrowIfNull(entities);

        return entities
            .OfType<ITextEntity>()
            .Where(t => Matches(t.Text, query, matchCase, wholeWord))
            .ToList();
    }

    private static string BuildPattern(string query, bool wholeWord)
    {
        string pattern = Regex.Escape(query);
        return wholeWord ? $@"\b{pattern}\b" : pattern;
    }

    private static RegexOptions Options(bool matchCase)
        => matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
}
