using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The Commander plots to, or copies, the system of the objective a story is waiting on.</summary>
public sealed class TheCommanderCanSetCourseForTheNextObjectiveTests
{
    private const string Clipboard = "clipboard";

    private static StoryFixtures Fixtures(params string[] answers) =>
        new(new RoundScriptedLlmProvider(answers.Select(answer => RoundScriptedLlmProvider.Saying(answer)).ToArray()));

    private static Adventure ShortStory(string key, string name) => LanternRoute() with { Key = key, Name = name, StoryId = null };

    private static void Settle(StoryFixtures fixtures, DateTimeOffset at)
    {
        foreach (var moment in fixtures.Book.Drain())
        {
            fixtures.Book.Told("F1", moment.Adventure.Key, new AdventureTold
            {
                Kind = AdventureToldKind.Beat,
                Text = "Told.",
                At = at,
                Beat = moment.Beat,
            });
        }
    }

    [Fact]
    public async Task AStockStoryChaptersObjectiveNamesItsSystem()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));
        Settle(fixtures, Now);
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        Settle(fixtures, Now.AddMinutes(1));

        var key = Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        var expected = fixtures.Book.Standing("F1", key)!.CurrentTrigger!.System;

        Assert.False(string.IsNullOrEmpty(expected));
        Assert.Equal((expected, (string?)null), fixtures.Director.ObjectiveSystem("F1"));
    }

    [Fact]
    public void TheOneActiveShortStorysObjectiveNamesItsSystem()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));

        Assert.Equal(("Ossen's Lantern", (string?)null), fixtures.Director.ObjectiveSystem("F1"));
    }

    [Fact]
    public void TwoShortStoriesAreRefusedByName()
    {
        using var fixtures = Fixtures();

        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-anchorage-run", "The Anchorage Run")));
        Assert.Null(fixtures.Book.Write("F1", ShortStory("the-quiet-field", "The Quiet Field")));
        Assert.Null(fixtures.Book.Begin("F1", "the-anchorage-run", Accepted));
        Assert.Null(fixtures.Book.Begin("F1", "the-quiet-field", Accepted));

        var (system, refusal) = fixtures.Director.ObjectiveSystem("F1");

        Assert.Null(system);
        Assert.Contains("The Anchorage Run", refusal);
        Assert.Contains("The Quiet Field", refusal);
    }

    [Fact]
    public void AnObjectiveWithNoSystemIsRefused()
    {
        using var fixtures = Fixtures();
        var story = ShortStory("the-rank-run", "The Rank Run");

        Assert.Null(fixtures.Book.Write("F1", story with
        {
            Beats = [Beat("Promotion", null, new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Explore", Rank = 6 }, "Rise.")],
        }));
        Assert.Null(fixtures.Book.Begin("F1", "the-rank-run", Accepted));

        Assert.Equal((null, "That objective has no system to go to."), fixtures.Director.ObjectiveSystem("F1"));
    }

    [Fact]
    public void WithNoStoryUnderWayItSaysSo()
    {
        using var fixtures = Fixtures();

        Assert.Equal((null, "No story is under way."), fixtures.Director.ObjectiveSystem("F1"));
    }

    [Fact]
    public async Task APausedStockStoryIsToldToResume()
    {
        using var fixtures = Fixtures(Spine, BeatsToTheBeacon);
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, TestContext.Current.CancellationToken));
        Assert.Null(fixtures.Director.SetOn("F1", false, Now.AddMinutes(3)));

        var (system, refusal) = fixtures.Director.ObjectiveSystem("F1");

        Assert.Null(system);
        Assert.Contains("Resume it first", refusal);
    }

    public static TheoryData<string> Phrases()
    {
        var data = new TheoryData<string>();
        string[] nouns = ["next objective", "my next objective", "the next objective"];

        foreach (var noun in nouns)
        {
            data.Add($"set course for {noun}");

            foreach (var target in new[] { "the clipboard", "my clipboard", Clipboard })
            {
                data.Add($"copy {noun} to {target}");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Phrases))]
    public void EachPhraseReachesItsToolWithoutTheModel(string said)
    {
        var router = new KeywordRouter(CapabilityRegistry.Build([AdventureCapability.Create()]));

        Assert.Equal(
            said.StartsWith("set", StringComparison.Ordinal) ? AdventureCapability.SetCourseTool : AdventureCapability.CopyObjectiveTool,
            router.MatchToolCommand(said)?.ToolName);
    }

    [Fact]
    public async Task TheToolsCopyTheSystemAndPlotToItAndRefuseWithoutOne()
    {
        var clipboard = new RecordingClipboard();
        var surface = NavigationSurface.Inert with { Clipboard = clipboard };
        var course = new AdventureCapability.ObjectiveCourse { Resolve = () => ("Ossen's Lantern", null) };
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(objectiveCourse: course, navigation: surface)]);

        var copied = await registry.InvokeAsync(
            AdventureCapability.CopyObjectiveTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(copied.IsError, copied.Content);
        Assert.Equal("Ossen's Lantern", clipboard.Last);

        var plotted = await registry.InvokeAsync(
            AdventureCapability.SetCourseTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(plotted.IsError, plotted.Content);
        Assert.Equal(["Ossen's Lantern", "Ossen's Lantern"], clipboard.Written);

        course.Resolve = () => (null, "That objective has no system to go to.");
        var refused = await registry.InvokeAsync(
            AdventureCapability.SetCourseTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(refused.IsError);
        Assert.Equal(2, clipboard.Written.Count);

        var model = await registry.InvokeAsync(
            AdventureCapability.CopyObjectiveTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(model.IsError);
    }
}
