using Avalonia.Controls;

namespace D47.App.Windowing;

/// <summary>
/// Opening a dialog over the panel (remediation.md 11, item 11).
/// <para>
/// One way in, so a dialog cannot be added that forgets to match the zoom of the window it opens
/// over. That is what happened to every dialog in the app: <see cref="ZoomHost"/> was written to
/// be attached to a window rather than built into one, precisely so zoom would not stop at the
/// panel's edge — and then exactly one window ever attached it.
/// </para>
/// </summary>
public static class Dialogs
{
    /// <summary>Shows it at the owner's size, and waits.</summary>
    public static Task Over(this Window dialog, Window owner)
    {
        ZoomHost.Match(dialog, owner);
        DarkWindowBorder.Apply(dialog);

        return dialog.ShowDialog(owner);
    }

    /// <summary>The same, for a dialog that answers something.</summary>
    public static Task<TResult> Over<TResult>(this Window dialog, Window owner)
    {
        ZoomHost.Match(dialog, owner);
        DarkWindowBorder.Apply(dialog);

        return dialog.ShowDialog<TResult>(owner);
    }
}
