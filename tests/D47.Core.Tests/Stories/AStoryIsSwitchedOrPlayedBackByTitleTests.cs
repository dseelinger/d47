using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>"Switch to the story X" and "play back X" are the Commander's, one phrase per title.</summary>
[Trait("Category", "Integration")]
public sealed class AStoryIsSwitchedOrPlayedBackByTitleTests
{
    private const string Commander = "F1";

    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        [.. Enumerable.Range(0, 2).SelectMany(_ => new[]
        {
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        })]));

    private static AdventureCapability.AdventureDesk Desk(StoryFixtures fixtures, List<string>? played = null) => new()
    {
        Titles = () => StoryRunVoice.Titles(fixtures.Director, Commander),
        Switch = (title, token) => StoryRunVoice.SwitchAsync(fixtures.Director, Commander, title, Now.AddDays(2), token),
        PlayRun = title =>
        {
            played?.Add(title);
            return StoryRunVoice.Recorded(fixtures.Director, Commander, title) is null ? "No lines." : null;
        },
    };

    private static (CapabilityRegistry Registry, KeywordRouter Router) Route(AdventureCapability.AdventureDesk desk)
    {
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);
        return (registry, new KeywordRouter(registry, () => AdventureCapability.RunPhrases(desk)));
    }

    private static async Task ShelveTheFirstStory(StoryFixtures fixtures)
    {
        Assert.Null(await fixtures.Director.PickAsync(Commander, Id, Now, CancellationToken.None));
        Assert.Null(await fixtures.Director.SwitchAsync(Commander, Other.Id, Now.AddDays(1), CancellationToken.None));
    }

    [Fact]
    public async Task SwitchingByTitleShelvesTheActiveStoryAndBringsTheNamedOneBack()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var (registry, router) = Route(Desk(fixtures));

        var command = router.MatchToolCommand($"switch to the story {Card.Title}");
        Assert.Equal(AdventureCapability.SwitchStoryTool, command?.ToolName);

        var result = await registry.InvokeAsync(command!.ToolName, command.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(Id, fixtures.Stories.Current(Commander)!.Id);
        Assert.Equal(StoryState.Shelved, fixtures.Stories.Find(Commander, Other.Id)!.State);
    }

    [Fact]
    public async Task OnlyAShelvedStoryHasASwitchPhrase()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var (_, router) = Route(Desk(fixtures));

        Assert.Null(router.MatchToolCommand($"switch to the story {Other.Title}"));
    }

    [Fact]
    public async Task APlainSwitchToTheTitleIsNotThisPhrase()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var (_, router) = Route(Desk(fixtures));

        Assert.Null(router.MatchToolCommand($"switch to {Card.Title}"));
    }

    [Fact]
    public async Task PlayingBackByTitleOpensARunThatHasLines()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var run = fixtures.Stories.Find(Commander, Id)!;
        var folder = Path.Combine(MemoryInstall.FakeRoot, "play-back");
        fixtures.Director.Records = new StoryRunRecord(
            fixtures.Files, folder, fixtures.Stories, () => Commander, null, new ReversibleProtector(), NullLogger.Instance);
        fixtures.Files.WriteText(
            Path.Combine(folder, StoryRunRecord.FolderName(Commander, Id, run.PickedAt), StoryRunRecord.LinesFile),
            """{"sent":"2026-01-01T00:00:00+00:00","from":"Ship","key":"line-1","body":"Hello."}""" + "\n");

        var played = new List<string>();
        var (registry, router) = Route(Desk(fixtures, played));

        Assert.Null(router.MatchToolCommand($"play back {Other.Title}"));

        var command = router.MatchToolCommand($"play back {Card.Title}");
        Assert.Equal(AdventureCapability.PlayStoryTool, command?.ToolName);

        var result = await registry.InvokeAsync(command!.ToolName, command.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal([Card.Title], played);
    }

    [Fact]
    public async Task ATitleThatIsAlsoACoreNameIsNotTakenForACoreSwitch()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var (_, router) = Route(Desk(fixtures));

        Assert.Equal(AdventureCapability.SwitchStoryTool, router.MatchToolCommand($"switch to the story {Card.Title}")?.ToolName);
        Assert.Null(router.MatchSetting($"switch to the story {Card.Title}"));
    }

    [Fact]
    public async Task TheModelIsRefusedBothTools()
    {
        using var fixtures = Fixtures();
        await ShelveTheFirstStory(fixtures);
        var (registry, router) = Route(Desk(fixtures));
        var arguments = router.MatchToolCommand($"switch to the story {Card.Title}")!.Arguments;

        foreach (var tool in new[] { AdventureCapability.SwitchStoryTool, AdventureCapability.PlayStoryTool })
        {
            var refused = await registry.InvokeAsync(tool, arguments, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(refused.IsError);
        }

        Assert.Equal(Other.Id, fixtures.Stories.Current(Commander)!.Id);
    }
}
