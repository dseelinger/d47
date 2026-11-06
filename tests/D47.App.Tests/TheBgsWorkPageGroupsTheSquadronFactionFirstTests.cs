using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › BGS work puts the squadron faction's work in its own groups (#2172).</summary>
public class TheBgsWorkPageGroupsTheSquadronFactionFirstTests
{
    private const string Faction = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T16:14:00Z", CultureInfo.InvariantCulture);

    private static string[] Lines(bool member) =>
    [
        .. member
            ? new[] { """{"timestamp":"2026-10-10T16:00:00Z","event":"SquadronStartup","SquadronName":"Greybeards","SquadronID":7,"CurrentRank":1}""" }
            : [],
        """{"timestamp":"2026-10-10T16:01:00Z","event":"FSDJump","StarSystem":"Home","SystemAddress":1,"Factions":[{"Name":"Neighbour Union"},{"Name":"Kestrel Reach Compact"}]}""",
        Mission("2026-10-10T17:00:00Z", "Far Party", 9),
        Mission("2026-10-10T17:01:00Z", "Far Party", 9),
        Mission("2026-10-10T17:02:00Z", "Neighbour Union", 1),
        Mission("2026-10-10T17:03:00Z", "Kestrel Reach Compact", 1),
    ];

    private static string Mission(string time, string faction, long system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
        + "\",\"Influence\":[{\"SystemAddress\":" + system + ",\"Trend\":\"UpGood\",\"Influence\":\"+\"}]}]}";

    private static CommanderGameState State(bool member = true) => JournalScript.Commander("F1", "Fixture", Lines(member));

    private static IReadOnlyList<BgsWorkRow> RowsOf(CommanderGameState state) =>
        BgsWorkPage.Rows(state.BgsWork, Tick, Now, SystemStandings.Empty);

    private static Window Open(CommanderGameState state, string? faction)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var window = new Window { Content = new BgsWorkPage(() => state, () => Tick, () => Now, squadronFaction: () => faction), Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    [Fact]
    public void TheGroupsAreTheSquadronFactionThenItsSystemsThenElsewhere()
    {
        var state = State();

        var groups = BgsWorkPage.Groups(RowsOf(state), state.BgsWork, Faction);

        Assert.Equal(["For " + Faction, "Other factions in its systems", "Elsewhere"], groups.Select(g => g.Title));
        Assert.Equal([Faction], groups[0].Rows.Select(r => r.Faction));
        Assert.Equal(["Neighbour Union"], groups[1].Rows.Select(r => r.Faction));
        Assert.Equal(["Far Party"], groups[2].Rows.Select(r => r.Faction));
    }

    [Fact]
    public void AGroupWithNoRowsIsLeftOut()
    {
        var state = State();
        var rows = RowsOf(state).Where(r => r.Faction != "Neighbour Union").ToList();

        var groups = BgsWorkPage.Groups(rows, state.BgsWork, Faction);

        Assert.Equal(["For " + Faction, "Elsewhere"], groups.Select(g => g.Title));
    }

    [Fact]
    public void ASystemWithNoFactionListGoesToElsewhere()
    {
        var state = JournalScript.Commander("F1", "Fixture", Mission("2026-10-10T17:02:00Z", "Neighbour Union", 1));

        var groups = BgsWorkPage.Groups(RowsOf(state), state.BgsWork, Faction);

        Assert.Equal("Elsewhere", Assert.Single(groups).Title);
    }

    [AvaloniaFact]
    public void KnowingTheFactionDrawsOneTablePerGroupAndNoNotice()
    {
        var window = Open(State(), Faction);
        var page = (Control)window.Content!;

        Assert.Empty(In<Notice>(page));
        Assert.Equal(3, In<TextBlock>(page).Count(block => block.Text == "1 FACTION"));

        window.Close();
    }

    [AvaloniaFact]
    public void ASquadronWithNoKnownFactionKeepsOneTableAndSaysHowItIsLearned()
    {
        var window = Open(State(), null);
        var page = (Control)window.Content!;

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(BgsWorkPage.UnknownFactionText, notice.Text);
        Assert.Equal(1, In<TextBlock>(page).Count(block => block.Text == "3 FACTIONS"));

        window.Close();
    }

    [AvaloniaFact]
    public void OutsideASquadronThereIsNoNotice()
    {
        var window = Open(State(member: false), null);

        Assert.Empty(In<Notice>((Control)window.Content!));

        window.Close();
    }
}
