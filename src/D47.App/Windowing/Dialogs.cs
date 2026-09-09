using Avalonia.Controls;

namespace D47.App.Windowing;

/// <summary>Opening a dialog over the panel (remediation.md 11, item 11).</summary>
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
