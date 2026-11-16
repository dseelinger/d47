using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › BGS shows the work since the tick on Systems for the BGS faction and on WORK for the rest (#2463).</summary>
public class TheBgsPageShowsTheWorkForEachFactionTests
{
    private const string Frontier = "F2463";
    private const string Faction = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T16:14:00Z", CultureInfo.InvariantCulture);

    private static string Mission(string time, string faction, long system, string trend = "UpGood", string marks = "+") =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
        + "\",\"Influence\":[{\"SystemAddress\":" + system + ",\"Trend\":\"" + trend + "\",\"Influence\":\"" + marks + "\"}]}]}";

    private static readonly string[] Lines =
    [
        """{"timestamp":"2026-10-10T16:01:00Z","event":"FSDJump","StarSystem":"Home","SystemAddress":1,"Factions":[{"Name":"Neighbour Union"},{"Name":"Kestrel Reach Compact"}]}""",
        """{"timestamp":"2026-10-10T16:02:00Z","event":"FSDJump","StarSystem":"Outpost","SystemAddress":3,"Factions":[{"Name":"Kestrel Reach Compact"}]}""",
        Mission("2026-10-10T17:00:00Z", "Far Party", 9),
        Mission("2026-10-10T17:01:00Z", "Far Party", 9),
        Mission("2026-10-10T17:02:00Z", "Neighbour Union", 1),
        Mission("2026-10-10T17:03:00Z", "Kestrel Reach Compact", 1),
        Mission("2026-10-10T17:04:00Z", "Kestrel Reach Compact", 3),
        Mission("2026-10-10T17:05:00Z", "Kestrel Reach Compact", 3, "DownBad", "++"),
    ];

    private sealed class Fixture
    {
        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; set; } = JournalScript.Commander(Frontier, "Fixture", Lines);

        public bool WithReport { get; set; } = true;

        public DateTimeOffset? Tick { get; set; } = TheBgsPageShowsTheWorkForEachFactionTests.Tick;

        private CampaignFrontsSnapshot? Snapshot => WithReport
            ? new CampaignFrontsSnapshot(
                Frontier,
                Faction,
                new FactionReport(Faction, null, null, [Place("Home"), Place("Outpost")], []),
                Now,
                [],
                Now,
                null,
                null)
            : null;

        public CampaignSources Sources => new(
            () => State,
            Store,
            () => Snapshot,
            () => Faction,
            () => Tick,
            CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State)]))
        {
            Now = () => TheBgsPageShowsTheWorkForEachFactionTests.Now,
        };

        private static FactionSystem Place(string name) => new()
        {
            Name = name,
            Influence = 0.4,
            Controls = true,
            ReportedAt = Now,
        };
    }

    private static (Window Window, CampaignPage Page) Open(Fixture fixture, int section, bool headset = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(fixture.Sources, headset: headset);
        var window = new Window
        {
            Content = page,
            Width = headset ? 1024 : 1280,
            Height = 1300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Show(page, section);

        return (window, page);
    }

    private static void Show(Control root, int section)
    {
        In<Segment>(root).FirstOrDefault(segment => segment.Name == CampaignPage.SectionsName)?.PressOption(section);
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) =>
        In<TextBlock>(root).Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TheSegmentIsFrontsSystemsWorkChronicle() =>
        Assert.Equal(["FRONTS", "SYSTEMS", "WORK", "CHRONICLE"], CampaignModel.Sections);

    [AvaloniaFact]
    public void WorkGroupsAroundTheBgsFactionAndLeavesOutWhatSystemsShows()
    {
        var (window, page) = Open(new Fixture(), CampaignModel.WorkSection);

        Assert.True(Shows(page, "Other factions in its systems"));
        Assert.True(Shows(page, "Elsewhere"));
        Assert.False(Shows(page, "For " + Faction));
        Assert.True(Shows(page, "Neighbour Union"));
        Assert.True(Shows(page, "Far Party"));
        Assert.False(Shows(page, "Outpost"));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoReportAllTheBgsFactionsWorkIsUnderForItsName()
    {
        var (window, page) = Open(new Fixture { WithReport = false }, CampaignModel.WorkSection);

        Assert.True(Shows(page, "For " + Faction));
        Assert.True(Shows(page, "Other factions in its systems"));
        Assert.True(Shows(page, "Elsewhere"));
        Assert.True(Shows(page, "Outpost"));

        window.Close();
    }

    [AvaloniaFact]
    public void SystemsShowsTheBgsFactionsWorkSinceTheTickAndNoneWhereThereIsNone()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture, CampaignModel.SystemsSection);

        Assert.True(Shows(page, "Since the tick"));
        Assert.True(Shows(page, "+1 mark"));
        Assert.True(Shows(page, "−1 mark"));
        Assert.Contains(In<Control>(page), control => control.Name == CampaignPage.HeadName + ".Yourwork");

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoTickTimeSystemsAndWorkNameTheLast24Hours()
    {
        var fixture = new Fixture { Tick = null };
        var (window, page) = Open(fixture, CampaignModel.SystemsSection);

        Assert.True(Shows(page, "Last 24 hours"));
        Assert.False(Shows(page, "Since the tick"));

        Show(page, CampaignModel.WorkSection);

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(BgsWorkTable.UnknownTickLabel, notice.Label);
        Assert.DoesNotContain(In<Button>(page), button => button.Name == "BgsWorkHead.Influence");

        window.Close();
    }

    [AvaloniaFact]
    public void TheSearchMatchesSystemOrFactionOnWork()
    {
        var (window, page) = Open(new Fixture(), CampaignModel.WorkSection);

        Assert.Equal("Search system or faction", page.FilterPlaceholder);
        Assert.True(page.Filters);

        page.Filter("far party");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Far Party"));
        Assert.False(Shows(page, "Neighbour Union"));

        page.Filter("nowhere");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, BgsWorkTable.NoMatchText(Tick)));

        window.Close();
    }

    [AvaloniaFact]
    public void WorkAlonePutsTheSearchFieldUpWhenThereIsNoReportAndNoChronicle()
    {
        var fixture = new Fixture { WithReport = false };
        var (window, page) = Open(fixture, CampaignModel.WorkSection);

        Assert.True(page.Filters);

        fixture.State = JournalScript.Commander(Frontier, "Fixture");
        _ = page.Tick();

        Assert.False(page.Filters);

        window.Close();
    }

    [AvaloniaFact]
    public void WorkHandedInWhileTheSectionIsOpenRedrawsIt()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture, CampaignModel.WorkSection);

        Assert.False(Shows(page, "Late Party"));

        fixture.State = JournalScript.Commander(Frontier, "Fixture", [.. Lines, Mission("2026-10-10T18:00:00Z", "Late Party", 9)]);

        Assert.True(page.Tick());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "Late Party"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheCountUnderWorkCountsItsRows()
    {
        var (window, page) = Open(new Fixture(), CampaignModel.WorkSection);

        var segment = In<Segment>(page).First(each => each.Name == CampaignPage.SectionsName);

        Assert.Equal("2 factions", segment.Statuses![CampaignModel.WorkSection]!.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBgsFactionsWorkIsHiddenWithTheRestWhileBgsIsOff()
    {
        var fixture = new Fixture();
        fixture.Store.SetOff(Frontier, true);
        var (window, page) = Open(fixture, CampaignModel.WorkSection);

        Assert.DoesNotContain(In<Segment>(page), segment => segment.Name == CampaignPage.SectionsName);
        Assert.False(Shows(page, "Far Party"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPutsTheWorkUnderTheSystemNameAndNamesThePeriodOnce()
    {
        var (window, page) = Open(new Fixture(), CampaignModel.SystemsSection, headset: true);

        Assert.True(Shows(page, "Under each system: your work for " + Faction + " since the 16:14 UTC tick."));
        Assert.True(Shows(page, "+1 mark"));
        Assert.DoesNotContain(In<TextBlock>(page), block => block.Text == "none");
        Assert.DoesNotContain(In<Button>(page), button => button.Name?.EndsWith("Yourwork", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAWorkHeadingMarksIt()
    {
        var (window, page) = Open(new Fixture(), CampaignModel.WorkSection);

        In<Button>(page).First(button => button.Name == "BgsWorkHead.Missions").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "MISSIONS ▼"));

        window.Close();
    }
}
