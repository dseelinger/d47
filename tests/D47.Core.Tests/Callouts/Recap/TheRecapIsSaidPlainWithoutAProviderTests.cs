using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>With no model the plain line is spoken; with one, it is always reworded and carries no Backstory.</summary>
public sealed class TheRecapIsSaidPlainWithoutAProviderTests
{
    private static readonly Announcement Line =
        new(RecapCallout.Key, "Last session you finished docked at Garay Terminal, Deciat in the Python.");

    private static Task<Announcement?> Vary(bool hasModel, Action onAsk) =>
        new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            Line,
            hasModel,
            personalityEnabled: true,
            rewordPercent: 0,
            () => ShipFacts.Unknown,
            "Jameson",
            (brief, instruction, token) =>
            {
                onAsk();
                return Task.FromResult(new FlavourReply("Garay Terminal, Deciat, in the Python. Good run.", FlavourMiss.None, null));
            });

    [Fact]
    public async Task WithNoProviderThePlainLineIsSpoken()
    {
        var asked = false;

        var said = await Vary(hasModel: false, () => asked = true);

        Assert.False(asked);
        Assert.Equal(Line.Text, said?.Text);
    }

    [Fact]
    public async Task WithAProviderItIsRewordedWhateverThePercentage()
    {
        var asked = 0;

        _ = await Vary(hasModel: true, () => asked++);

        Assert.Equal(1, asked);
    }

    [Fact]
    public void TheBriefCarriesNoBackstoryAndNoScenario()
    {
        var brief = FlavourBriefs.For(Line, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.False(brief.NeedsAboutMe);
        Assert.False(brief.NeedsScenario);
        Assert.False(brief.NeedsStory);
        Assert.False(brief.NeedsGameState);
    }
}
