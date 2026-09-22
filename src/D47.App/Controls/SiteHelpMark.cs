using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;

namespace D47.App.Controls;

/// <summary>The help mark a pop-up window carries, which opens the site (#252).</summary>
public static class SiteHelpMark
{
    /// <summary>A quiet <c>HELP</c> that opens <paramref name="url"/>.</summary>
    public static Button For(string url, string name)
    {
        var mark = Glyphs.Quiet(
            new Button
            {
                Name = name,
                VerticalAlignment = VerticalAlignment.Center,

                // Held for anything that needs to know where this goes without reading it off the tip (#382).
                Tag = url,
            },
            "HELP",
            "Opens in your browser");

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
