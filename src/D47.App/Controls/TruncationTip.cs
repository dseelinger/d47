using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>
/// A tooltip that names a piece of text only while that text is actually cut off by
/// <see cref="TextTrimming.CharacterEllipsis"/> — re-checked whenever layout changes, since whether
/// a column clips its text depends on the width it was given, not just on the text itself (#382).
/// </summary>
public static class TruncationTip
{
    /// <summary>
    /// Watches <paramref name="text"/> for ellipsising and keeps a tooltip on <paramref name="target"/>
    /// (the text block itself, where <paramref name="target"/> is left null) in step: present with
    /// <paramref name="fullText"/> while trimmed, absent while the text fits.
    /// </summary>
    public static void Watch(TextBlock text, Func<string?> fullText, Control? target = null)
    {
        target ??= text;

        void Check() =>
            ToolTip.SetTip(target, IsTrimmed(text) ? fullText() : null);

        text.LayoutUpdated += (_, _) => Check();
        Check();
    }

    private static bool IsTrimmed(TextBlock text) =>
        text.TextLayout.TextLines.Any(line => line.HasCollapsed);
}
