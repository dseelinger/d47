using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

// System.IO is implicitly imported and System.IO.Path is not the one meant here - the same aliasing Glyphs.cs
// carries, for the same reason.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// Reset is a drawn mark at both levels, and the same one
/// (https://github.com/dseelinger/d47/issues/69).
/// </summary>
public sealed class ResetIsAGlyphAtBothLevelsTests
{
    /// <summary>
    /// The drawn resets — every glyph button on the page that is not one of the two bulk controls above
 /// the cards.
    /// </summary>
    private static IReadOnlyList<Button> ResetButtons(SettingsHost host)
    {
        // Found in the tree rather than by FindControl: the bulk glyphs are built in code now (2026-09-01),
        // so they are not in the axaml's namescope and FindControl answers null — which would quietly turn
        // this exclusion off and count them as resets.
        var bulk = host.View.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Name == D47.App.Settings.SettingsView.BulkName);

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

    /// <summary>Neither reset is text.</summary>
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
    /// And what replaced them is the shared constant rather than two similar paths, so the two scales
    /// cannot drift apart the way a word and a character already had.
    /// </summary>
    [AvaloniaFact]
    public void EveryResetDrawsTheSameMark()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var drawn = ResetButtons(host);

        Assert.NotEmpty(drawn);

        // Compared by the geometry's bounds against a path built from the constant, because Data is a parsed
        // StreamGeometry and does not hand back the string it came from.
        var reference = Glyphs.Draw(Glyphs.Reset, D47.App.Theming.ThemeManager.TextMutedKey).Data!.Bounds;

        foreach (var button in drawn)
        {
            var path = (Path)button.Content!;

            Assert.NotNull(path.Data);
            Assert.Equal(reference, path.Data!.Bounds);
        }
    }

    /// <summary>A mark with no text has no accessible name unless one is given.</summary>
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

    [Fact]
    public void TheMarkIsAnArcAndAnArrowhead()
    {
        Assert.Contains(" A ", Glyphs.Reset);

        // Two subpaths: the arc, then the head.
        Assert.Equal(2, Glyphs.Reset.Split('M', StringSplitOptions.RemoveEmptyEntries).Length);

        // Nearly a full turn rather than the three-quarter arc it replaced, which at fourteen pixels read as
        // a comma with a tick on it.
        Assert.Contains(" 0 1 ", Glyphs.Reset);
    }
}
