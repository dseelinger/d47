using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>What a furnished page marks so a surface nothing can be pressed on does not draw it (#202).</summary>
public static class PageChrome
{
    /// <summary>The class a container of pressable things carries.</summary>
    public const string Class = "page-chrome";

    /// <summary>
    /// Marks a container as chrome and hands it back, so it can be written inline where the container
    /// is built rather than as a second statement somewhere below it.
    /// </summary>
    public static T AsChrome<T>(this T control)
        where T : Control
    {
        control.Classes.Add(Class);
        return control;
    }

    /// <summary>Whether this control was marked.</summary>
    public static bool IsChrome(this Control control) => control.Classes.Contains(Class);

    /// <summary>Caps an open settings strip at half the height of the page it sits on (#340).</summary>
    public static void CapStripHeight(this Control host, Control strip)
    {
        strip.MaxHeight = host.Bounds.Height / 2;
        host.SizeChanged += (_, e) => strip.MaxHeight = e.NewSize.Height / 2;
    }
}
