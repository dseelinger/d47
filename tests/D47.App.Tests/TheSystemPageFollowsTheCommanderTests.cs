using System.Net;
using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// SEARCH › SYSTEM opens on the Commander's system, follows them while it shows it, opens any typed name, and
/// draws each state Spansh can leave it in (#824).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheSystemPageFollowsTheCommanderTests
{
    private const long Ltt7786 = 633608311522;
    private const long Hunaharten = 7268024264097;
    private const long Sol = 10477373803;

    /// <summary>A service that records what it was asked and answers as each test says.</summary>
    private sealed class Systems : IStarSystemService
    {
        public List<long> Asked { get; } = [];

        public List<string> Typed { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public Func<long, Task<StarSystemProfile?>> Profile { get; set; } =
            address => Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {address}", SystemAddress = address });

        public Func<string, Task<IReadOnlyList<SystemNameMatch>>> Names { get; set; } =
            _ => Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add(systemAddress);
                Tokens.Add(cancellationToken);
            }

            return Profile(systemAddress);
        }

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (Typed)
            {
                Typed.Add(typed);
            }

            return Names(typed);
        }

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));

        public long[] AskedNow()
        {
            lock (Asked)
            {
                return [.. Asked];
            }
        }
    }

    private sealed class World
    {
        public JournalLocation? Here { get; set; } = At("LTT 7786", Ltt7786);

        public bool Enabled { get; set; } = true;

        public int SettingsOpened { get; set; }

        public Systems Systems { get; } = new();

        public StarSystemSurface Surface => new(Systems, () => Here, () => Enabled, () => SettingsOpened++);
    }

    private static JournalLocation At(string system, long address) =>
        new(system, null, false, null) { SystemAddress = address };

    private static SystemNameMatch Match(string name, long address) => new(name, address, new StarPosition(0, 0, 0));

    private static (PanelView Panel, Window Window) Shown(World world, double width = 1280, double height = 860, bool open = true)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(world.Surface);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (open)
        {
            panel.Tab = PanelTab.Search;
            Dispatcher.UIThread.RunJobs();
        }

        return (panel, window);
    }

    private static StarSystemPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<StarSystemPage>().Single();

    /// <summary>Pumps the UI thread until <paramref name="done"/> holds, for work that comes back from the pool.</summary>
    private static void Until(Func<bool> done, Func<string>? showing = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, $"the page did not settle in time{(showing is null ? string.Empty : $"; it shows: {showing()}")}");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle(World world, int asked) => Until(() => world.Systems.AskedNow().Length >= asked);

    private static List<string> TextOf(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static bool Shows(Control root, string text) =>
        TextOf(root).Any(line => line.Contains(text, StringComparison.Ordinal));

    private static void WaitToShow(Control root, string text) =>
        Until(() => Shows(root, text), () => string.Join(" | ", TextOf(root)));

    private static IEnumerable<Button> Buttons(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(PanelView panel, string text)
    {
        var box = panel.GetVisualDescendants().OfType<TextBox>().Single(box => AutomationProperties.GetName(box) == "System name");
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Dispatcher.UIThread.RunJobs();
    }

    private static Button? Back(PanelView panel) =>
        Buttons(panel).SingleOrDefault(button =>
            AutomationProperties.GetName(button)?.StartsWith("Back to my system", StringComparison.Ordinal) == true);

    private static void NoRetry(PanelView panel) =>
        Assert.DoesNotContain(Buttons(Page(panel)), button =>
            (AutomationProperties.GetName(button) ?? button.Content as string ?? string.Empty) is var name
            && (name.Contains("retry", StringComparison.OrdinalIgnoreCase)
                || name.Contains("refresh", StringComparison.OrdinalIgnoreCase)
                || name.Contains("try again", StringComparison.OrdinalIgnoreCase)));

    [AvaloniaFact]
    public void SearchHasTheSystemRootOnTheWindowAndNotInMini()
    {
        var (panel, window) = Shown(new World(), open: false);

        Assert.True(panel.Nav.Has(PanelTab.Search));
        Assert.Equal(StarSystemPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        panel.Nav.Select(PanelTab.Search);
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(PanelTab.Search, panel.Tab);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void SearchHasTheSystemRootInTheHeadset()
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var headset = new VrPanelSurface(new PanelViewModel(), settings, _ => null, starSystem: new World().Surface);

        Assert.True(headset.Nav.Has(PanelTab.Search));
        Assert.Equal(StarSystemPage.RootKey, headset.Nav.RootKeyOf(PanelTab.Search));
    }

    [AvaloniaFact]
    public void ItOpensOnTheJournalsSystemAndAsksOnce()
    {
        var world = new World();
        var (panel, window) = Shown(world);

        Settle(world, 1);
        panel.TickSearch();
        panel.TickSearch();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Ltt7786], world.Systems.AskedNow());
        Assert.True(Page(panel).Following);
        Assert.Equal(StarSystemPage.SummaryText, ((IPageSummary)Page(panel)).Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void AnArrivalWhileShowingAsksForTheNewSystem()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        Settle(world, 1);

        world.Here = At("Sol", Sol);
        Assert.True(panel.TickSearch());
        Settle(world, 2);

        Assert.Equal([Ltt7786, Sol], world.Systems.AskedNow());

        window.Close();
    }

    [AvaloniaFact]
    public void AHiddenPageAsksForNothingAndAsksOnceWhenShownAgain()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        Settle(world, 1);

        panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        world.Here = At("Sol", Sol);
        Assert.False(panel.TickSearch());

        world.Here = At("Hunaharten", Hunaharten);
        Assert.False(panel.TickSearch());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Ltt7786], world.Systems.AskedNow());

        panel.Tab = PanelTab.Search;
        Dispatcher.UIThread.RunJobs();
        panel.TickSearch();
        panel.TickSearch();
        Settle(world, 2);

        Assert.Equal([Ltt7786, Hunaharten], world.Systems.AskedNow());

        window.Close();
    }

    [AvaloniaFact]
    public void ATypedSystemStaysUntilTheCommanderGoesBack()
    {
        var world = new World { Here = At("Hunaharten", Hunaharten) };
        world.Systems.Names = _ => Task.FromResult<IReadOnlyList<SystemNameMatch>>(
            [Match("LTT 7786", Ltt7786), Match("LTT 7719", 1)]);

        var (panel, window) = Shown(world);
        Settle(world, 1);
        Assert.Null(Back(panel));

        Type(panel, "ltt 7786");
        Settle(world, 2);

        Assert.Equal(["ltt 7786"], world.Systems.Typed);
        Assert.Equal([Hunaharten, Ltt7786], world.Systems.AskedNow());
        Assert.False(Page(panel).Following);
        WaitToShow(panel, "STAR SYSTEM");

        world.Here = At("Sol", Sol);
        panel.TickSearch();
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Hunaharten, Ltt7786], world.Systems.AskedNow());

        var back = Back(panel);
        Assert.NotNull(back);
        Assert.Equal("Back to my system, Sol", AutomationProperties.GetName(back));

        Press(back);
        Settle(world, 3);

        Assert.Equal([Hunaharten, Ltt7786, Sol], world.Systems.AskedNow());
        Assert.True(Page(panel).Following);
        Assert.Null(Back(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void CloseNamesAreListedAtMostFive()
    {
        var world = new World();
        world.Systems.Names = _ => Task.FromResult<IReadOnlyList<SystemNameMatch>>(
            [.. Enumerable.Range(1, 7).Select(n => Match($"LTT 77{n}0", n))]);

        var (panel, window) = Shown(world);
        Settle(world, 1);

        Type(panel, "LTT 778");
        WaitToShow(panel, "No system is called “LTT 778”. Spansh has these close names.");

        var options = Buttons(Page(panel))
            .Where(button => AutomationProperties.GetName(button)?.StartsWith("LTT 77", StringComparison.Ordinal) == true)
            .ToList();
        Assert.Equal(StarSystemPage.CloseNames, options.Count);

        Press(options[2]);
        Settle(world, 2);

        Assert.Equal(3, world.Systems.AskedNow()[^1]);
        NoRetry(panel);

        window.Close();
    }

    [AvaloniaFact]
    public void ANameWithNoCloseMatchSaysSo()
    {
        var world = new World();
        var (panel, window) = Shown(world);
        Settle(world, 1);

        Type(panel, "Qwerty 12");
        WaitToShow(panel, "Spansh has no system called “Qwerty 12”, and none with a close name.");
        NoRetry(panel);

        window.Close();
    }

    [AvaloniaFact]
    public void ANewerRequestCancelsTheOneInFlight()
    {
        var world = new World();
        world.Systems.Profile = _ => new TaskCompletionSource<StarSystemProfile?>().Task;

        var (panel, window) = Shown(world);
        Settle(world, 1);

        world.Here = At("Sol", Sol);
        panel.TickSearch();
        Settle(world, 2);

        Assert.True(world.Systems.Tokens[0].IsCancellationRequested);
        Assert.False(world.Systems.Tokens[1].IsCancellationRequested);

        window.Close();
    }

    [AvaloniaFact]
    public void TheArrivalCheckReturnsBeforeTheFetchCompletes()
    {
        var world = new World();
        var pending = new TaskCompletionSource<StarSystemProfile?>();
        world.Systems.Profile = _ => pending.Task;

        var (panel, window) = Shown(world);
        Settle(world, 1);

        world.Here = At("Sol", Sol);
        Assert.True(panel.TickSearch());
        Assert.False(pending.Task.IsCompleted);
        WaitToShow(panel, "Asking Spansh for Sol. A populated system takes about two seconds.");

        pending.SetResult(new StarSystemProfile { Name = "Sol", SystemAddress = Sol });
        WaitToShow(panel, "ECONOMY");

        window.Close();
    }

    [AvaloniaFact]
    public void AnAnswerArrivingIsReportedOnTheNextTick()
    {
        var world = new World();
        var pending = new TaskCompletionSource<StarSystemProfile?>();
        world.Systems.Profile = _ => pending.Task;

        var (panel, window) = Shown(world);
        Settle(world, 1);
        panel.TickSearch();

        Assert.False(panel.TickSearch());

        pending.SetResult(new StarSystemProfile { Name = "LTT 7786", SystemAddress = Ltt7786 });
        WaitToShow(panel, "ECONOMY");

        Assert.True(panel.TickSearch());
        Assert.False(panel.TickSearch());

        window.Close();
    }

    [AvaloniaFact]
    public void AnUnreadableAnswerSaysSpanshDidNotAnswer()
    {
        var world = new World();
        world.Systems.Profile = _ => Task.FromException<StarSystemProfile?>(new System.Text.Json.JsonException("truncated"));

        var (panel, window) = Shown(world);

        WaitToShow(panel, "SPANSH DIDN'T ANSWER");
        Assert.True(Shows(panel, $"{StarSystemPage.Unreadable} {StarSystemPage.AsksAgain}"));

        window.Close();
    }

    [AvaloniaFact]
    public void LookupsOffSaySoAndAskForNothing()
    {
        var world = new World { Enabled = false };
        var (panel, window) = Shown(world);

        Assert.True(Shows(panel, "SYSTEM LOOKUPS ARE OFF"));
        Assert.True(Shows(panel, "Looking up a star system is switched off. It shares the galaxy search setting, so turning on “Look things up in the galaxy” switches it on."));

        Press(Buttons(Page(panel)).Single(button => button.Content as string == "Open settings"));
        Assert.Equal(1, world.SettingsOpened);
        Assert.Empty(world.Systems.AskedNow());
        NoRetry(panel);

        world.Enabled = true;
        panel.TickSearch();
        Settle(world, 1);

        Assert.Equal([Ltt7786], world.Systems.AskedNow());

        window.Close();
    }

    [AvaloniaFact]
    public void AnUnansweredLookupSaysSpanshDidNotAnswer()
    {
        var world = new World();
        world.Systems.Profile = _ => Task.FromException<StarSystemProfile?>(
            new GalaxyUnavailableException("I couldn't reach the system lookup — check the network connection."));

        var (panel, window) = Shown(world);

        WaitToShow(panel, "SPANSH DIDN'T ANSWER");
        Assert.True(Shows(panel, $"I couldn't reach the system lookup — check the network connection. {StarSystemPage.AsksAgain}"));
        Assert.True(Shows(panel, "LTT 7786"));
        NoRetry(panel);

        window.Close();
    }

    [AvaloniaFact]
    public void ASystemSpanshHasNoRecordOfSaysSo()
    {
        var world = new World();
        world.Systems.Profile = _ => Task.FromResult<StarSystemProfile?>(null);

        var (panel, window) = Shown(world);

        WaitToShow(panel, "Spansh has no record of this system yet.");
        Assert.True(Shows(panel, "LTT 7786"));
        NoRetry(panel);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoJournalOnlyTheFieldIsOffered()
    {
        var world = new World { Here = null };
        var (panel, window) = Shown(world);

        Assert.True(Shows(panel, StarSystemPage.NoJournal));
        Assert.Empty(world.Systems.AskedNow());
        Assert.Null(Back(panel));
        NoRetry(panel);

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheOverviewReadsTheRecord()
    {
        var profile = Ltt7786Profile();

        Assert.Equal("Industrial / Refinery", StarSystemPage.Economy(profile));
        Assert.Equal("K7 V · Scoopable", StarSystemPage.MainStar(profile));
        Assert.Equal("7.97 / −41.91 / 77.88", StarSystemPage.Coordinates(profile.Position));

        var world = new World();
        world.Systems.Profile = _ => Task.FromResult<StarSystemProfile?>(profile);

        var (panel, window) = Shown(world);
        WaitToShow(panel, "LAST REPORT TO SPANSH");

        Assert.True(Shows(panel, "YOUR SYSTEM · FOLLOWING YOU"));
        Assert.True(Shows(panel, "MINOR FACTIONS"));
        Assert.True(Shows(panel, "CONTROLS"));
        Assert.True(Shows(panel, $"{profile.Factions.Count} FACTIONS"));
        Assert.True(Shows(panel, profile.ReportedAt!.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void EachStateIsCaptured()
    {
        using var look = AppLook.Put();
        var profile = Ltt7786Profile();

        Capture("system-overview-following.png", new World(), world => world.Systems.Profile = _ => Task.FromResult<StarSystemProfile?>(profile), "LAST REPORT TO SPANSH");

        Capture(
            "system-typed.png",
            new World { Here = At("Hunaharten", Hunaharten) },
            world =>
            {
                world.Systems.Profile = address => Task.FromResult<StarSystemProfile?>(
                    address == Ltt7786 ? profile : new StarSystemProfile { Name = "Hunaharten", SystemAddress = address });
                world.Systems.Names = _ => Task.FromResult<IReadOnlyList<SystemNameMatch>>([Match("LTT 7786", Ltt7786)]);
            },
            "YOUR SYSTEM",
            panel => Type(panel, "LTT 7786"),
            "STAR SYSTEM");

        Capture("system-loading.png", new World(), world => world.Systems.Profile = _ => new TaskCompletionSource<StarSystemProfile?>().Task, "Asking Spansh");

        Capture(
            "system-unavailable.png",
            new World(),
            world => world.Systems.Profile = _ => Task.FromException<StarSystemProfile?>(
                new GalaxyUnavailableException("I couldn't reach the system lookup — check the network connection.")),
            "SPANSH DIDN'T ANSWER");

        Capture("system-unrecorded.png", new World(), world => world.Systems.Profile = _ => Task.FromResult<StarSystemProfile?>(null), "no record");

        Capture(
            "system-close-names.png",
            new World(),
            world => world.Systems.Names = _ => Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [Match("LTT 7786", 1), Match("LTT 7719", 2), Match("LTT 16422", 3), Match("LTT 9848", 4), Match("LTT 16741", 5)]),
            "ECONOMY",
            panel => Type(panel, "LTT 778"),
            "close names");

        Capture("system-no-match.png", new World(), _ => { }, "ECONOMY", panel => Type(panel, "Qwerty 12"), "none with a close name");

        Capture("system-lookups-off.png", new World { Enabled = false }, _ => { }, "LOOKUPS ARE OFF");

        Capture("system-no-journal.png", new World { Here = null }, _ => { }, "hasn't read a journal");
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheHeadsetIsCaptured()
    {
        using var look = AppLook.Put();
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var world = new World();
        var profile = Ltt7786Profile();
        world.Systems.Profile = _ => Task.FromResult<StarSystemProfile?>(profile);

        var headset = new VrPanelSurface(new PanelViewModel(), settings, _ => null, starSystem: world.Surface);
        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(headset)!;

        headset.Nav.Select(PanelTab.Search);
        Until(() =>
        {
            Serve(headset);
            return view.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "LAST REPORT TO SPANSH");
        }, () => $"{view.Tab} {world.Systems.AskedNow().Length} " + string.Join(" | ", view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)));

        Serve(headset);

        var (width, height) = headset.Size;
        using var frame = new RenderTargetBitmap(new Avalonia.PixelSize(width, height));
        frame.Render(view);
        frame.SaveCapture("system-headset.png");
    }

    /// <summary>One frame, into a buffer nobody reads, which lays the headset's panel out.</summary>
    private static void Serve(VrPanelSurface headset)
    {
        Dispatcher.UIThread.RunJobs();

        var (width, height) = headset.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                headset.Draw((IntPtr)pixels, width * 4);
                headset.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(
        string name,
        World world,
        Action<World> arrange,
        string settled,
        Action<PanelView>? act = null,
        string? acted = null)
    {
        arrange(world);

        var (panel, window) = Shown(world);
        WaitToShow(panel, settled);

        if (act is not null)
        {
            act(panel);
            WaitToShow(panel, acted!);
        }

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);

        window.Close();
    }

    /// <summary>LTT 7786 as Spansh had it on 2026-10-05, read through the real service.</summary>
    private static StarSystemProfile Ltt7786Profile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        var body = File.ReadAllText(Path.Combine(root.FullName, "tests", "D47.Knowledge.Tests", "Fixtures", "spansh-dump-ltt-7786.json"));

        using var service = new SpanshStarSystemService(
            NullLogger<SpanshStarSystemService>.Instance,
            new HttpClient(new Answer(body)));

        return service.ProfileAsync(Ltt7786, CancellationToken.None).GetAwaiter().GetResult()!;
    }

    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
