using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Callouts the game caused and that write quickly are spoken in the order drained (#2476); an urgent line and a
/// composed line wait for nobody and hold up nobody.
/// </summary>
public sealed class AQuickCalloutIsSpokenInTheOrderQueuedTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static readonly Announcement Reaction =
        new(StoryReactionCallout.Key(new StoryReactionDue("respawn", 5)), string.Empty);

    [Fact]
    public void UrgentAndComposedLinesLeaveTheOrderedBatchAndTheRestKeepTheirOrder()
    {
        var first = new Announcement("cargo.full", "Cargo hold full.");
        var urgent = new Announcement("danger.interdicted", "Interdiction.", CalloutUrgency.Urgent);
        var narration = new Announcement(NarratorCallout.Key, string.Empty);
        var second = new Announcement("fuel.scooped", "Tanks full.");

        var (urgentLines, composed, ordered) = CalloutDispatch.Split([first, urgent, Reaction, narration, second]);

        Assert.Equal([urgent], urgentLines);
        Assert.Equal([Reaction, narration], composed);
        Assert.Equal([first, second], ordered);
    }

    [Fact]
    public void AStoryChapterLineSaidAsWrittenIsNotComposed()
    {
        Assert.False(LineWriter.IsComposed(new Announcement("adventure.some-story.1", "Hold here, Commander.")));
        Assert.True(LineWriter.IsComposed(new Announcement(NpcChatter.KeyPrefix + "hail", string.Empty)));
    }

    [Fact]
    public async Task ARoutineLineDrainedLaterIsSpokenAfterOneDrainedBesideAReactionAndBeforeTheReaction()
    {
        var reactionWritten = new TaskCompletionSource<IReadOnlyList<Announcement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (dispatch, spoken, turn) = Dispatch(announcement =>
            announcement == Reaction ? reactionWritten.Task : Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            var firstLine = new Announcement("cargo.full", "Cargo hold full.");
            var secondLine = new Announcement("fuel.scooped", "Tanks full.");

            var firstBatch = dispatch.Dispatch([firstLine, Reaction]);
            var secondBatch = dispatch.Dispatch([secondLine]);

            await WaitUntil(() => Spoken(spoken).Count == 2);

            reactionWritten.SetResult([Reaction with { Text = "That landing will be remembered." }]);
            await Task.WhenAll(firstBatch, secondBatch).WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["Cargo hold full.", "Tanks full.", "That landing will be remembered."], Spoken(spoken));
        }
    }

    [Fact]
    public async Task AQuickLineWaitsForTheRewordingOfALineDrainedBeforeIt()
    {
        var firstWritten = new TaskCompletionSource<IReadOnlyList<Announcement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLine = new Announcement("cargo.full", "Cargo hold full.");
        var (dispatch, spoken, turn) = Dispatch(announcement =>
            announcement == firstLine ? firstWritten.Task : Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            var firstBatch = dispatch.Dispatch([firstLine]);
            var secondBatch = dispatch.Dispatch([new Announcement("fuel.scooped", "Tanks full.")]);

            await Task.Delay(50, TestContext.Current.CancellationToken);
            Assert.Empty(Spoken(spoken));

            firstWritten.SetResult([firstLine with { Text = "The hold is full." }]);
            await Task.WhenAll(firstBatch, secondBatch).WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["The hold is full.", "Tanks full."], Spoken(spoken));
        }
    }

    [Fact]
    public async Task ABatchWhoseWritingThrowsOrComesBackEmptyDoesNotHoldUpTheNext()
    {
        var throwing = new Announcement("cargo.full", "Cargo hold full.");
        var empty = new Announcement("ambient.quiet", string.Empty);
        var (dispatch, spoken, turn) = Dispatch(announcement =>
            announcement == throwing
                ? Task.FromException<IReadOnlyList<Announcement>>(new InvalidOperationException("No writer."))
                : announcement == empty
                    ? Task.FromResult<IReadOnlyList<Announcement>>([])
                    : Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            await Task.WhenAll(
                    dispatch.Dispatch([throwing]),
                    dispatch.Dispatch([empty]),
                    dispatch.Dispatch([new Announcement("fuel.scooped", "Tanks full.")]))
                .WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["Tanks full."], Spoken(spoken));
        }
    }

    [Fact]
    public async Task AnNpcsFirstLineTakesItsPlaceOnceItsNameIsAnswered()
    {
        var answer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var npc = new Announcement("message.npc", "Hello there.") { Speaker = "Dan Roberts", Voice = VoiceRole.Comms };
        var (dispatch, spoken, turn) = Dispatch(
            announcement => Task.FromResult<IReadOnlyList<Announcement>>([announcement]),
            announcement => announcement == npc ? answer.Task : null);

        using (turn)
        using (dispatch)
        {
            var firstBatch = dispatch.Dispatch([npc, new Announcement("cargo.full", "Cargo hold full.")]);
            var secondBatch = dispatch.Dispatch([new Announcement("fuel.scooped", "Tanks full.")]);

            await WaitUntil(() => Spoken(spoken).Count == 2);

            answer.SetResult();
            await Task.WhenAll(firstBatch, secondBatch).WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["Cargo hold full.", "Tanks full.", "Hello there."], Spoken(spoken));
        }
    }

    [Fact]
    public async Task AnUrgentLineWaitsForNoPlaceAndIsCountedUntilSpoken()
    {
        var slowWritten = new TaskCompletionSource<IReadOnlyList<Announcement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new Announcement("cargo.full", "Cargo hold full.");
        var (dispatch, spoken, turn) = Dispatch(announcement =>
            announcement == slow ? slowWritten.Task : Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            var chatterHolding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var chatterRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var chatter = turn.TakeAsync(async () =>
            {
                chatterHolding.SetResult();
                await chatterRelease.Task.ConfigureAwait(false);
            });

            await chatterHolding.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
            Assert.False(dispatch.UrgentUnspoken);

            var slowBatch = dispatch.Dispatch([slow]);
            var urgentBatch = dispatch.Dispatch([new Announcement("danger.interdicted", "Interdiction.", CalloutUrgency.Urgent)]);

            Assert.True(dispatch.UrgentUnspoken);

            chatterRelease.SetResult();
            await Task.WhenAll(chatter, urgentBatch).WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["Interdiction."], Spoken(spoken));
            Assert.False(dispatch.UrgentUnspoken);

            slowWritten.SetResult([slow]);
            await slowBatch.WaitAsync(Patience, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ARegionFirstIsSpokenBeforeTheDiscoveryLineEvenWhileItIsBeingWritten()
    {
        var written = new TaskCompletionSource<IReadOnlyList<Announcement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var regionFirst = new Announcement(RegionFirstCallout.KeyPrefix + "1.18", "May be a first.", CalloutUrgency.Urgent);
        var discovered = new Announcement("codex-discovered.1", "Discovered a plant.");
        var (dispatch, spoken, turn) = Dispatch(announcement =>
            announcement == regionFirst ? written.Task : Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            var batch = dispatch.Dispatch([discovered, regionFirst]);

            await Task.Delay(50, TestContext.Current.CancellationToken);
            Assert.Empty(Spoken(spoken));
            Assert.True(dispatch.UrgentUnspoken);

            written.SetResult([regionFirst with { Text = "A first, Commander." }]);
            await batch.WaitAsync(Patience, TestContext.Current.CancellationToken);

            Assert.Equal(["A first, Commander.", "Discovered a plant."], Spoken(spoken));
            Assert.False(dispatch.UrgentUnspoken);
        }
    }

    [Fact]
    public async Task AChatterBeatIsAbandonedWhenAnUrgentLineIsWaitingForTheTurn()
    {
        var (dispatch, _, turn) = Dispatch(announcement => Task.FromResult<IReadOnlyList<Announcement>>([announcement]));

        using (turn)
        using (dispatch)
        {
            var beatStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var chatter = turn.TakeAsync(async () =>
            {
                beatStarted.SetResult();
                await Announcer.HoldTheBeatAsync(TimeSpan.FromMinutes(10), () => dispatch.UrgentUnspoken).ConfigureAwait(false);
            });

            await beatStarted.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);

            var urgent = dispatch.Dispatch([new Announcement("danger.interdicted", "Interdiction.", CalloutUrgency.Urgent)]);

            await Task.WhenAll(chatter, urgent).WaitAsync(Patience, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ABeatEndsAtOnceWhenAnUrgentLineIsAlreadyWaiting()
    {
        await Announcer.HoldTheBeatAsync(TimeSpan.FromMinutes(10), () => true)
            .WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    private static (CalloutDispatch Dispatch, List<string> Spoken, SpeakingTurn Turn) Dispatch(
        Func<Announcement, Task<IReadOnlyList<Announcement>>> write,
        Func<Announcement, Task?>? nameAnswer = null)
    {
        var turn = new SpeakingTurn();
        var spoken = new List<string>();

        var dispatch = new CalloutDispatch(
            write,
            lines => turn.TakeAsync(() =>
            {
                lock (spoken)
                {
                    spoken.AddRange(lines.Select(line => line.Text));
                }

                return Task.CompletedTask;
            }),
            nameAnswer ?? (_ => null),
            NullLogger.Instance);

        return (dispatch, spoken, turn);
    }

    private static List<string> Spoken(List<string> spoken)
    {
        lock (spoken)
        {
            return [.. spoken];
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var patience = new CancellationTokenSource(Patience);

        while (!condition())
        {
            await Task.Delay(10, patience.Token);
        }
    }
}
