using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// The Commander's gender picks which version of a two-version member they meet: its name in every hidden line, and
/// its provider, voice and picture. A story with such a member cannot be picked until the gender is set.
/// </summary>
public sealed class TheCommandersGenderPicksTheVersionTests
{
    private static StoryFixtures Running(string? gender, out string? refusal)
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            Versioned);

        fixtures.Director.Gender = () => gender;
        refusal = fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None).GetAwaiter().GetResult();
        return fixtures;
    }

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(CommanderGender.Man, "Ellie hums a song from home.")]
    [InlineData(CommanderGender.Woman, "Ellis hums a song from home.")]
    public void AClueNamesTheVersionTheCommanderMeets(string gender, string clue)
    {
        using var fixtures = Running(gender, out var refusal);

        Assert.Null(refusal);
        Assert.Equal(clue, fixtures.Director.Clue("F1", new StoryClueDue(Id, 0))?.Clue);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheLookupGivesTheManTheForManVersion()
    {
        using var fixtures = Running(CommanderGender.Man, out _);

        Assert.Equal(
            new StorySpeakerShown("cray", "Ellie", StorySpeaker.Kokoro, "af_heart", $"{Id}.cray.for-man"),
            fixtures.Director.Speaker("F1", "cray"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheLookupGivesTheWomanTheForWomanVersion()
    {
        using var fixtures = Running(CommanderGender.Woman, out _);

        Assert.Equal(
            new StorySpeakerShown("cray", "Ellis", StorySpeaker.Chatterbox, "ellis-sample", $"{Id}.cray.for-woman"),
            fixtures.Director.Speaker("F1", "cray"));
    }

    [Fact]
    public void AMemberWithOneVersionIsTheSameForEveryone()
    {
        Assert.Equal(
            new StorySpeakerShown("dock-hand", "Ren", StorySpeaker.Kokoro, "bm_george", $"{Id}.dock-hand"),
            Versioned.Speaker("dock-hand", CommanderGender.Woman));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ChangingTheGenderTakesEffectFromTheNextLine()
    {
        var gender = CommanderGender.Man;
        using var fixtures = Running(gender, out _);
        fixtures.Director.Gender = () => gender;

        Assert.Equal("Ellie hums a song from home.", fixtures.Director.Clue("F1", new StoryClueDue(Id, 0))?.Clue);

        gender = CommanderGender.Woman;

        Assert.Equal("Ellis hums a song from home.", fixtures.Director.Clue("F1", new StoryClueDue(Id, 0))?.Clue);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void PickWaitsForTheGender()
    {
        using var fixtures = Running(null, out var refusal);

        Assert.True(fixtures.Director.NeedsGenderFor(Id));
        Assert.Equal(StoryDirector.NeedsGender, refusal);
        Assert.Null(fixtures.Stories.Current("F1"));
        Assert.Empty(fixtures.Provider.Requests);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AStoryWithoutVersionsNeedsNoGender()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        Assert.False(fixtures.Director.NeedsGenderFor(Id));
    }
}
