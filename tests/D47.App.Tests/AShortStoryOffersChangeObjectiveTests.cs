using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Interface;
using Xunit;
using static D47.App.Tests.NotForMeGroupFixture;

namespace D47.App.Tests;

/// <summary>A begun, unfinished short story's page offers Change objective and Never ask this under NEXT, and each changes only its own story.</summary>
[Trait("Category", "Integration")]
public sealed class AShortStoryOffersChangeObjectiveTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 20, 0, 0, TimeSpan.Zero);

    private static Task<AdventureOutcome> Write(AdventureAsk ask, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var draft = ask.Rewrite!.Chapter with
        {
            Beats =
            [
                .. ask.Rewrite.Chapter.Beats.Take(ask.Rewrite.From),
                new AdventureBeat
                {
                    Title = "Another Way",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 },
                    Line = "Two more.",
                },
            ],
        };

        return Task.FromResult(new AdventureOutcome(draft, "A bounty instead.", null, []));
    }

    private static Adventure Short(string key, DateTimeOffset? accepted, DateTimeOffset? abandoned = null) => new()
    {
        Key = key,
        Name = key,
        Source = AdventureSource.Generated,
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Signal",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
                Line = "Go and look.",
            },
        ],
        AcceptedAt = accepted,
        AbandonedAt = abandoned,
    };

    private static Surface Read(string key, params Adventure[] shorts)
    {
        var surface = Open(TriggerKind.Arrive, write: Write);

        foreach (var adventure in shorts)
        {
            Assert.Null(surface.Stories!.Book.Write("F1", adventure));
        }

        surface.Panel.Nav.SelectRoot(AdventuresPage.RootKey);
        surface.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + key, key));
        Dispatcher.UIThread.RunJobs();
        return surface;
    }

    private static void Press(Surface surface, string label)
    {
        var button = Assert.IsAssignableFrom<Button>(Pressable(surface.Panel, label));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string? Objective(Surface surface, string key) =>
        surface.Stories!.Book.Standing("F1", key)?.CurrentBeat?.Title;

    [AvaloniaFact]
    public void BothButtonsShowOnAnActiveShortStory()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("short-a", Short("short-a", Now.AddHours(-1)));

        Assert.NotNull(Pressable(surface.Panel, "Change objective"));
        Assert.NotNull(Pressable(surface.Panel, "Never ask this"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void NeitherButtonShowsOnAShortStoryNotBegunOrAbandoned()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("not-begun", Short("not-begun", null), Short("set-aside", Now.AddHours(-2), Now.AddHours(-1)));

        Assert.Null(Pressable(surface.Panel, "Change objective"));
        Assert.Null(Pressable(surface.Panel, "Never ask this"));

        surface.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "set-aside", "set-aside"));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(Pressable(surface.Panel, "Change objective"));
        Assert.Null(Pressable(surface.Panel, "Never ask this"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ChangeObjectiveAsksWhatYouWouldRatherDo()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("short-a", Short("short-a", Now.AddHours(-1)));
        Press(surface, "Change objective");

        Assert.NotNull(Starting(surface.Panel, "What would you rather do?"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void NeverAskThisAsksWhetherToWriteADifferentObjective()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("short-a", Short("short-a", Now.AddHours(-1)));
        Press(surface, "Never ask this");

        Assert.NotNull(Starting(surface.Panel, "Write a different objective?"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task UseThisChangesOnlyTheStoryOnThePage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("short-b", Short("short-a", Now.AddHours(-1)), Short("short-b", Now.AddHours(-1)));
        var outcome = await surface.Stories!.Stories!.ReviseAdventureObjectiveAsync("F1", "short-b", "a bounty instead", [], CancellationToken.None);

        Assert.Null(outcome.Refusal);

        StoriesView.Propose(surface.Stories, surface.Panel.Prompts, "F1", () => { }, _ => { }, outcome, "short-b");
        Dispatcher.UIThread.RunJobs();

        var use = surface.Panel.GetVisualDescendants().OfType<Button>().First(button =>
            button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Use this"));
        use.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Another Way", Objective(surface, "short-b"));
        Assert.Equal("The Signal", Objective(surface, "short-a"));
        Assert.NotNull(Starting(surface.Panel, "Bounties: 0 of 2"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public async Task NeverAskThisReplacesOnlyTheStoryOnThePage()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Read("short-b", Short("short-a", Now.AddHours(-1)), Short("short-b", Now.AddHours(-1)));
        var done = new List<string?>();

        StoriesView.NotForMe(surface.Stories!, surface.Panel.Prompts, "F1", () => { }, done.Add, "short-b");
        Dispatcher.UIThread.RunJobs();

        var yes = surface.Panel.GetVisualDescendants().OfType<Button>().First(button =>
            button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Write a different one"));
        yes.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (done.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal([null], done);
        Assert.Equal("Another Way", Objective(surface, "short-b"));
        Assert.Equal("The Signal", Objective(surface, "short-a"));

        surface.Window.Close();
    }
}
