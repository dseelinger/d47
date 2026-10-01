using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>An option that adds a core says that core's waking line once, and unlocks nothing.</summary>
public sealed class AnEndingThatAddsACoreSpeaksItsWakingTests
{
    [Fact]
    public void TheHereticWakesWithItsOwnLine()
    {
        var secret = Secret with { Options = [new StoryOption { Id = "free", Label = "Free it", After = "It is free.", Add = ["heretic"] }] };

        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(), secret);

        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-400),
            State = StoryState.Finished,
            StoppedAt = Now,
        });
        fixtures.Director.EndingPosted("F1", Id, Now);

        var answer = fixtures.Director.Answer("F1", null);

        Assert.Null(answer.Refusal);
        Assert.Equal("It is free.", answer.After);
        Assert.Equal([GuardianCores.Line(CoreWaking.Heretic)], answer.Wakings);
        Assert.Empty(fixtures.Director.Answer("F1", null).Wakings);
        Assert.Equal(HeldCores.None, fixtures.Stories.Find("F1", Id)!.HeldCores);
    }
}
