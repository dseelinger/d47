using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>The chapter writer and every flavour line carry the one hidden layer the director gives out.</summary>
public sealed class EverySpeakerReadsTheSameHiddenStoryTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheChapterWriterReadsTheDirectorsHiddenLayer()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var brief = fixtures.Director.HiddenBrief("F1");

        Assert.NotNull(brief);
        Assert.Contains(brief, fixtures.Provider.Requests[0].Prompt.History[0].Text);
        Assert.DoesNotContain(Secret.Clues[0].Text, brief);
    }

    [Fact]
    public async Task AFlavourLineCarriesTheHiddenLayerInItsSystemBlock()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying("A line."));
        var brief = StoryClues.Brief(
            new Story { Id = Id, Title = Card.Title, PublicLayer = Card.Describe(), PickedAt = Now },
            Secret);

        await FlavourTurn.AskAsync(
            provider, null, "A persona.", null, "Say something.", null, null, null, null, TestContext.Current.CancellationToken, hiddenStory: brief);

        Assert.Contains(brief, provider.Requests[0].Prompt.RenderCachedSystemBlock());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void WithNoStoryThereIsNoHiddenLayer()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider());

        Assert.Null(fixtures.Director.HiddenBrief("F1"));
        Assert.DoesNotContain("hidden story", new PromptAssembly().RenderCachedSystemBlock(), StringComparison.OrdinalIgnoreCase);
    }
}
