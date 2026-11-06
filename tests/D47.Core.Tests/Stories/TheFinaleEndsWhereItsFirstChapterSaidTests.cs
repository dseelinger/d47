using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A middle finale chapter's writer is told where the finale ends, how far that is, and how many finale chapters are left.</summary>
public sealed class TheFinaleEndsWhereItsFirstChapterSaidTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly AdventureDestination Beta = new(3, "Fixture Beta", 2, "Fixture Beta 2");

    [Fact]
    public async Task TheBriefNamesTheDestinationItsDistanceAndTheChaptersLeft()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(8)),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));
        var ask = PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { FinaleChapter = 3, FinaleChapters = 4, Destination = Beta, Stage = "Finale" });

        var galaxy = new PlannedChapterWorld.Galaxy();
        galaxy.Positions["Fixture Beta"] = new StarPosition(120, 0, 0);

        var outcome = await PlannedChapterWorld.Generator(provider, galaxy).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var brief = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("The story's finale ends on Fixture Beta 2, in Fixture Beta, 20 light years from the Commander's position. One finale chapter is left after this one.", brief);
        Assert.Contains("the longest one hop may be. A place farther away is reached over several hops.", brief);
        Assert.DoesNotContain("\"destination\":", brief);
    }
}
