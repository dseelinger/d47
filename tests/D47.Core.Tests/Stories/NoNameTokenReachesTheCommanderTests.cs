using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Every {name:} token is replaced with the version's name before the chapter writer or any speaker reads it.</summary>
public sealed class NoNameTokenReachesTheCommanderTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheChapterWriterReadsTheResolvedName()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            Versioned);
        fixtures.Director.Gender = () => CommanderGender.Woman;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("Ellis is the Commander's sister.", prompt);
        Assert.DoesNotContain("{name:", prompt);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheHiddenBriefHoldsNoToken()
    {
        using var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)),
            Versioned);
        fixtures.Director.Gender = () => CommanderGender.Man;

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Director.HiddenBrief("F1");

        Assert.Contains("Ellie is the Commander's sister.", brief);
        Assert.DoesNotContain("{name:", brief);
    }

    [Fact]
    public void EveryHiddenTextIsResolved()
    {
        var token = Versioned with
        {
            End = "{name:cray} stays.",
            Beats = Versioned.Beats with { Midpoint = "{name:cray} remembers." },
            Finale = [Versioned.Finale[0] with { Text = "{name:cray} speaks." }, .. Versioned.Finale.Skip(1)],
            Options = [Versioned.Options[0] with { Label = "Keep {name:cray}", After = "{name:cray} stays aboard." }],
        };

        var resolved = token.For(CommanderGender.Man);

        Assert.All(resolved.Texts(), text => Assert.DoesNotContain("{name:", text.Text));
        Assert.Equal("Keep Ellie", resolved.Options[0].Label);
    }
}
