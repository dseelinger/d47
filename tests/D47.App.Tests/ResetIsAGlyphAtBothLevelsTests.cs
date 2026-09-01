using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

// System.IO is implicitly imported and System.IO.Path is not the one meant here - the same
// aliasing Glyphs.cs carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// Reset is a drawn mark at both levels, and the same one
/// (https://github.com/dseelinger/d47/issues/69).
/// <para>
/// The card-level control was the word <c>Reset</c> and the row-level one was <c>↺</c>, a text
/// character. Both are now <see cref="Glyphs.Reset"/>, which matters for a reason that is not
/// cosmetic: a character is whatever the installed font carries, so it arrives at a different
/// weight from the four marks beside it, hangs off a baseline rather than sitting in its box, and
/// draws as a hollow rectangle on a machine whose face lacks U+21BA.
/// </para>
/// </summary>
public sealed class ResetIsAGlyphAtBothLevelsTests
{
    /// <summary>
    /// The drawn resets — every glyph button on the page that is not one of the two bulk
    /// controls above the cards
    /// (<a href="https://github.com/dseelinger/d47/issues/223">#223</a>).
    /// <para>
    /// <b>"Every button whose content is a Path" used to be the whole rule, and #223 made it
    /// false.</b> Expand all and Collapse all are glyph buttons on this page and are not resets,
    /// so both tests below caught them and failed — correctly, on the old rule. The
    /// discriminator is structural rather than textual: a reset sits in a card header or on a
    /// row, and the pair sits in <c>BulkExpand</c>. Filtering on the accessible name would have
    /// made <see cref="EveryResetStillSaysWhatItIs"/> assert itself.
    /// </para>
    /// </summary>
    private static IReadOnlyList<Button> ResetButtons(SettingsHost host)
    {
        var bulk = host.View.FindControl<Control>("BulkExpand");

        return
        [
            .. host.View.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Content is Path)

                // The info glyph is a Path on every row and is not a reset (2026-09-01).
                .Where(button => button.Name?.StartsWith(
                    D47.App.Settings.SettingsView.RowInfoPrefix, StringComparison.Ordinal) is not true)
                .Where(button => bulk is null || !button.GetVisualAncestors().Contains(bulk)),
        ];
    }

    /// <summary>
    /// Neither reset is text. Asserted as "no reset button has a string for content" rather than by
    /// counting paths, because the failure this guards is a character coming back — and a character
    /// is a string whatever it looks like on the machine that wrote it.
    /// </summary>
    [AvaloniaFact]
    public void NoResetControlIsAWordOrACharacter()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        // Something has to have changed, or every reset is hidden and the test asserts nothing.
        settings.Apply(
            D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var texts = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => button.Content as string)
            .Where(content => content is not null)
            .ToList();

        Assert.DoesNotContain("Reset", texts);
        Assert.DoesNotContain("↺", texts);
    }

    /// <summary>
    /// And what replaced them is the shared constant rather than two similar paths, so the two
    /// scales cannot drift apart the way a word and a character already had.
    /// </summary>
    [AvaloniaFact]
    public void EveryResetDrawsTheSameMark()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var drawn = ResetButtons(host);

        Assert.NotEmpty(drawn);

        // Compared by the geometry's bounds against a path built from the constant, because Data is
        // a parsed StreamGeometry and does not hand back the string it came from. A call site
        // switched to a different mark moves the bounds; a call site switched to a character stops
        // being a Path at all and is caught by the test above.
        var reference = Glyphs.Draw(Glyphs.Reset, D47.App.Theming.ThemeManager.TextMutedKey).Data!.Bounds;

        foreach (var button in drawn)
        {
            var path = (Path)button.Content!;

            Assert.NotNull(path.Data);
            Assert.Equal(reference, path.Data!.Bounds);
        }
    }

    /// <summary>
    /// <b>A mark with no text has no accessible name unless one is given.</b> The word and the
    /// character were each their button's name by being its content; replacing them with a
    /// <c>Path</c> would otherwise have handed a screen reader an unnamed button — the same fault,
    /// in the same file, that composing the caption into runs caused once before.
    /// </summary>
    [AvaloniaFact]
    public void EveryResetStillSaysWhatItIs()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        foreach (var button in ResetButtons(host))
        {
            var name = AutomationProperties.GetName(button);

            Assert.False(string.IsNullOrWhiteSpace(name), "a drawn reset with no accessible name");
            Assert.StartsWith("Reset ", name);
        }
    }

    /// <summary>
    /// The mark is an arc and a separate arrowhead. Pinned as geometry rather than as a picture,
    /// because the one thing a test cannot say about a glyph is whether it reads as "undo" — that
    /// needs eyes, and the circle read the other way is "refresh", which is a different promise
    /// about a button that throws work away.
    /// <para>
    /// <b>The sweep flag used to be pinned as a proxy for that, and it is not one</b> (redrawn
    /// 2026-09-01). It says which way the <em>pen</em> travels; what a reader sees is where the
    /// arrowhead is and which way it points, and the two need not agree. The mark this file now
    /// holds is written clockwise with the head at the top of the gap pointing back — and it was
    /// chosen off a drawing, which is the only instrument that can settle it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMarkIsAnArcAndAnArrowhead()
    {
        Assert.Contains(" A ", Glyphs.Reset);

        // Two subpaths: the arc, then the head.
        Assert.Equal(2, Glyphs.Reset.Split('M', StringSplitOptions.RemoveEmptyEntries).Length);

        // Nearly a full turn rather than the three-quarter arc it replaced, which at fourteen
        // pixels read as a comma with a tick on it.
        Assert.Contains(" 0 1 ", Glyphs.Reset);
    }
}
