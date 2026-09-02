using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia;

namespace D47.App.Controls;

/// <summary>
/// The help mark a pop-up window carries, which opens the site
/// (<a href="https://github.com/dseelinger/d47/issues/252">#252</a>).
/// <para>
/// <b>A dialog cannot reach the in-app help, and that is a fact about the mechanism rather than an
/// omission.</b> <c>HelpLevel.Open</c> works by <c>nav.Take</c> — help is a level of the panel —
/// and these windows are shown with <c>ShowDialog</c> over it. A mark inside one has no panel to
/// navigate, because the panel is behind a modal.
/// </para>
/// <para>
/// <b>The Commander's ruling, 2026-09-01: open the site, as <c>CoverageWindow</c> already did.</b>
/// The two roads not taken are worth knowing. Dismissing the dialog to reach the help level throws
/// away whatever the Commander had chosen in it — a scale, a toggle, a corpus that took minutes to
/// read — which punishes the question. Drawing the band inside the dialog means a second drawer,
/// and the panel's is the one that gets fixed when a band renders badly.
/// </para>
/// <para>
/// So a browser tab, and the concise in-app form is given up for these windows. The page is the
/// same one authored once: the panel would have drawn the top of it, and the site shows all of it.
/// </para>
/// </summary>
public static class SiteHelpMark
{
    /// <summary>
    /// A <c>?</c> that opens <paramref name="url"/>.
    /// <para>
    /// <b>The address is the tooltip</b>, following <c>CoverageWindow</c>: a control that launches
    /// a browser should say where it is about to go before it goes there, and on a window with no
    /// status line there is nowhere else to say it.
    /// </para>
    /// </summary>
    /// <param name="name">
    /// The control's name, so a test can find this particular mark. Windows have more than one
    /// button and none of them is reachable by role alone.
    /// </param>
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

    /// <summary>
    /// Hands the address to whatever the Commander browses with.
    /// <para>
    /// <c>UseShellExecute</c> because a URL is not an executable: without it this is an attempt to
    /// run a file called <c>https:</c>. Swallowed rather than reported, because a machine with no
    /// browser registered is not a state a help mark should crash the app over.
    /// </para>
    /// <para>
    /// Internal since <a href="https://github.com/dseelinger/d47/issues/269">#269</a>, so
    /// <see cref="InfoGlyph"/>'s way out to the same page is this launch rather than a second one
    /// that would have to be kept in step with it.
    /// </para>
    /// </summary>
    internal static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Nothing to say and nowhere to say it. The tooltip already showed the address, so a
            // Commander whose browser did not open can still read where they were being sent.
        }
    }
}
