using D47.Core.Adventures;
using D47.Core.Conversation;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

public sealed class AChapterWithACallPastItsLimitIsOverLimitTests
{
    private static MeteredRequest Call(string model, double seconds, int budget) =>
        new(model, LlmUsage.None, 0m, (long)(seconds * 1000), null, MaxOutputTokens: budget);

    [Fact]
    public void ABeatsCallThatRanOneHundredAndThirtySecondsIsWithinItsLimit()
    {
        Assert.Equal("written", StoryComparisonTests.OutcomeOf(false, true, [Call("claude-sonnet-5-5", 130, AdventureGenerator.BeatsOutputCeiling)]));
    }

    [Fact]
    public void ABeatsCallThatRanFourHundredAndOneSecondsIsOverItsLimit()
    {
        Assert.Equal("over limit", StoryComparisonTests.OutcomeOf(false, true, [Call("claude-sonnet-5-5", 401, AdventureGenerator.BeatsOutputCeiling)]));
    }

    [Fact]
    public void ASpineCallThatRanTwoHundredAndOneSecondsIsOverItsLimit()
    {
        Assert.Equal("over limit", StoryComparisonTests.OutcomeOf(false, true, [Call("claude-sonnet-5-5", 201, AdventureGenerator.SpineOutputCeiling)]));
    }

    [Fact]
    public void AChapterNotWrittenStaysNotWrittenWhateverItsCallsTook()
    {
        Assert.Equal("not written", StoryComparisonTests.OutcomeOf(false, false, [Call("claude-sonnet-5-5", 401, AdventureGenerator.BeatsOutputCeiling)]));
    }

    [Fact]
    public void TheLimitIsNeverUnderTwoMinutes()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), AdventureGenerator.CallLimitFor(8192));
        Assert.Equal(TimeSpan.FromSeconds(200), AdventureGenerator.CallLimitFor(16000));
        Assert.Equal(TimeSpan.FromSeconds(400), AdventureGenerator.CallLimitFor(32000));
    }
}
