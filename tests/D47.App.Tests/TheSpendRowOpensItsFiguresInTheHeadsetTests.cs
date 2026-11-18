using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Storage;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The SPEND row shows on the headset panel and opens the figures there (#1094).</summary>
[Trait("Category", "Integration")]
public class TheSpendRowOpensItsFiguresInTheHeadsetTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private sealed class StoppedClock(DateTimeOffset at) : D47.Core.IWallClock
    {
        public DateTimeOffset UtcNow { get; } = at;
    }

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (VrPanelSurface Surface, SpendTracker Session, SpeechSpend Speech) Headset()
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var ledger = new SpendLedger(
            Path.Combine(TestSurface.MemoryFolder("d47-spend-headset"), "spend.jsonl"),
            new MemoryFileSystem(),
            new StoppedClock(Noon),
            NullLogger.Instance);

        var session = new SpendTracker(ledger);
        var speech = new SpeechSpend();

        var desk = new SpendDesk(
            (owner, open) => new SpendDialog(
                session.Last,
                session,
                speech,
                ledger,
                settings.Current,
                TimeZoneInfo.Utc,
                breakdown: () => open(new SpendBreakdownPage(ledger, TimeZoneInfo.Utc))).Over(owner),
            () => SpendDialog.SessionDollars(session, speech, settings.Current, new D47.Core.Listening.TranscriptionSpend()),
            () => SpendDialog.SessionDetail(session, speech, settings.Current));

        var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null, spend: desk);

        Jobs();
        surface.Board.Render();
        Jobs();
        surface.Board.Render();

        return (surface, session, speech);
    }

    private static Button Spend(VrPanelSurface surface) =>
        surface.Board.View.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "TurnDetails");

    private static bool Press(VrPanelSurface surface, Control control)
    {
        var centre = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), surface.Board.View);

        Assert.NotNull(centre);

        var (width, height) = surface.Size;
        var landed = surface.Press((float)(centre!.Value.X / width), (float)(centre.Value.Y / height));

        Jobs();
        surface.Board.Render();
        Jobs();
        surface.Board.Render();

        return landed;
    }

    [AvaloniaFact]
    public void PressingSpendInTheHeadsetOpensTheFiguresThereAndBackClosesThem()
    {
        var (surface, _, _) = Headset();
        using var _ = surface;

        Assert.True(Press(surface, Spend(surface)));

        var dialog = Assert.IsType<SpendDialog>(surface.Modal);

        Assert.Contains(dialog, surface.Board.View.GetVisualDescendants());

        var pressable = dialog.GetVisualDescendants().OfType<Button>().Where(button => button.IsVisible).ToList();

        Assert.NotEmpty(pressable);
        Assert.All(pressable, button => Assert.True(button.Bounds.Height >= 44 - 0.5, $"{button.Content} is {button.Bounds.Height} tall"));

        Assert.True(surface.Back());
        Assert.Null(surface.Modal);
    }

    [AvaloniaFact]
    public void TheFiguresInTheHeadsetCloseByTheirOwnButton()
    {
        var (surface, _, _) = Headset();
        using var _ = surface;

        Press(surface, Spend(surface));

        var close = surface.Modal!.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Close"));

        Assert.True(Press(surface, close));
        Assert.Null(surface.Modal);
    }

    [AvaloniaFact]
    public void SpendBreakdownInTheHeadsetOpensThePageOnTheHeadsetPanel()
    {
        var (surface, session, _) = Headset();
        using var _ = surface;

        session.Record(
            new TurnCost(new LlmUsage(1_240, 380, 0, 18_400), 0.0231m, true),
            SpendPurposes.Conversation,
            coldPrefixExpected: true,
            "anthropic",
            "claude-opus-5");

        Press(surface, Spend(surface));

        var dialog = surface.Modal!;
        var tile = dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SpendBreakdown");

        Assert.True(Press(surface, tile));
        Assert.Null(surface.Modal);

        var view = surface.Board.View as PanelView ?? surface.Board.View.GetVisualDescendants().OfType<PanelView>().First();
        Assert.Equal("Spend breakdown", view.Nav.Trail[^1].Word);

        var page = view.GetVisualDescendants().OfType<SpendBreakdownPage>().Single();
        // The headset's window is offscreen and never shown, so visibility is read up to the page only.
        static bool Drawn(Visual control, Visual page) =>
            control.IsVisible && control.GetVisualAncestors().TakeWhile(ancestor => ancestor != page).All(ancestor => ancestor.IsVisible);

        Assert.True(page.Bounds.Width > 0, "the page is laid out");

        var section = page.GetVisualDescendants().OfType<Segment>().Single(segment => segment.Name == "SpendSection");
        Assert.Equal(TypeScale.MinimumTarget, section.Bounds.Height, 0.5);

        var pressable = page.GetVisualDescendants().OfType<Button>().Where(button => Drawn(button, page)).ToList();
        Assert.NotEmpty(pressable);
        Assert.All(pressable, button => Assert.True(button.Bounds.Height >= 44 - 0.5, $"{button.Name} is {button.Bounds.Height} tall"));
    }

    [AvaloniaFact]
    public void TheHeadsetsSessionFigureFollowsSpendBeingRecorded()
    {
        var (surface, _, speech) = Headset();
        using var _ = surface;

        var figure = surface.Board.View.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SessionFigure");
        var before = figure.Text;

        speech.Record("elevenlabs", 5_000);
        surface.RefreshSessionSpend();
        Jobs();

        Assert.NotEqual(before, figure.Text);
    }
}
