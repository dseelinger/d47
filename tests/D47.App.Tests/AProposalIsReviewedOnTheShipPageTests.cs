using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page carries TALK THROUGH THIS BUILD, where a proposal is accepted or rejected per slot (#570).</summary>
[Trait("Category", "Integration")]
[Collection(nameof(ShipArtCollection))]
public class AProposalIsReviewedOnTheShipPageTests
{
    private const int ShipId = 12;

    private static readonly SlotPlan Engines = new("MainEngines", "Clean Drive Tuning", 3, Module: "Thrusters");

    private sealed record Bench(ShipsMode Mode, ShipPlanService Ships, BuildTalk Talk, string BuildId);

    private static Bench Set(Func<ShipBuild, Task<BuildAdvice>> advise)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-build-talk-tests"));

        paths.EnsureCreated();

        // As MainWindow points it, so the page draws its hull as the app does.
        ShipArt.Files = new DiskFileSystem();
        ShipArt.Folder = paths.Ships;
        ShipArt.Shipped = Path.Combine(AppContext.BaseDirectory, "ships");

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-10-05T19:00:00Z","event":"Commander","FID":"F1","Name":"John Deparagon"}""",
                     """{"timestamp":"2026-10-05T19:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Blue Meridian","Modules":[{"Slot":"MainEngines","Item":"int_engine_size6_class5","On":true}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => live);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        var build = ships.BuildFor(ShipId, "python", "Blue Meridian");
        ships.Plan(build.Id, Engines);

        var talk = new BuildTalk(
            ships,
            (asked, _, _, _) => advise(asked),
            () => new DateTimeOffset(2026, 10, 5, 19, 43, 0, TimeSpan.Zero));

        return new Bench(new ShipsMode(ships, checklists, () => live, talk: talk), ships, talk, build.Id);
    }

    private static Task<BuildAdvice> ThreeChanges(ShipBuild build) => Task.FromResult(new BuildAdvice(
        BuildRemarkKind.Goal,
        "Three slots change. The Frame Shift Drive takes grade 5 Increased Range, and the Thrusters drop to lighter tuning.",
        [
            new SlotChange("FrameShiftDrive", build.For("FrameShiftDrive"), new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, "Mass Manager", "Frame Shift Drive"), "The largest single gain in jump range."),
            new SlotChange("MainEngines", build.For("MainEngines"), Engines with { Blueprint = "Dirty Drive Tuning", Grade = 2 }, "Lighter tuning, less mass."),
            new SlotChange("PowerPlant", build.For("PowerPlant"), new SlotPlan("PowerPlant", "Low Emissions", 5, Module: "Power Plant"), "Cooler, for longer jumps between scoops."),
        ],
        ShipPlanAdvisor.Cost(build.With(new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, Module: "Frame Shift Drive")), null),
        ["LifeSupport: there is no blueprint called Featherweight for Life Support."]));

    private static Window Show(Control content)
    {
        var window = new Window
        {
            Content = content,
            Width = 1100,
            Height = 1700,
            Background = (Avalonia.Media.IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        var path = name;
        frame.SaveCapture(path);
    }

    private static Button Pressable(Control root, string label, int index = 0) =>
        root.GetVisualDescendants().OfType<Button>().Where(button => Equals(button.Content, label)).ElementAt(index);

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<string> Texts(Control root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty);

    [AvaloniaFact]
    public void TheSectionSitsAboveTheSlotsAndShowsOnlyTheFieldUntilSomethingIsSaid()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var bench = Set(ThreeChanges);
        var page = new ItemPage(bench.Mode, new PanelNavigator(), bench.BuildId);
        var window = Show(page);

        var section = Assert.Single(page.GetVisualDescendants().OfType<BuildTalkSection>());
        var texts = Texts(page).ToList();

        Assert.True(texts.IndexOf("TALK THROUGH THIS BUILD") < texts.FindIndex(text => text.Contains("THRUSTERS", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(BuildTalkSection.Hint, texts);
        Assert.False(Pressable(section, "Clear").IsVisible);
        Assert.Equal(bench.BuildId, bench.Talk.Open);

        Capture(window, "build-talk-empty.png");

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(bench.Talk.Open);
    }

    [AvaloniaFact]
    public async Task AcceptingARowWritesTheSlotAndRedrawsTheSlotList()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var bench = Set(ThreeChanges);
        var page = new ItemPage(bench.Mode, new PanelNavigator(), bench.BuildId);
        var window = Show(page);

        await bench.Talk.AskAsync(bench.BuildId, "Make it jump further. I don't care about the shields.", InputSource.Typed, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        var section = page.GetVisualDescendants().OfType<BuildTalkSection>().Single();

        Assert.Contains("3 CHANGES · 0 ACCEPTED · 0 REJECTED · 3 TO DECIDE", Texts(section));
        Assert.Contains("Dropped: LifeSupport: there is no blueprint called Featherweight for Life Support.", Texts(section));

        Press(Pressable(section, "Accept", 0));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Increased FSD Range", bench.Ships.Store.Find(bench.BuildId)!.For("FrameShiftDrive")?.Blueprint);
        Assert.Contains("✓ IN THE PLAN", Texts(section));

        Press(Pressable(section, "Reject", 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(bench.Ships.Store.Find(bench.BuildId)!.For("PowerPlant"));

        // The engines are edited after the proposal: the row can only be rejected.
        bench.Ships.Plan(bench.BuildId, Engines with { Grade = 5 });
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("PLAN CHANGED SINCE", Texts(section));
        Assert.False(Pressable(section, "Accept", 0).IsEnabled);
        Assert.False(Pressable(section, "Accept all").IsEnabled);
        Assert.True(Pressable(section, "Reject all").IsEnabled);
        Assert.Contains("3 CHANGES · 1 ACCEPTED · 1 REJECTED · 1 TO DECIDE", Texts(section));

        // The slot list below the section has redrawn with the accepted plan.
        Assert.Contains(Texts(page), text => text.Contains("Increased FSD Range", StringComparison.OrdinalIgnoreCase));

        Capture(window, "build-talk-proposal.png");

        window.Close();
    }

    [AvaloniaFact]
    public async Task AFailedTurnSaysThePlanIsUnchangedAndOffersRetry()
    {
        using var kit = AppLook.ControlKit();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var bench = Set(_ => Task.FromResult(new BuildAdvice(
            BuildRemarkKind.Question, null, [], null, [], "The model's answer was not one I could read. Try again.", "unreadable")));

        var page = new ItemPage(bench.Mode, new PanelNavigator(), bench.BuildId);
        var window = Show(page);

        await bench.Talk.AskAsync(bench.BuildId, "Make it a better explorer.", InputSource.Spoken, CancellationToken.None);
        await bench.Talk.AskAsync(bench.BuildId, "Why that thruster?", InputSource.Typed, CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        var section = page.GetVisualDescendants().OfType<BuildTalkSection>().Single();
        var texts = Texts(section).ToList();

        Assert.Contains($"The model's answer was not one I could read. Try again. {BuildTalkSection.Unchanged}", texts);
        Assert.Contains("UNREADABLE", texts);
        Assert.Contains("CMDR JOHN DEPARAGON", texts);
        Assert.Contains(texts, text => text.StartsWith("by voice · ", StringComparison.Ordinal));
        Assert.True(Pressable(section, "Retry").IsEnabled);
        Assert.True(Pressable(section, "Clear").IsVisible);

        Capture(window, "build-talk-failed.png");

        window.Close();
    }
}
