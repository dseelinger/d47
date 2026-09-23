using Avalonia.Controls;
using D47.App.Theming;

namespace D47.App.Windowing;

/// <summary>Opening a dialog over the panel (remediation.md 11, item 11).</summary>
public static class Dialogs
{
    /// <summary>
    /// Shows it at the owner's size, and waits. The caption strip goes on last, after
    /// <see cref="ZoomHost.Match"/> has already wrapped the dialog's content — the strip is chrome and
    /// stays outside that scaling rather than being zoomed along with the page it sits on (#286).
    /// A dialog's border is the accent; <paramref name="borderKey"/> names another colour for a window
    /// that is not modal in look.
    /// </summary>
    public static Task Over(this Window dialog, Window owner, bool showMinimize = true, string borderKey = ThemeManager.AKey)
    {
        Dress(dialog, owner, showMinimize, borderKey);

        return dialog.ShowDialog(owner);
    }

    /// <summary>The same, for a dialog that answers something.</summary>
    public static Task<TResult> Over<TResult>(this Window dialog, Window owner, bool showMinimize = true, string borderKey = ThemeManager.AKey)
    {
        Dress(dialog, owner, showMinimize, borderKey);

        return dialog.ShowDialog<TResult>(owner);
    }

    private static void Dress(Window dialog, Window owner, bool showMinimize, string borderKey)
    {
        ZoomHost.Match(dialog, owner);
        WindowBorder.Apply(dialog, borderKey);
        CaptionStrip.Apply(dialog, showMinimize);
    }
}
