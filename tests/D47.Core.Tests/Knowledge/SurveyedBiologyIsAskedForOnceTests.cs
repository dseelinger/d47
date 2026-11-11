using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class SurveyedBiologyIsAskedForOnceTests
{
    private const long System = 99;

    [Fact]
    public async Task TwoReadersOfOneSystemShareOneAskAndTheAnswerIsHeld()
    {
        var held = new SurveyedBiology();
        var release = new TaskCompletionSource<SystemBiology>();
        var asked = 0;

        Task<SystemBiology> Ask(long address, CancellationToken token)
        {
            Interlocked.Increment(ref asked);

            return release.Task;
        }

        var first = held.GetAsync(System, Ask, TestContext.Current.CancellationToken);
        var second = held.GetAsync(System, Ask, TestContext.Current.CancellationToken);
        var answer = new SystemBiology(System, []);
        release.SetResult(answer);

        Assert.Same(answer, await first);
        Assert.Same(answer, await second);
        Assert.Same(answer, await held.GetAsync(System, Ask, TestContext.Current.CancellationToken));
        Assert.Equal(1, asked);
        Assert.True(held.TryGet(System, out _));
    }

    [Fact]
    public async Task AFailedAskReturnsNullAndHoldsNothing()
    {
        var held = new SurveyedBiology();

        var answer = await held.GetAsync(
            System, (_, _) => throw new HttpRequestException(), TestContext.Current.CancellationToken);

        Assert.Null(answer);
        Assert.False(held.TryGet(System, out _));
    }
}
