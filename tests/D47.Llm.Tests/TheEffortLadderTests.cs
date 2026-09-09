using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>Five rungs, in order, and what each becomes on the wire.</summary>
public class TheEffortLadderTests
{
    [Fact]
    public void TheLadderIsInOrder()
    {
        Assert.Equal(
            [
                ThinkingEffort.Low,
                ThinkingEffort.Medium,
                ThinkingEffort.High,
                ThinkingEffort.Xhigh,
                ThinkingEffort.Max,
            ],
            Enum.GetValues<ThinkingEffort>());
    }

    [Fact]
    public void XhighSitsBetweenHighAndMax()
    {
        Assert.True(ThinkingEffort.Xhigh > ThinkingEffort.High);
        Assert.True(ThinkingEffort.Xhigh < ThinkingEffort.Max);
    }

    [Fact]
    public void ThereIsNoOff()
    {
        Assert.DoesNotContain(
            Enum.GetNames<ThinkingEffort>(),
            name => name is "Off" or "None");
    }
}
