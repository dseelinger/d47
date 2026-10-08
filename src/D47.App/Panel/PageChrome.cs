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

    /// <summary>The tile that opens a settings strip, for the page to hand to the title line; null without a strip.</summary>
    public static Control? ToolOf(Control? strip) => (strip as IPageChrome)?.BarTool;

    /// <summary>Docks an open settings strip under the page's top rows and caps it at half the page.</summary>
    public static void DockStripAtTop(this DockPanel root, Control strip)
    {
        DockPanel.SetDock(strip, Dock.Top);
        root.Children.Add(strip);
        root.CapStripHeight(strip);
    }

    /// <summary>Caps an open settings strip at half the height of the page it sits on (#340).</summary>
    public static void CapStripHeight(this Control host, Control strip)
    {
        strip.MaxHeight = host.Bounds.Height / 2;
        host.SizeChanged += (_, e) => strip.MaxHeight = e.NewSize.Height / 2;
    }
}
