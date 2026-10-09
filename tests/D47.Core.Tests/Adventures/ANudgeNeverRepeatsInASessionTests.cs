using D47.Core.Callouts;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>An adventure is nudged at most once per session, even when the nudge was never recorded.</summary>
public sealed class ANudgeNeverRepeatsInASessionTests
{
    private readonly AStalledStoryGetsANudgeTests _stalled = new();

    [Fact]
    public void TheNextNarrationAfterANudgeIsAnOrdinaryOne()
    {
        var narrator = AStalledStoryGetsANudgeTests.Narrator(_stalled.Stalled(2, 4, 6));
        var first = AStalledStoryGetsANudgeTests.FirstNarration(narrator, Accepted.AddDays(8));

        var later = Assert.Single(narrator.Examine(AStalledStoryGetsANudgeTests.At(Accepted.AddDays(8).AddHours(3))));

        Assert.StartsWith(NarratorCallout.NudgePrefix, first.Key, StringComparison.Ordinal);
        Assert.Equal(NarratorCallout.Key, later.Key);
    }
}
