using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Standing: reputation on a centre-zero gauge, and the two navy ranks (#552).</summary>
public class TheStandingScreenFillsFromAZeroMarkTests
{
    private const string Reputation =
        """{"event":"Reputation","Empire":100.0,"Federation":62.4,"Alliance":18.0,"Independent":-40.2}""";

    private const string Rank =
        """{"event":"Rank","Combat":5,"Trade":6,"Explore":7,"Soldier":0,"Exobiologist":0,"Empire":12,"Federation":14,"CQC":0}""";

    private const string Progress =
        """{"event":"Progress","Combat":10,"Trade":20,"Explore":30,"Soldier":0,"Exobiologist":0,"Empire":42,"Federation":0,"CQC":0}""";

    private static JournalEvent Event(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(DateTimeOffset.UtcNow, root.GetProperty("event").GetString()!, root);
    }

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static (Window Window, PanelView Panel, CommanderGameState State) Open(params string[] events)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-standing-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var json in events)
        {
            state.Apply(Event(json));
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableCommanderName(() => state.Identity.Name);
        panel.EnableStanding(() => state);
        panel.EnableChecklist(checklists);

        var window = new Window
        {
            Content = panel,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (window, panel, state);
    }

    private static TextBlock Text(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static bool Shows(PanelView panel, string text) =>
        panel.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    [AvaloniaFact]
    public void StandingIsTheCommanderTabsFirstRoot()
    {
        var (window, panel, _) = Open(Reputation);

        Assert.Equal(StandingPage.RootKey, panel.Nav.Roots(PanelTab.Commander)[0].Key);
        Assert.True(Shows(panel, "CMDR JOHN DEPARAGON"));
        Assert.True(Shows(panel, "COMMANDER RECORD"));

        window.Close();
    }

    [AvaloniaFact]
    public void EachPowerShowsItsBandAndASignedNumber()
    {
        var (window, panel, _) = Open(Reputation);

        Assert.True(Shows(panel, "ALLIED"));
        Assert.True(Shows(panel, "FRIENDLY"));
        Assert.True(Shows(panel, "CORDIAL"));
        Assert.True(Shows(panel, "100"));
        Assert.True(Shows(panel, "62"));
        Assert.True(Shows(panel, "−40"));

        window.Close();
    }

    [AvaloniaFact]
    public void ANegativeNumberAndAnUnfriendlyBandAreRed()
    {
        var (window, panel, _) = Open(Reputation);

        Assert.Equal(Published(ThemeManager.RedKey), Ink(Text(panel, "UNFRIENDLY").Foreground));
        Assert.Equal(Published(ThemeManager.RedKey), Ink(Text(panel, "−40").Foreground));
        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Text(panel, "62").Foreground));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "FRIENDLY").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void APowerNotYetSeenDrawsADashAndAnEmptyGauge()
    {
        var (window, panel, _) = Open(Rank);

        Assert.Equal(4, panel.GetVisualDescendants().OfType<TextBlock>().Count(block => block.Text == "—"));
        Assert.False(Shows(panel, "0"));

        window.Close();
    }

    [Fact]
    public void ThePositiveFillRunsRightOfTheMarkAndTheNegativeLeft()
    {
        var positive = Gauge.CentreTrack(0.62);
        Assert.Equal(1, positive.ColumnDefinitions[0].Width.Value);
        Assert.Equal(0, positive.ColumnDefinitions[1].Width.Value);
        Assert.Equal(0.62, positive.ColumnDefinitions[2].Width.Value, 6);

        var negative = Gauge.CentreTrack(-0.34);
        Assert.Equal(0.34, negative.ColumnDefinitions[1].Width.Value, 6);
        Assert.Equal(0, negative.ColumnDefinitions[2].Width.Value);

        var unseen = Gauge.CentreTrack(null);
        Assert.Equal(0, unseen.ColumnDefinitions[1].Width.Value);
        Assert.Equal(0, unseen.ColumnDefinitions[2].Width.Value);
    }

    [AvaloniaFact]
    public void ANavyRankSaysHowFarToTheNextAndTheTopHasNoNext()
    {
        var (window, panel, _) = Open(Rank, Progress);

        Assert.True(Shows(panel, "Duke"));
        Assert.True(Shows(panel, "RANK 12 OF 14 · 42% TO PRINCE"));
        Assert.True(Shows(panel, "Admiral"));
        Assert.True(Shows(panel, "RANK 14 OF 14"));

        window.Close();
    }

    [AvaloniaFact]
    public void ANewReputationEventRedrawsThePageOnTheTick()
    {
        var (window, panel, state) = Open(Reputation);

        Assert.False(panel.TickCommander() && Shows(panel, "−90"));

        state.Apply(Event("""{"event":"Reputation","Empire":100.0,"Federation":62.4,"Alliance":18.0,"Independent":-95.0}"""));

        Assert.True(panel.TickCommander());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(panel, "−95"));
        Assert.True(Shows(panel, "HOSTILE"));
        Assert.False(panel.TickCommander());

        window.Close();
    }

    [AvaloniaFact]
    public void TheFooterCarriesAPhraseGetStandingAnswers()
    {
        var (window, panel, _) = Open(Reputation);

        Assert.True(Shows(panel, $"Say: “{StandingPage.Phrase}”"));
        Assert.Contains("standing with the empire", StandingPage.Phrase, StringComparison.OrdinalIgnoreCase);

        window.Close();
    }

    [AvaloniaFact]
    public void TheStandingScreenIsCaptured()
    {
        var (window, _, _) = Open(Reputation, Rank, Progress);

        using var frame = window.CaptureRenderedFrame()!;
        var path = "commander-standing.png";
        frame.SaveCapture(path);

        window.Close();
    }
}
