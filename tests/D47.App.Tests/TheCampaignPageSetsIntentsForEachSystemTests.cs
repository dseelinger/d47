using System.Globalization;
using Avalonia;
using Avalonia.Automation;
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

/// <summary>Commander › Campaign: the frame, the Systems table with an intent Stepper per row, the faction picker and the headset (#2275).</summary>
public class TheCampaignPageSetsIntentsForEachSystemTests
{
    private const string Frontier = "F2275";
    private const string Us = "Kestrel Reach Compact";
    private const string Rival = "Lave Purple Gang";
    private const string Unknown = "Nowhere Syndicate";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T18:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset BgsTick = DateTimeOffset.Parse("2026-11-09T14:12:00Z", CultureInfo.InvariantCulture);

    private sealed class Fixture
    {
        public CampaignStore Store { get; } = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

        public CommanderGameState State { get; } = JournalScript.Commander(
            Frontier,
            "Kade Orrin",
            """{ "timestamp":"2026-11-09T17:00:00Z", "event":"FSDJump", "StarSystem":"Ross 128", "SystemAddress":128, "StarPos":[0,0,0], "Factions":[{ "Name":"Kestrel Reach Compact", "Influence":0.38, "ActiveStates":[{ "State":"War" }] },{ "Name":"Ross 128 Gold Life Inc", "Influence":0.376, "ActiveStates":[{ "State":"War" }] }] }""",
            """{ "timestamp":"2026-11-09T17:30:00Z", "event":"FSDJump", "StarSystem":"Arque", "SystemAddress":129, "StarPos":[0,0,0], "Factions":[{ "Name":"Arque Purple Cartel", "Influence":0.57 },{ "Name":"Kestrel Reach Compact", "Influence":0.245 }] }""");

        private CampaignFrontsSnapshot? _snapshot;

        public FactionReport? Report
        {
            get => _snapshot?.Report;
            set => _snapshot = value is null ? null : new CampaignFrontsSnapshot(Frontier, Us, value, BgsTick.AddMinutes(5), [], BgsTick.AddMinutes(6), null, null);
        }

        /// <summary>Recomputes the fronts as the watch does on the tick after a campaign change: a new snapshot, the same report.</summary>
        public void Recompute() => _snapshot = _snapshot! with { Fronts = [], ComputedAt = Now };

        public string? Squadron { get; set; } = Us;

        public DateTimeOffset Clock { get; set; } = Now;

        public List<ToolInvocation> Invoked { get; } = [];

        public CapabilityRegistry Registry { get; }

        public Fixture()
        {
            Registry = CapabilityRegistry.Build([CampaignCapability.Create(Store, () => State, galaxy: new Galaxy())]);
            Registry.ToolInvoked += Invoked.Add;
        }

        public CampaignSources Sources => new(
            () => State,
            Store,
            () => _snapshot,
            () => Squadron,
            () => BgsTick,
            Registry)
        {
            Now = () => Clock,
        };
    }

    private sealed class Galaxy : StubGalaxyService
    {
        public override Task<FactionReport> FactionAsync(
            string faction, CancellationToken cancellationToken, bool refresh = false, IProgress<FactionProgress>? progress = null) =>
            Task.FromResult(new FactionReport(
                faction,
                null,
                null,
                faction == Unknown ? [] : [new FactionSystem { Name = "Lave", Influence = 0.4, Controls = true }],
                []));
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
    };

    /// <summary>A twelve-system campaign like the handoff's.</summary>
    private static FactionReport Sample() => new(Us, "Cooperative", "Independent",
    [
        Place("Lalande 29917", 0.680, true, ["Boom"], rival: 0.188),
        Place("Gliese 868", 0.728, true, ["Boom"], rival: 0.147),
        Place("Kanati", 0.610, true, rival: 0.225, age: 41),
        Place("Haoria", 0.526, true, ["Outbreak"], rival: 0.312),
        Place("Wolf 1301", 0.440, true, rival: 0.406),
        Place("Ross 128", 0.380, true, ["War"], rival: 0.376),
        Place("HIP 21991", 0.310, false, rival: 0.352),
        Place("Arque", 0.245, true, rival: 0.572),
        Place("Mullag", 0.185, false, rival: 0.305),
        Place("Ngolo", 0.092, false, rival: 0.495),
        Place("Tau Bootis", 0.064, false, rival: 0.574),
        Place("LHS 1914", 0.041, false, pending: ["Retreat"], rival: 0.351),
    ], []);

    private static Fixture SampleCampaign()
    {
        var fixture = new Fixture { Report = Sample() };

        fixture.Store.SetIntent(Frontier, "Lalande 29917", SystemIntent.ExpandFromHere);
        fixture.Store.SetIntent(Frontier, "Gliese 868", SystemIntent.StayUnder75);
        fixture.Store.SetIntent(Frontier, "Haoria", SystemIntent.Hold);
        fixture.Store.SetIntent(Frontier, "Ross 128", SystemIntent.Hold);
        fixture.Store.SetIntent(Frontier, "HIP 21991", SystemIntent.TakeControl);
        fixture.Store.SetIntent(Frontier, "Arque", SystemIntent.Hold);
        fixture.Store.SetIntent(Frontier, "Mullag", SystemIntent.Grow);
        fixture.Store.SetIntent(Frontier, "LHS 1914", SystemIntent.Grow);

        return fixture;
    }

    private static (Window Window, CampaignPage Page) Open(Fixture fixture, bool headset = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new CampaignPage(fixture.Sources, headset: headset);
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 1300,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static (Window Window, PanelView View) OpenPanel(Fixture fixture, bool headset, double width, double height)
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableCopy(new RecordingClipboard());

        if (headset)
        {
            view.Classes.Add("headset");
            new MiniPanelServices
            {
                GameState = () => fixture.State,
                HistoryState = () => HistoryState.Done,
                Campaign = fixture.Sources,
            }.Furnish(view);
        }
        else
        {
            view.EnableChecklist(TestSurface.Checklists(TestSurface.MemoryFolder("d47-campaign-page-tests")));
            view.EnableMissions(() => fixture.State, () => Now);
            view.EnableStanding(() => fixture.State);
            view.EnableStatistics(() => fixture.State);
            view.EnableCampaign(fixture.Sources);
            view.EnableBgsWork(() => fixture.State, () => BgsTick, () => Now);
            view.EnableCommanders(new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }));
        }

        var window = new Window
        {
            Content = view,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, CampaignPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static bool ShowsRun(Control root, string text) =>
        In<TextBlock>(root).Any(block => block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == text) == true);

    private static Stepper StepperOf(Control root, string system) =>
        In<Stepper>(root).Single(stepper => AutomationProperties.GetName(stepper) == CampaignPage.StepperName(system));

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Step(Stepper stepper, string arrow) =>
        Press(In<RepeatButton>(stepper).Single(button => AutomationProperties.GetName(button) == arrow));

    [Fact]
    public void TheLabelsMarkOnlyADefaultIntent()
    {
        Assert.Equal(
            ["Hold · default", "Grow", "Expand from here", "Stay under 75%", "Take control", "Leave alone"],
            CampaignModel.Labels(SystemIntent.Hold, isDefault: true));
        Assert.Equal("Leave alone · default", CampaignModel.Labels(SystemIntent.LeaveAlone, isDefault: true)[5]);
        Assert.DoesNotContain(CampaignModel.Labels(SystemIntent.Grow, isDefault: false), label => label.Contains("default", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.492, "+49.2")]
    [InlineData(-0.042, "−4.2")]
    [InlineData(0.004, "+0.4")]
    [InlineData(null, "—")]
    public void TheLeadIsInPointsWithItsSign(double? lead, string text) =>
        Assert.Equal(text, CampaignModel.LeadText(lead));

    [Fact]
    public void TheFrontsAreComputedAfterTheTickWhenTheReportIsNewerThanIt()
    {
        var snapshot = new CampaignFrontsSnapshot(Frontier, Us, Sample(), BgsTick.AddMinutes(5), [], BgsTick.AddMinutes(6), null, null);

        Assert.Equal("After the 14:12 UTC tick", CampaignModel.ComputedText(snapshot, BgsTick));
        Assert.Equal("14:18 UTC", CampaignModel.ComputedText(snapshot, BgsTick.AddHours(1)));
        Assert.Equal("Not yet", CampaignModel.ComputedText(null, BgsTick));
    }

    [Fact]
    public void ThePickerListsTheSquadronsFactionFirstThenTheFactionsSeen()
    {
        var choices = CampaignModel.FactionChoices(Us, Us, new Fixture().State.Standings);

        Assert.Equal([Us, "Arque Purple Cartel", "Ross 128 Gold Life Inc"], choices.Select(choice => choice.Name));
        Assert.Equal(["Your squadron's faction", "Seen in Arque", "Seen in Ross 128"], choices.Select(choice => choice.Detail));
    }

    [AvaloniaFact]
    public void WithNoCampaignFactionThePageSaysHowToChooseOne()
    {
        var fixture = new Fixture { Squadron = null };
        var (window, page) = Open(fixture);

        Assert.True(Shows(page, CampaignModel.NoFactionText));
        Assert.Equal("CHOOSE FACTION", In<Button>(page).Single(button => button.Name == CampaignPage.ChangeName).Content);
        Assert.Empty(In<Stepper>(page));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoReportYetThePageSaysItIsFetchedAfterTheTick()
    {
        var (window, page) = Open(new Fixture());

        Assert.True(Shows(page, CampaignModel.NoReportText));
        Assert.Equal("CHANGE FACTION", In<Button>(page).Single(button => button.Name == CampaignPage.ChangeName).Content);
        Assert.Equal($"Your campaign for {Us}: what you mean to do in each of its systems.", page.Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void EachSystemHasAStepperAndADefaultIntentIsGrey()
    {
        var (window, page) = Open(SampleCampaign());

        Assert.True(Shows(page, "12 · 7 controlled"));
        Assert.True(Shows(page, "After the 14:12 UTC tick"));
        Assert.Equal(12, In<Stepper>(page).Count());

        var wolf = StepperOf(page, "Wolf 1301");
        Assert.Equal("Hold · default", wolf.SelectedItem);
        Assert.True(wolf.IsInherited);

        var ngolo = StepperOf(page, "Ngolo");
        Assert.Equal("Leave alone · default", ngolo.SelectedItem);
        Assert.True(ngolo.IsInherited);

        var lalande = StepperOf(page, "Lalande 29917");
        Assert.Equal("Expand from here", lalande.SelectedItem);
        Assert.False(lalande.IsInherited);

        Assert.True(Shows(page, "frozen"));
        Assert.True(Shows(page, "Retreat pending"));
        Assert.True(Shows(page, CampaignModel.Legend));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRowsComeMostInfluenceFirstAndTheLeadSorts()
    {
        var (window, page) = Open(SampleCampaign());

        string[] Order() => [.. In<Stepper>(page).Select(stepper => AutomationProperties.GetName(stepper)!.Replace("Intent ", string.Empty, StringComparison.Ordinal))];

        Assert.Equal("Gliese 868", Order()[0]);
        Assert.Equal("LHS 1914", Order()[^1]);

        Press(In<Button>(page).Single(button => button.Name == "CampaignHead.Lead"));

        Assert.Equal("Gliese 868", Order()[0]);
        Assert.Equal("Tau Bootis", Order()[^1]);

        window.Close();
    }

    [AvaloniaFact]
    public void AnOldReportSaysSoAndGreysTheRow()
    {
        var (window, page) = Open(SampleCampaign());

        Assert.True(ShowsRun(page, "41 days ago"));
        Assert.True(ShowsRun(page, "OLD REPORT"));

        window.Close();
    }

    [AvaloniaFact]
    public void AStepSetsTheIntentAsTheCommanderAndKeepsTheStepper()
    {
        var fixture = SampleCampaign();
        var (window, page) = Open(fixture);

        var wolf = StepperOf(page, "Wolf 1301");
        Step(wolf, "Next");

        Assert.Equal(SystemIntent.Grow, fixture.Store.For(Frontier).Intents["Wolf 1301"]);
        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.SetIntentTool, ToolCaller.Commander, true), (call.Tool, call.Caller, call.Succeeded));
        Assert.Equal("Grow", wolf.SelectedItem);
        Assert.False(wolf.IsInherited);

        Assert.False(page.Tick());
        Assert.Same(wolf, StepperOf(page, "Wolf 1301"));

        window.Close();
    }

    [AvaloniaFact]
    public void FrontsRecomputedAfterAStepDoNotReplaceTheStepper()
    {
        var fixture = SampleCampaign();
        var (window, page) = Open(fixture);

        var wolf = StepperOf(page, "Wolf 1301");
        Step(wolf, "Next");
        fixture.Recompute();

        Assert.False(page.Tick());
        Assert.Same(wolf, StepperOf(page, "Wolf 1301"));

        window.Close();
    }

    [AvaloniaFact]
    public void ANewMinuteDoesNotReplaceTheStepper()
    {
        var fixture = SampleCampaign();
        var (window, page) = Open(fixture);

        var wolf = StepperOf(page, "Wolf 1301");
        fixture.Clock = Now.AddMinutes(1);

        Assert.False(page.Tick());
        Assert.Same(wolf, StepperOf(page, "Wolf 1301"));

        window.Close();
    }

    [AvaloniaFact]
    public void AFactionGalaxySearchDoesNotKnowIsRefusedOnThePageShowing()
    {
        using var look = AppLook.Put();
        var fixture = SampleCampaign();
        var (window, view) = OpenPanel(fixture, headset: false, 1280, 900);

        Press(In<Button>(view).Single(button => button.Name == CampaignPage.ChangeName));
        var picker = Assert.Single(In<PickerPage>(view));
        picker.FindControl<TextBox>("FilterBox")!.Text = Unknown;
        Dispatcher.UIThread.RunJobs();
        Press(picker.FindControl<Button>("AcceptButton")!);
        view.TickCommander();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Campaign"], view.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Null(fixture.Store.For(Frontier).Faction);
        var message = In<TextBlock>(view).Single(block => block.Name == CampaignPage.MessageName);
        Assert.Equal($"I found no minor faction called {Unknown}, so I have not changed the campaign.", message.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AnIntentSetByVoiceRedrawsThePage()
    {
        var fixture = SampleCampaign();
        var (window, page) = Open(fixture);

        fixture.Store.SetIntent(Frontier, "Ngolo", SystemIntent.Grow);

        Assert.True(page.Tick());
        Assert.Equal("Grow", StepperOf(page, "Ngolo").SelectedItem);

        window.Close();
    }

    [Fact]
    public async Task TheModelCannotSetTheIntentTheStepperSets()
    {
        var fixture = SampleCampaign();

        var refused = await fixture.Registry.InvokeAsync(
            CampaignCapability.SetIntentTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Ngolo", ["intent"] = "Grow" }),
            CancellationToken.None,
            ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.False(fixture.Store.For(Frontier).Intents.ContainsKey("Ngolo"));
    }

    [AvaloniaFact]
    public void ChangeFactionOpensThePickerAndTheDefaultUsesTheSquadronsFaction()
    {
        using var look = AppLook.Put();
        var fixture = SampleCampaign();
        fixture.Store.SetFaction(Frontier, "Arque Purple Cartel", clearIntents: true);
        var (window, view) = OpenPanel(fixture, headset: false, 1280, 900);

        Press(In<Button>(view).Single(button => button.Name == CampaignPage.ChangeName));

        Assert.Equal(["Campaign", "Campaign faction"], view.Nav.Trail.Select(crumb => crumb.Word));
        var picker = Assert.Single(In<PickerPage>(view));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("campaign-faction-picker.png");
        }

        Press(picker.FindControl<Button>("DefaultButton")!);

        Assert.Equal(Us, fixture.Store.For(Frontier).Faction);
        var call = Assert.Single(fixture.Invoked);
        Assert.Equal((CampaignCapability.SetFactionTool, ToolCaller.Commander), (call.Tool, call.Caller));
        Assert.Equal(["Campaign"], view.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void TheDesktopPageMatchesTheSystemsArtboard()
    {
        using var look = AppLook.Put();
        var (window, view) = OpenPanel(SampleCampaign(), headset: false, 1280, 1300);

        Assert.Equal(
            ["Checklist", "Missions", "Progress", "Statistics", "Campaign", "Ledgers", "Commanders"],
            view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));
        Assert.Equal(12, In<Stepper>(view).Count());

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("campaign-systems-desktop.png");
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelPagesMoreThanTwentyFiveSystems()
    {
        using var look = AppLook.Put();
        var fixture = SampleCampaign();
        fixture.Report = Sample() with
        {
            Systems = [.. Sample().Systems, .. Enumerable.Range(1, 18).Select(i => Place($"Extra {i:00}", 0.01, false))],
        };

        var (window, view) = OpenPanel(fixture, headset: true, AppLook.CaptureWidth, AppLook.CaptureHeight);

        Assert.Equal(FactionModel.PageSize, In<Stepper>(view).Count());
        Assert.True(Shows(view, "1–25 of 30"));

        if (TestSurface.CapturesWanted)
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture("campaign-systems-headset.png");
        }

        Press(In<Button>(view).Single(button => button.Name == CampaignPage.NextName));

        Assert.Equal(5, In<Stepper>(view).Count());
        Assert.True(Shows(view, "26–30 of 30"));

        window.Close();
    }
}
