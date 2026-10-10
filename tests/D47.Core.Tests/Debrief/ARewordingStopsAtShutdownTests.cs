using D47.Core.Debrief;
using Xunit;

namespace D47.Core.Tests.Debrief;

[Trait("Category", "Integration")]
public sealed class ARewordingStopsAtShutdownTests
{
    [Fact]
    public async Task WithTheDebriefOffTheModelIsNotAsked()
    {
        using var debrief = Drafted();
        var asked = 0;

        debrief.Switch(on: false);

        await debrief.Host.Reword((_, _) =>
        {
            asked++;
            return Task.FromResult<string?>("Do not mention my rank.");
        });

        Assert.Equal(0, asked);
        Assert.Single(DebriefRewording.Pending(debrief.Store));
    }

    [Fact]
    public async Task ACancelledRewordingEndsWithoutWritingTheProposal()
    {
        using var debrief = Drafted();
        var drafted = Direction(debrief);
        var asking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var rewording = debrief.Host.Reword(async (_, token) =>
        {
            asking.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return "Do not mention my rank.";
        });

        await asking.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        debrief.Host.CancelRewording();

        await rewording.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(drafted, Direction(debrief));
        Assert.False(Direction(debrief).Reworded);
    }

    private static StandingDirection Direction(ADebriefOnDisk debrief) =>
        Assert.Single(debrief.Open().For(ADebriefOnDisk.Commander), entry => entry.Kind == DirectionKind.Direction);

    /// <summary>A debrief whose last session drafted one direction, waiting to be reworded.</summary>
    private static ADebriefOnDisk Drafted()
    {
        var debrief = new ADebriefOnDisk();

        debrief.Host.NoteTurn(ADebriefOnDisk.Correction, "Understood, Commander.");
        debrief.Host.Run(ADebriefOnDisk.Commander);

        Assert.Single(DebriefRewording.Pending(debrief.Store));
        return debrief;
    }
}
