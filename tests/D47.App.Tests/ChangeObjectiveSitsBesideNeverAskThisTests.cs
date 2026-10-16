using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>A refusable objective offers Change objective beside Never ask this, on the Stories tab and the chapter's page; the proposal's three buttons apply, keep or reword it. Captures go to <see cref="TestSurface.CaptureDirectory"/>.</summary>
[Trait("Category", "Integration")]
public sealed class ChangeObjectiveSitsBesideNeverAskThisTests
{
    private const string Reply = "A bounty puts you in the same fight, a little closer to home.";

    private static Task<AdventureOutcome> Write(AdventureAsk ask, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var draft = new Adventure
        {
            Key = ChapterKey,
            Name = "The First Light",
            Source = AdventureSource.Generated,
            StoryId = StoryFixture.Story.Id,
            Beats =
            [
                new AdventureBeat
                {
                    Title = "Another Way",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 },
                    Line = "Two more.",
                },
            ],
        };

        return Task.FromResult(new AdventureOutcome(draft, Reply, null, []));
    }

    private static string? Objective(AdventureSurface surface) =>
        surface.Book.Standing("F1", ChapterKey)?.CurrentBeat?.Title;

    /// <summary>A proposal for "a bounty instead" held by the director and offered in front of the panel; <paramref name="done"/> collects what the page is told.</summary>
    private static async Task<Surface> Offered(List<string?> done, Func<string?>? commander = null, bool chapter = false)
    {
        var surface = Open(TriggerKind.Arrive, write: Write, commander: commander);

        if (chapter)
        {
            ReadChapter(surface);
        }

        var stories = surface.Stories!;
        var outcome = await stories.Stories!.ReviseObjectiveAsync("F1", "a bounty instead", [], CancellationToken.None);

        Assert.Null(outcome.Refusal);

        StoriesView.Propose(stories, surface.Panel.Prompts, "F1", () => { }, done.Add, outcome);
        Dispatcher.UIThread.RunJobs();
        return surface;
    }

    private static Button? Labelled(Visual root, string label) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Content as string == label);

    private static Button? Option(Surface surface, string label) =>
        surface.Panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

    private static void Press(Surface surface, string label)
    {
        var button = Assert.IsAssignableFrom<Button>(Option(surface, label));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void BothButtonsSitOnTheStripAndTheChapterPage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        Save(surface.Window, "change-objective-strip.png");

        Assert.NotNull(Pressable(surface.Panel, "Change objective"));
        Assert.NotNull(Pressable(surface.Panel, "Never ask this"));
        Assert.Null(Pressable(surface.Panel, "Not for me"));

        ReadChapter(surface);
        Save(surface.Window, "change-objective-chapter.png");

        var change = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Change objective"));
        Assert.NotNull(Pressable(surface.Panel, "Never ask this"));
        Assert.True(change.Bounds.Height >= 44);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheBeaconScanOffersNeitherButton()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Beacon);

        Assert.Null(Pressable(surface.Panel, "Change objective"));
        Assert.Null(Pressable(surface.Panel, "Never ask this"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ChangeObjectiveOpensTheEntryForTheCommandersWords()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(TriggerKind.Arrive);
        var change = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, "Change objective"));
        change.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Save(surface.Window, "change-objective-entry.png");

        Assert.NotNull(Starting(surface.Panel, "What would you rather do?"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task TheProposalIsShownWithThreeButtons()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = await Offered([]);
        Save(surface.Window, "change-objective-proposal.png");

        Assert.NotNull(Starting(surface.Panel, Reply));

        foreach (var label in new[] { "Use this", "Keep the current one", "Say something else" })
        {
            var button = Assert.IsAssignableFrom<Button>(Option(surface, label));
            Assert.True(button.Bounds.Height >= 44, label);
        }

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task UseThisLeavesThePageShowingTheNewObjective()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var done = new List<string?>();
        var surface = await Offered(done);
        Press(surface, "Use this");

        Assert.Equal("Another Way", Objective(surface.Stories!));
        Assert.Null(Assert.Single(done.Skip(1).DefaultIfEmpty(null)));
        Assert.Null(surface.Stories!.Stories!.Proposal("F1"));
        Assert.NotNull(Starting(surface.Panel, "Bounties: 0 of 2"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task KeepTheCurrentOneLeavesTheObjectiveAsItWas()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = await Offered([]);
        Press(surface, "Keep the current one");

        Assert.Equal("The Signal", Objective(surface.Stories!));
        Assert.Null(surface.Stories!.Stories!.Proposal("F1"));
        Assert.NotNull(Starting(surface.Panel, "Arrive"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task AStaleProposalIsRefusedAndTheObjectiveStays()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var done = new List<string?>();
        var surface = await Offered(done);
        surface.Stories!.Stories!.Abandon("F1", DateTimeOffset.UtcNow);
        Press(surface, "Use this");

        Assert.Equal("The story changed while the objective was being written.", done.LastOrDefault());
        Assert.Null(surface.Stories!.Stories!.Proposal("F1"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task SaySomethingElseOpensTheEntryAgain()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = await Offered([]);
        Press(surface, "Say something else");

        Assert.NotNull(Starting(surface.Panel, "What would you rather do?"));
        Assert.Equal("The Signal", Objective(surface.Stories!));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task UseThisLeavesTheChapterPageShowingTheNewObjective()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = await Offered([], chapter: true);
        Press(surface, "Use this");

        Assert.Equal("Another Way", Objective(surface.Stories!));
        Assert.NotNull(Starting(surface.Panel, "Bounties: 0 of 2"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task AProposalPressedAfterTheCommanderChangedDoesNothing()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var shown = "F1";
        var done = new List<string?>();
        var surface = await Offered(done, () => shown);
        shown = "F2";
        Press(surface, "Use this");

        Assert.Equal(StoriesView.CommanderChangedRefusal, done.LastOrDefault());
        Assert.Equal("The Signal", Objective(surface.Stories!));
        Assert.Null(surface.Stories!.Stories!.Proposal("F1"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task NeitherButtonIsDrawnWhileAnObjectiveIsBeingWritten()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var release = new TaskCompletionSource();

        var surface = Open(TriggerKind.Arrive, write: async (ask, now, token) =>
        {
            await release.Task;
            return await Write(ask, now, token);
        });

        var writing = surface.Stories!.Stories!.ReviseObjectiveAsync("F1", "a bounty instead", [], CancellationToken.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Pressable(surface.Panel, "Change objective"));
        Assert.Null(Pressable(surface.Panel, "Never ask this"));

        ReadChapter(surface);

        Assert.Null(Pressable(surface.Panel, "Change objective"));
        Assert.Null(Pressable(surface.Panel, "Never ask this"));

        release.SetResult();
        await writing;
        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheVoiceToolShowsTheStoriesTabAndOpensTheEntry()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var desk = new AdventureCapability.AdventureDesk();
        var surface = Open(TriggerKind.Arrive, desk: desk);
        surface.Panel.Tab = PanelTab.Transcript;
        Dispatcher.UIThread.RunJobs();

        Assert.Null(desk.ChangeObjective());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Stories, surface.Panel.Tab);
        Assert.NotNull(Starting(surface.Panel, "What would you rather do?"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheVoiceToolSaysWhyWhenTheStoryIsNotWaiting()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var desk = new AdventureCapability.AdventureDesk();
        var surface = Open(TriggerKind.Beacon, desk: desk);

        Assert.Equal("The story is not waiting on an objective.", desk.ChangeObjective());

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task TheHeadsetDrawsBothButtonsAndTheProposalWithPressableTargets()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);
        var stories = Build(TriggerKind.Arrive, write: Write);

        using var vr = new VrPanelSurface(new PanelViewModel(), settings, _ => null, gameState: () => null, adventures: stories);

        vr.Nav.Select(PanelTab.Stories);
        vr.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render().SaveCapture("change-objective-strip-headset.png");

        foreach (var label in new[] { "Change objective", "Never ask this" })
        {
            var button = Assert.IsAssignableFrom<Button>(Labelled(vr.Board.View, label));
            Assert.True(button.Bounds.Height >= 44, label);
        }

        var outcome = await stories.Stories!.ReviseObjectiveAsync("F1", "a bounty instead", [], CancellationToken.None);
        StoriesView.Propose(stories, vr.Prompts, "F1", () => { }, _ => { }, outcome);
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render();
        Dispatcher.UIThread.RunJobs();
        vr.Board.Render().SaveCapture("change-objective-proposal-headset.png");

        foreach (var label in new[] { "Use this", "Keep the current one", "Say something else" })
        {
            var button = vr.Board.View.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                b.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

            Assert.NotNull(button);
            Assert.True(button.Bounds.Height >= 44, label);
        }
    }
}
