using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A row that opens a picker says it is working while the picker is being built.
/// <para>
/// Gathering the choices can mean asking the machine for its capture devices or a provider for
/// its voices. A button that looks unchanged for a second reads as a button that did not take
/// the click, and the Commander clicks it again.
/// </para>
/// </summary>
public class PickerButtonSaysItIsWorkingTests
{
    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths);
    }

    /// <summary>
    /// The glyph is built with the row and starts hidden — nothing is spinning on a surface that
    /// is merely open.
    /// </summary>
    [AvaloniaFact]
    public void ThePickerRowCarriesABusyGlyphThatStartsHidden()
    {
        var host = Open();

        var glyphs = host.View.GetVisualDescendants().OfType<BusyGlyph>().ToList();

        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.False(glyph.IsVisible));

        host.Close();
    }

    /// <summary>
    /// And it is the microphone row's own, next to the button that opens its picker — the row
    /// the Commander reported this against.
    /// </summary>
    [AvaloniaFact]
    public void TheMicrophoneRowHasOneBesideItsButton()
    {
        var host = Open();

        // Any panel, not a StackPanel specifically: this was one, until a horizontal StackPanel
        // turned out to measure its children with no width limit and let the button run out past
        // the column. Where the glyph sits is the requirement; which panel puts it there is not.
        var row = host.View
            .GetVisualDescendants()
            .OfType<Avalonia.Controls.Panel>()
            .FirstOrDefault(panel =>
                panel.Children.OfType<BusyGlyph>().Any() && panel.Children.OfType<Button>().Any());

        Assert.NotNull(row);

        // Before the button, so it appears where the eye already is rather than off past the
        // end of a control whose width follows its contents.
        Assert.IsType<BusyGlyph>(row.Children[0]);
        Assert.IsType<Button>(row.Children[1]);
        Assert.Equal(Dock.Left, DockPanel.GetDock(row.Children[0]));

        host.Close();
    }
}
