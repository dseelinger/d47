using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The In Ship reading rendered on each theme and at the three panel sizes, saved to
/// <see cref="TestSurface.CaptureDirectory"/> for comparison with brief 03's Transcript screen (#398).
/// </summary>
public class TheTranscriptIsDrawnAsAConversationTests
{
    private static readonly GuiColourMatrix Blue = new(0x1A / 255.0, 0, 0, 0, 1, 0, 0, 0, 255.0 / 0x1A);

    private static PanelView Conversation(PanelMode mode)
    {
        var model = new PanelViewModel();

        model.Append("[calm] Functioning within tolerance, Commander. Docked at Sacred Fire, Giryak, systems quiet.");
        model.Append("plot a route to Deciat", voice: TranscriptVoice.Commander);
        model.Append("Route plotted to Deciat, four jumps. Fuel covers it with a scoop at the second star.");
        model.Append("Sacred Fire clear. Safe flying, Commander.", speaker: "Tower", sourceKey: "carrier.departure");
        model.Append("thanks", voice: TranscriptVoice.Commander);

        var panel = new PanelView { DataContext = model, Mode = mode };
        var known = new SystemsInPlay();

        known.Add(() => ["Giryak", "Deciat"]);
        panel.EnableCopy(new NoClipboard());
        panel.EnableSystemNames(known, () => "Giryak");

        return panel;
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite, 1280, 860)]
    [InlineData(ThemeCatalog.Elite, 924, 640)]
    [InlineData(ThemeCatalog.Elite, 512, 280)]
    [InlineData(ThemeCatalog.Dark, 1280, 860)]
    [InlineData(ThemeCatalog.Light, 1280, 860)]
    [InlineData(ThemeCatalog.ElitePaletteId, 1280, 860)]
    public void TheConversationIsCaptured(string themeId, double width, double height)
    {
        // The smallest size is the mini panel, which is what the app draws there.
        var panel = Conversation(height < 400 ? PanelMode.Mini : PanelMode.Full);

        var path = AppLook.Capture(
            panel,
            $"transcript-{themeId}-{width}x{height}.png",
            themeId,
            width,
            height,
            themeId == ThemeCatalog.ElitePaletteId ? Blue : null);

        Assert.True(File.Exists(path));

        var commander = panel.GetControl<StackPanel>("Bubbles").Children
            .OfType<Border>()
            .Where(turn => turn.HorizontalAlignment == HorizontalAlignment.Right);

        Assert.Equal(2, commander.Count());
    }

    private sealed class NoClipboard : IClipboard
    {
        public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
