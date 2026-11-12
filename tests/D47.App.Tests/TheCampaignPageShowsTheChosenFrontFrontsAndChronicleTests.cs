using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>Commander › Campaign: the chosen or proposed front, the Fronts and Chronicle sections and the failed-fetch warning (#2262).</summary>
public class TheCampaignPageShowsTheChosenFrontFrontsAndChronicleTests
{
    private const string Frontier = "F2262";
    private const string Us = "Kestrel Reach Compact";
    private const string Rival = "Lave Purple Gang";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T18:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-11-09T14:12:00Z", CultureInfo.InvariantCulture);

    private sealed class Fixture
    {
        private readonly string? _failure;

        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; } = JournalScript.Commander(
            Frontier,
            "Kade Orrin",
            """{ "timestamp":"2026-11-09T17:30:00Z", "event":"FSDJump", "StarSystem":"Arque", "SystemAddress":129, "StarPos":[0,0,0], "Factions":[{ "Name":"Arque Purple Cartel", "Influence":0.57 },{ "Name":"Kestrel Reach Compact", "Influence":0.245 }] }""",
            """{ "timestamp":"2026-11-09T17:40:00Z", "event":"FSDJump", "StarSystem":"Haoria", "SystemAddress":130, "StarPos":[0,0,0], "Factions":[{ "Name":"Kestrel Reach Compact", "Influence":0.526, "ActiveStates":[{ "State":"War" }] },{ "Name":"Lave Purple Gang", "Influence":0.312, "ActiveStates":[{ "State":"War" }] }], "Conflicts":[{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"Kestrel Reach Compact", "Stake":"Haoria Hub", "WonDays":2 }, "Faction2":{ "Name":"Lave Purple Gang", "Stake":"", "WonDays":1 } }] }""");

        public FactionReport Report { get; }

        public CampaignFrontsSnapshot Snapshot { get; private set; } = null!;

        public CampaignFocus Focus { get; }

        public CapabilityRegistry Registry { get; }

        public List<ToolInvocation> Invoked { get; } = [];

        public Fixture(FactionReport? report = null, string? failure = null)
        {
            _failure = failure;
            Report = report ?? Sample();
            Store.SetIntent(Frontier, "Gliese 868", SystemIntent.StayUnder75);
            Recompute();

            Focus = new CampaignFocus(Store, () => Snapshot, () => State, () => BgsTick, () => Now);
            Registry = CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State, fronts: () => Snapshot, focus: Focus)]);
            Registry.ToolInvoked += Invoked.Add;
            Focus.Poll();
        }

        /// <summary>Computes the fronts from the campaign as the watch does after a tick.</summary>
        public void Recompute() => Snapshot = new CampaignFrontsSnapshot(
            Frontier,
            Us,
            Report,
            BgsTick.AddMinutes(5),
            CampaignFronts.Of(Report, State.Standings, Store.For(Frontier), Now),
            BgsTick.AddMinutes(6),
            _failure,
            _failure is null ? null : Now);

        public CampaignSources Sources => new(
            () => State,
            Store,
            () => Snapshot,
            () => Us,
            () => BgsTick,
            Registry)
        {
            Now = () => Now,
            Proposal = () => Focus.Proposal,
            NextAction = () => Focus.NextAction(),
        };
    }

    private static FactionSystem Place(string name, double influence, bool controls, string[]? active = null, string[]? pending = null, double rival = 0.10, int age = 1) => new()
    {
        Name = name,
        Influence = influence,
        Controls = controls,
        ActiveStates = active ?? [],
        PendingStates = pending ?? [],
        Others = [new FactionNeighbour(Rival, rival)],
        ReportedAt = Now.AddDays(-age),
        Population = 1_000_000,
    };

    private static FactionReport Sample() => new(Us, "Cooperative", "Independent",
    [
        Place("Gliese 868", 0.728, true, ["Boom"], rival: 0.147),
        Place("Kanati", 0.610, true, rival: 0.225, age: 41),
        Place("Haoria", 0.526, true, ["War", "Outbreak"], rival: 0.312),
        Place("Wolf 1301", 0.440, true, rival: 0.406),
        Place("Arque", 0.245, true, rival: 0.572),
        Place("LHS 1914", 0.041, false, pending: ["Retreat"], rival: 0.351),
    ], []);

    private static (Window Window, CampaignPage Page) Open(Fixture fixture, bool headset = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(fixture.Sources, headset: headset, copy: headset ? null : _ => Task.FromResult(true));
        var window = new Window
        {
            Content = page,
            Width = headset ? AppLook.CaptureWidth : 1280,
            Height = headset ? AppLook.CaptureHeight : 1300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static Button Tile(Control root, string name) => In<Button>(root).Single(button => button.Name == name);

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void ShowSection(Control root, int section)
    {
        In<Segment>(root).Single(segment => segment.Name == CampaignPage.SectionsName).PressOption(section);
        Dispatcher.UIThread.RunJobs();
    }

    private static ChronicleEntry Entry(string kind, string system, string outcome, params DateTimeOffset[] ticks) =>
        new(kind, system, "Arque Purple Cartel", outcome, ticks, [new ChronicleWork(BgsWorkKind.Mission, 7), new ChronicleWork(BgsWorkKind.Bounty, 1_200_000)]);

    [AvaloniaFact]
    public void WithNoFrontChosenTheTopFrontIsProposedWithTakeItAndNextFront()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);

        var proposed = Assert.IsType<StackPanel>(In<StackPanel>(page).Single(panel => panel.Name == CampaignPage.ProposedName));
        Assert.True(Shows(proposed, "PROPOSED FRONT"));
        Assert.True(Shows(proposed, fixture.Focus.Proposal!.Front.System));
        Assert.True(Shows(proposed, CampaignModel.ProposalNote));
        Assert.Contains(In<TextBlock>(proposed), block => block.Text?.StartsWith("Rank 1 of ", StringComparison.Ordinal) == true);
        Assert.Equal("TAKE IT", Tile(page, CampaignPage.TakeItName).Content);
        Assert.Equal("NEXT FRONT", Tile(page, CampaignPage.NextFrontName).Content);
        Assert.DoesNotContain(In<StackPanel>(page), panel => panel.Name == CampaignPage.ChosenName);

        window.Close();
    }

    [AvaloniaFact]
    public void TakeItChoosesTheProposedFrontAsTheCommander()
    {
        var fixture = new Fixture();
        var proposed = fixture.Focus.Proposal!.Front;
        var (window, page) = Open(fixture);

        Press(Tile(page, CampaignPage.TakeItName));

        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.TakeFrontTool, ToolCaller.Commander, true), (call.Tool, call.Caller, call.Succeeded));
        Assert.Equal((proposed.Kind, proposed.System), (fixture.Store.For(Frontier).Chosen!.Kind, fixture.Store.For(Frontier).Chosen!.System));
        Assert.Contains(In<StackPanel>(page), panel => panel.Name == CampaignPage.ChosenName);
        Assert.DoesNotContain(In<StackPanel>(page), panel => panel.Name == CampaignPage.ProposedName);
        Assert.Equal("DROP THE FRONT", Tile(page, CampaignPage.DropFrontName).Content);

        window.Close();
    }

    [AvaloniaFact]
    public void NextFrontProposesTheNextFrontAsTheCommander()
    {
        var fixture = new Fixture();
        var first = fixture.Focus.Proposal!.Front;
        var (window, page) = Open(fixture);

        Press(Tile(page, CampaignPage.NextFrontName));

        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.NextFrontTool, ToolCaller.Commander), (call.Tool, call.Caller));
        var second = fixture.Focus.Proposal!.Front;
        Assert.NotEqual((first.Kind, first.System), (second.Kind, second.System));
        var proposed = In<StackPanel>(page).Single(panel => panel.Name == CampaignPage.ProposedName);
        Assert.True(Shows(proposed, second.System));
        Assert.Contains(In<TextBlock>(proposed), block => block.Text?.StartsWith("Rank 2 of ", StringComparison.Ordinal) == true);

        window.Close();
    }

    [AvaloniaFact]
    public void TheChosenFrontShowsItsNextActionAndDropsAsTheCommander()
    {
        var fixture = new Fixture();
        fixture.Focus.Accept();
        var chosen = fixture.Store.For(Frontier).Chosen!;
        var (window, page) = Open(fixture);

        var block = In<StackPanel>(page).Single(panel => panel.Name == CampaignPage.ChosenName);
        Assert.True(Shows(block, "CHOSEN FRONT"));
        Assert.True(Shows(block, chosen.System));
        Assert.True(Shows(block, "0 ticks"));
        Assert.True(Shows(block, "NEXT"));
        Assert.True(Shows(block, fixture.Focus.NextAction()!.Sentence));
        Assert.True(Shows(block, CampaignModel.ChosenPhrases));

        Press(Tile(page, CampaignPage.DropFrontName));

        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.DropFrontTool, ToolCaller.Commander, true), (call.Tool, call.Caller, call.Succeeded));
        Assert.Null(fixture.Store.For(Frontier).Chosen);
        Assert.DoesNotContain(In<StackPanel>(page), panel => panel.Name == CampaignPage.ChosenName);

        window.Close();
    }

    [AvaloniaFact]
    public void ARowsTakeTileChoosesThatFrontAndTheRowSaysChosen()
    {
        var fixture = new Fixture();
        var front = fixture.Snapshot.Fronts.Last();
        var (window, page) = Open(fixture);

        Assert.Equal(fixture.Snapshot.Fronts.Count, In<Button>(page).Count(button => button.Name?.StartsWith("CampaignTake.", StringComparison.Ordinal) == true));

        Press(Tile(page, CampaignPage.TakeName(front)));

        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.TakeFrontTool, ToolCaller.Commander, true), (call.Tool, call.Caller, call.Succeeded));
        Assert.Equal((front.Kind, front.System), (fixture.Store.For(Frontier).Chosen!.Kind, fixture.Store.For(Frontier).Chosen!.System));
        Assert.True(Shows(page, "CHOSEN"));
        Assert.DoesNotContain(In<Button>(page), button => button.Name == CampaignPage.TakeName(front));
        Assert.Equal(fixture.Snapshot.Fronts.Count - 1, In<Button>(page).Count(button => button.Name?.StartsWith("CampaignTake.", StringComparison.Ordinal) == true));

        window.Close();
    }

    [AvaloniaFact]
    public void TheFrontsAreCountedOnTheSegmentedAndAnOldReportIsMarked()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);

        var fronts = fixture.Snapshot.Fronts;
        Assert.Contains(fronts, front => front.Kind == FrontKind.Stale);
        Assert.Equal(
            [$"{fronts.Count} fronts", "6 systems", "0 resolved"],
            In<Segment>(page).Single(segment => segment.Name == CampaignPage.SectionsName).Statuses.Select(status => status!.Text));
        Assert.Contains(In<TextBlock>(page), block => block.Text == CampaignModel.OldLabel);
        Assert.True(Shows(page, "41 days ago"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetSegmentedHasNoCounts()
    {
        var (window, page) = Open(new Fixture(), headset: true);

        Assert.Empty(In<Segment>(page).Single(segment => segment.Name == CampaignPage.SectionsName).Statuses);

        window.Close();
    }

    [AvaloniaFact]
    public void TheChronicleListsResolvedFrontsNewestFirstWithoutClaimingTheOutcome()
    {
        var fixture = new Fixture();
        fixture.Store.Choose(Frontier, FrontKind.Conflict, "Haoria", BgsTick.AddDays(-20));
        fixture.Store.Resolve(Frontier, FrontKind.Conflict, "Haoria", Entry("Conflict", "Haoria", "Won", BgsTick.AddDays(-19), BgsTick.AddDays(-18)));
        fixture.Store.Choose(Frontier, FrontKind.Coup, "Arque", BgsTick.AddDays(-5));
        fixture.Store.Resolve(Frontier, FrontKind.Coup, "Arque", Entry("Coup threat", "Arque", "Resolved", BgsTick.AddDays(-4)));
        var (window, page) = Open(fixture);

        ShowSection(page, CampaignModel.ChronicleSection);

        var ended = In<TextBlock>(page).Select(block => block.Text).Where(text => text is "5 Nov" or "22 Oct").ToList();
        Assert.Equal(["5 Nov", "22 Oct"], ended);
        Assert.True(Shows(page, "Won"));
        Assert.True(Shows(page, "+7 marks"));
        Assert.True(Shows(page, "1,200,000 CR in bounties"));
        Assert.True(Shows(page, CampaignModel.ChronicleHint));

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedFetchWarnsAtTheTopAndMarksTheComputedTimeInWarn()
    {
        using var look = AppLook.Put();
        var fixture = new Fixture(failure: "HTTP 503");
        var (window, page) = Open(fixture);

        var notice = In<Notice>(page).Single(each => each.Name == CampaignPage.NoticeName);
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.Equal(CampaignModel.FailedLabel, notice.Label);
        Assert.Equal("HTTP 503", notice.Detail);
        Assert.Contains("These fronts were computed after the 14:12 UTC tick", notice.Text, StringComparison.Ordinal);

        var computed = In<TextBlock>(page).Single(block => block.Text == "After the 14:12 UTC tick");
        var warn = ((ISolidColorBrush)Application.Current!.FindResource(ThemeManager.WarnKey)!).Color;
        Assert.Equal(warn, ((ISolidColorBrush)computed.Foreground!).Color);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoFrontAtAllTheBlockIsLeftOut()
    {
        var calm = new FactionReport(Us, "Cooperative", "Independent", [Place("Wolf 1301", 0.60, true, rival: 0.10)], []);
        var fixture = new Fixture(calm);
        var (window, page) = Open(fixture);

        Assert.Empty(fixture.Snapshot.Fronts);
        Assert.DoesNotContain(In<StackPanel>(page), panel => panel.Name is CampaignPage.ChosenName or CampaignPage.ProposedName);
        Assert.True(Shows(page, CampaignModel.NoFrontsText));

        window.Close();
    }

    [AvaloniaFact]
    public void FrontsChangingDoNotReplaceTheStepperOnTheSystemsSection()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);
        ShowSection(page, CampaignModel.SystemsSection);

        var stepper = In<Stepper>(page).First();
        Press(Tile(page, CampaignPage.NextFrontName));

        Assert.Same(stepper, In<Stepper>(page).First());
        Assert.True(Shows(In<StackPanel>(page).Single(panel => panel.Name == CampaignPage.ProposedName), fixture.Focus.Proposal!.Front.System));

        window.Close();
    }

    [Fact]
    public void ThePhrasesTheTilesNameAreOnesTheCampaignToolsTake()
    {
        var fixture = new Fixture();
        var front = fixture.Snapshot.Fronts[0];
        var phrases = CampaignCapability.Phrases(fixture.Store, () => fixture.State, focus: fixture.Focus, fronts: () => fixture.Snapshot).Select(command => command.Phrase).ToList();
        var spoken = CampaignCapability.Create(fixture.Store, () => fixture.State).Tools.SelectMany(tool => tool.Commands).Select(command => command.Phrase).ToList();

        Assert.Contains(CampaignModel.TakePhrase(front), phrases);
        Assert.Contains("take it", phrases);
        Assert.Contains("next front", spoken);
        Assert.Contains("drop the front", spoken);
    }

    [Fact]
    public void AChronicleEntryEndsAtItsLastTickAndSaysWhatTheCommanderDid()
    {
        var entry = Entry("War", "Haoria", "Won", BgsTick.AddDays(-2), BgsTick);
        var campaign = new Campaign { Chronicle = [entry, Entry("Coup threat", "Arque", "Resolved")] };

        var rows = CampaignModel.ChronicleRows(campaign);

        Assert.Equal(["Arque", "Haoria"], rows.Select(row => row.Entry.System));
        Assert.Equal("—", CampaignModel.EndedText(rows[0].Ended));
        Assert.Equal("9 Nov", CampaignModel.EndedText(rows[1].Ended));
        Assert.Equal(["+7 marks", "1,200,000 CR in bounties"], CampaignModel.WorkLines(entry));
        Assert.Equal(["none"], CampaignModel.WorkLines(entry with { Work = [] }));
    }

    [AvaloniaFact]
    public void AVisitedWarShowsItsDaysWonAndWhatDoesNotCountThere()
    {
        var fixture = new Fixture();
        var war = fixture.Snapshot.Fronts[0];
        Assert.Equal(FrontKind.Conflict, war.Kind);
        fixture.Focus.Take(FrontKind.Conflict, "Haoria");
        var (window, page) = Open(fixture);

        var block = In<StackPanel>(page).Single(panel => panel.Name == CampaignPage.ChosenName);
        Assert.True(Shows(block, "2 – 1"));
        Assert.Contains(In<TextBlock>(block), text => text.Text?.StartsWith("days won · you saw it ", StringComparison.Ordinal) == true);
        Assert.True(Shows(block, fixture.Focus.NextAction()!.NotCounting!));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetFrontIsOneSlabWithTheTwoTilesStackedBesideIt()
    {
        var fixture = new Fixture();
        fixture.Focus.Take(FrontKind.Conflict, "Haoria");
        var (window, page) = Open(fixture, headset: true);

        var block = In<Grid>(page).Single(grid => grid.Name == CampaignPage.ChosenName);
        Assert.True(Shows(block, "CHOSEN FRONT · CONFLICT"));
        Assert.True(Shows(block, "2 – 1"));
        Assert.True(Shows(block, fixture.Focus.NextAction()!.NotCounting!));
        Assert.Equal(["NEXT FRONT", "DROP THE FRONT"], In<Button>(block).Select(button => button.Content));
        Assert.False(Shows(page, CampaignModel.ChosenPhrases));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSampleCampaignIsCaptured()
    {
        if (!TestSurface.CapturesWanted)
        {
            return;
        }

        using var look = AppLook.Put();

        void Capture(Window window, string name)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture(name);
        }

        var proposed = new Fixture(failure: "HTTP 503");
        var (first, _) = Open(proposed);
        Capture(first, "campaign-proposed-failed.png");
        first.Close();

        var chosen = new Fixture();
        chosen.Store.Choose(Frontier, FrontKind.Conflict, "Haoria", BgsTick.AddDays(-20));
        chosen.Store.Resolve(Frontier, FrontKind.Conflict, "Haoria", Entry("Conflict", "Haoria", "Won", BgsTick.AddDays(-19), BgsTick.AddDays(-18)));
        chosen.Focus.Take(FrontKind.Conflict, "Haoria");
        chosen.Focus.Poll();
        var (second, page) = Open(chosen);
        Capture(second, "campaign-chosen-fronts.png");
        ShowSection(page, CampaignModel.ChronicleSection);
        Capture(second, "campaign-chronicle.png");
        second.Close();

        var (third, _) = Open(chosen, headset: true);
        Capture(third, "campaign-fronts-headset.png");
        third.Close();
    }

    [AvaloniaFact]
    public void TheFrontsTableFillsAWideWindowWhileTheFrameStaysAtNineHundred()
    {
        var fixture = new Fixture();
        var (window, page) = Open(fixture);
        window.Width = 1600;
        Dispatcher.UIThread.RunJobs();

        var take = In<Button>(page).First(button => button.Name?.StartsWith("CampaignTake.", StringComparison.Ordinal) == true);
        var tableRight = take.TranslatePoint(new Point(take.Bounds.Width, 0), page)!.Value.X;
        Assert.True(tableRight > 900, $"the fronts table ends at {tableRight}");

        var change = Tile(page, CampaignPage.ChangeName);
        var frameRight = change.TranslatePoint(new Point(change.Bounds.Width, 0), page)!.Value.X;
        Assert.True(frameRight <= 900, $"the frame ends at {frameRight}");

        window.Close();
    }
}
