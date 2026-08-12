using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class RetryPolicyTests
{
    private static RetryPolicy Policy(BackoffShape shape, int attempts = 4) => new()
    {
        Attempts = attempts,
        Wait = TimeSpan.FromSeconds(2),
        Backoff = shape,
    };

    [Fact]
    public void TheFirstAttemptNeverWaits()
    {
        Assert.Equal(TimeSpan.Zero, Policy(BackoffShape.Sequential).WaitBefore(1));
        Assert.Equal(TimeSpan.Zero, Policy(BackoffShape.Logarithmic).WaitBefore(1));
    }

    [Fact]
    public void SequentialGrowsByTheBaseEachTime()
    {
        var policy = Policy(BackoffShape.Sequential);

        Assert.Equal(TimeSpan.FromSeconds(2), policy.WaitBefore(2));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.WaitBefore(3));
        Assert.Equal(TimeSpan.FromSeconds(6), policy.WaitBefore(4));
    }

    [Fact]
    public void LogarithmicGrowsButDecelerates()
    {
        var policy = Policy(BackoffShape.Logarithmic);

        var first = policy.WaitBefore(2);
        var second = policy.WaitBefore(3);
        var third = policy.WaitBefore(4);

        Assert.Equal(TimeSpan.FromSeconds(2), first);
        Assert.True(second > first);
        Assert.True(third > second);

        // The point of the shape: each step up is smaller than the one before it.
        Assert.True(second - first > third - second);
    }

    [Fact]
    public void BothShapesAgreeOnTheFirstRetry()
    {
        Assert.Equal(
            Policy(BackoffShape.Sequential).WaitBefore(2),
            Policy(BackoffShape.Logarithmic).WaitBefore(2));
    }

    [Fact]
    public void OneAttemptMeansNoRetriesAndNoWaiting()
    {
        var policy = Policy(BackoffShape.Sequential, attempts: 1);

        Assert.Equal(TimeSpan.Zero, policy.TotalWait());
    }

    [Fact]
    public void TotalWaitIsWhatTheCommanderWouldSitThrough()
    {
        // 0 + 2 + 4 + 6 for four attempts.
        Assert.Equal(TimeSpan.FromSeconds(12), Policy(BackoffShape.Sequential).TotalWait());
    }
}
