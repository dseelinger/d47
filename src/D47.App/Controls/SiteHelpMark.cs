using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;

namespace D47.App.Controls;

/// <summary>The help mark a pop-up window carries, which opens the site (#252).</summary>
public static class SiteHelpMark
{
    /// <summary>A <c>?</c> that opens <paramref name="url"/>.</summary>
    /// <paramref name="url"/>.</paramref>
    public static Button For(string url, string name)
    {
        var mark = new Button
        {
            Name = name,
            Content = "?",
            FontSize = Theming.TypeScale.Secondary,
            Padding = new Thickness(7, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = url,
        };

        mark.Click += (_, _) => Open(url);

        return mark;
    }

    /// <summary>Hands the address to whatever the Commander browses with.</summary>
    internal static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
        // Nothing to say and nowhere to say it.
        }
    }
}
