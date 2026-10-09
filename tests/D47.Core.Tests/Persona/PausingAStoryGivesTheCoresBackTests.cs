using D47.Core.Persona;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Persona;

[Trait("Category", "Integration")]
public sealed class PausingAStoryGivesTheCoresBackTests
{
    private static StoryFixtures Fixtures() => new(new RoundScriptedLlmProvider(
        RoundScriptedLlmProvider.Saying(Spine),
        RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

    private static async Task<StoryFixtures> Picked()
    {
        var fixtures = Fixtures();
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        Assert.Equal(HeldCores.All, fixtures.Cores("F1").Hold.Cores);
        return fixtures;
    }

    private static void PauseTheChapter(StoryFixtures fixtures, DateTimeOffset at)
    {
        var key = fixtures.Stories.Current("F1")!.CurrentChapter!;
        Assert.Null(fixtures.Book.Abandon("F1", key, at));
        Assert.Null(fixtures.Director.Tick("F1", at));
    }

    [Fact]
    public async Task PausingBeforeTheScanGivesThemBackAndResumingHoldsThemAgain()
    {
        using var fixtures = await Picked();

        PauseTheChapter(fixtures, Now.AddHours(1));
        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);

        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddHours(2), CancellationToken.None));
        Assert.Equal(HeldCores.All, fixtures.Cores("F1").Hold.Cores);
    }

    [Fact]
    public async Task ResumingAfterTheScanDoesNotHoldThemAgain()
    {
        using var fixtures = await Picked();
        fixtures.ScanBeacon("F1", BeaconAddress, Now.AddHours(1));

        PauseTheChapter(fixtures, Now.AddHours(2));
        Assert.Null(await fixtures.Director.ResumeAsync("F1", Now.AddHours(3), CancellationToken.None));

        Assert.True(fixtures.Cores("F1").IsAwake(PersonaCatalog.Warden));
    }

    [Fact]
    public async Task SwitchingTheStoryOffGivesThemBack()
    {
        using var fixtures = await Picked();

        Assert.Null(fixtures.Director.SetOn("F1", on: false, Now.AddHours(1)));
        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);

        Assert.Null(fixtures.Director.SetOn("F1", on: true, Now.AddHours(2)));
        Assert.Equal(HeldCores.All, fixtures.Cores("F1").Hold.Cores);
    }

    [Fact]
    public async Task AbandoningTheStoryGivesThemBack()
    {
        using var fixtures = await Picked();

        Assert.Null(fixtures.Director.Abandon("F1", Now.AddHours(1)));

        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);
        Assert.True(fixtures.Cores("F1").IsAwake(PersonaCatalog.Heretic));
    }

    [Fact]
    public async Task ASessionWithoutOdysseyGivesThemBack()
    {
        using var fixtures = await Picked();

        fixtures.Director.Observe(
            AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now.AddHours(1))}}", "event":"LoadGame", "FID":"F1", "Commander":"X", "Horizons":true, "Odyssey":false }"""),
            "F1");

        Assert.Equal(CoreHold.None, fixtures.Cores("F1").Hold);
    }
}
