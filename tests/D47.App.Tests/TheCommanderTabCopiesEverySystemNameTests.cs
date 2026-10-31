using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every system name the Commander tab's tables draw has a copy glyph beside it; a row with no system has none.</summary>
public class TheCommanderTabCopiesEverySystemNameTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] CombatLines =
    [
        """{"timestamp":"2026-10-10T09:00:00Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10}""",
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Bounty","Target":"eagle","TotalReward":1000,"VictimFaction":"Pirates","Rewards":[{"Faction":"Alpha","Reward":1000}]}""",
    ];

    private static readonly string[] BgsLines =
    [
        """{"timestamp":"2026-10-10T17:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""",
        """{"timestamp":"2026-10-10T17:05:00Z","event":"RedeemVoucher","Type":"bounty","Factions":[{"Faction":"Gamma Pirates","Amount":3000}]}""",
    ];

    private static readonly string[] CodexLines =
    [
        """{"timestamp":"2026-10-01T09:00:00Z","event":"LoadGame"}""",
        CodexEntry("2026-10-02T10:00:00Z", 1, "Sol"),
    ];

    private static string CodexEntry(string time, long id, string system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_X;\",\"Category_Localised\":\"Biological and Geological\""
        + ",\"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\",\"SubCategory_Localised\":\"Organic structures\""
        + ",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"" + system + "\",\"SystemAddress\":1,\"BodyID\":5,\"IsNewEntry\":true}";

    private static CommanderGameState State(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static Window Show(Control page)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1000,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static List<string> Copied(Visual root) =>
        [.. root.GetVisualDescendants().OfType<Button>().Select(CopyGlyph.GetCopies).OfType<string>()];

    private static (Func<string, Task<bool>> Copy, List<string> Written) Clipboard()
    {
        var written = new List<string>();

        return (value => { written.Add(value); return Task.FromResult(true); }, written);
    }

    private static void BySystem(Visual page)
    {
        page.GetVisualDescendants().OfType<Segment>().Last().GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
    }

    private static void Press(Visual root, string copies)
    {
        root.GetVisualDescendants().OfType<Button>()
            .First(button => CopyGlyph.GetCopies(button) == copies)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ACommandersLastLocationHasAGlyphAndAnUnknownOneHasNone()
    {
        var found = new Dictionary<string, CommanderSighting>
        {
            ["F1"] = new("F1", "John Deparagon", Now, "Sol", "Anaconda"),
            ["F2"] = new("F2", "Jameson", Now.AddDays(-3), null, "Sidewinder"),
        };
        var roster = new CommanderRoster(() => found, () => 2, () => "F1", () => false, _ => { });
        var (copy, written) = Clipboard();
        var window = Show(new CommandersPage(roster, copy: copy));

        Assert.Equal(["Sol"], Copied(window));
        Press(window, "Sol");
        Assert.Equal(["Sol"], written);
        window.Close();
    }

    [AvaloniaFact]
    public void ACommandersPageWithNoCopyDelegateDrawsNoGlyph()
    {
        var found = new Dictionary<string, CommanderSighting> { ["F1"] = new("F1", "John Deparagon", Now, "Sol", "Anaconda") };
        var roster = new CommanderRoster(() => found, () => 1, () => "F1", () => false, _ => { });
        var window = Show(new CommandersPage(roster));

        Assert.Empty(Copied(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ACombatStatsSystemRowHasAGlyph()
    {
        var state = State(CombatLines);
        var (copy, written) = Clipboard();
        var page = new CombatStatsPage(() => state, () => HistoryState.Done, () => Now, copy: copy);
        var window = Show(page);

        Assert.Empty(Copied(window));

        BySystem(page);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Sol"], Copied(window));
        Press(window, "Sol");
        Assert.Equal(["Sol"], written);
        window.Close();
    }

    [AvaloniaFact]
    public void ACombatStatsPageWithNoCopyDelegateDrawsNoGlyph()
    {
        var state = State(CombatLines);
        var page = new CombatStatsPage(() => state, () => HistoryState.Done, () => Now);
        var window = Show(page);

        BySystem(page);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Copied(window));
        window.Close();
    }

    [AvaloniaFact]
    public void ABgsWorkSystemHasAGlyphAndNoCopyDelegateDrawsNone()
    {
        var state = State(BgsLines);
        var (copy, written) = Clipboard();
        var window = Show(new BgsWorkPage(() => state, () => Now.AddHours(-4), () => Now, copy: copy));

        Assert.Contains("Alpha", Copied(window));
        Press(window, "Alpha");
        Assert.Equal(["Alpha"], written);
        window.Close();

        var bare = Show(new BgsWorkPage(() => state, () => Now.AddHours(-4), () => Now));
        Assert.Empty(Copied(bare));
        bare.Close();
    }

    [AvaloniaFact]
    public void ACodexEntrySystemHasAGlyphAndNoCopyDelegateDrawsNone()
    {
        var state = State(CodexLines);
        var choice = new CodexChoice { Period = (int)CodexPeriod.Everything };
        var (copy, written) = Clipboard();
        var window = Show(new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, choice, 18, copy: copy));

        Assert.Equal(["Sol"], Copied(window));
        Press(window, "Sol");
        Assert.Equal(["Sol"], written);
        window.Close();

        var bare = Show(new CodexRegionPage(() => state, () => HistoryState.Done, () => Now, choice, 18));
        Assert.Empty(Copied(bare));
        bare.Close();
    }

    [AvaloniaFact]
    public void ACodexEntryPagesLoggingSystemHasAGlyph()
    {
        var state = State(CodexLines);
        var (copy, written) = Clipboard();
        var window = Show(new CodexEntryPage(() => state, 1, 18, "Entry 1", null, copy: copy));

        Assert.Equal(["Sol"], Copied(window));
        window.Close();
    }
}
