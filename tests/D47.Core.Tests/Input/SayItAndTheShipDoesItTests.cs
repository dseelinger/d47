using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The five spoken ship commands (list.md Phase 52).
/// <para>
/// The bare words are the risk this file exists for. <c>engage</c> is a substring of three
/// phrases that were already live — <em>engage supercruise</em>, <em>engage boost</em> and
/// <em>engage the frame shift drive</em> — so a keyword that short would hijack every sentence
/// containing it, which is remediation.md 16 exactly. The router is whole-utterance, and these
/// assert that it stays so: the short phrase and the long ones are different utterances and each
/// reaches its own action.
/// </para>
/// </summary>
public class SayItAndTheShipDoesItTests
{
    private const uint E = 0x45;
    private const uint S = 0x53;
    private const uint B = 0x42;
    private const uint F = 0x46;
    private const uint T = 0x54;

    private static EliteBinds Binds(params (string Action, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(e => new EliteBinding(e.Action, "Primary", "Keyboard", e.Key))],
    };

    /// <summary>Every binding the five commands can reach, so a miss is a miss rather than a gap.</summary>
    private static EliteBinds AllBinds() => Binds(
        ("Hyperspace", "Key_E"),
        ("Supercruise", "Key_S"),
        ("UseBoostJuice", "Key_B"),
        ("SetSpeed100", "Key_T"),
        ("HyperSuperCombination", "Key_F"));

    private static GameStatus Flying(StatusFlags extra = StatusFlags.None) => new()
    {
        Flags = StatusFlags.InMainShip | extra,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>Mass locked, at a stated moment — the two things the boost loop watches.</summary>
    private static GameStatus Locked(double seconds) => new()
    {
        Flags = StatusFlags.InMainShip | StatusFlags.FsdMassLocked,
        ReadAt = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
    };

    private static GameStatus Clear(double seconds) => new()
    {
        Flags = StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
    };

    /// <summary>
    /// A scripted status stream, which is what replaces a clock here. The last sample repeats, so
    /// a stream that never clears is written as the samples that matter rather than as a hundred
    /// copies of the same one.
    /// </summary>
    private static Func<CancellationToken, Task<GameStatus>> Stream(params GameStatus[] samples)
    {
        var next = 0;

        return _ => Task.FromResult(next < samples.Length ? samples[next++] : samples[^1]);
    }

    private sealed record Fixture(CapabilityRegistry Registry, RecordingGameInput Input, KeywordRouter Router);

    private static Fixture Build(EliteBinds binds, GameStatus status, bool enabled = true)
    {
        var input = new RecordingGameInput();

        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => status,
            Input = input,
            Enabled = () => enabled,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        return new Fixture(registry, input, new KeywordRouter(registry));
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);
        Assert.NotNull(match);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    /// <summary>The key a run of steps actually pressed, ignoring the waits.</summary>
    private static uint[] Pressed(RecordingGameInput input) =>
    [
        .. input.Steps
            .Where(step => step.Kind == InputStepKind.KeyDown)
            .Select(step => step.Code),
    ];

    [Fact]
    public async Task EngageOnItsOwnJumps()
    {
        var fixture = Build(AllBinds(), Flying());

        var result = await Say(fixture, "engage");

        Assert.False(result.IsError);
        Assert.Equal([E], Pressed(fixture.Input));
    }

    [Fact]
    public async Task SupercruiseOnItsOwnSupercruises()
    {
        var fixture = Build(AllBinds(), Flying());

        var result = await Say(fixture, "supercruise");

        Assert.False(result.IsError);
        Assert.Equal([S], Pressed(fixture.Input));
    }

    /// <summary>
    /// The other half of the acceptance, and the one that would fail if the bare word were a
    /// keyword rather than a whole utterance.
    /// </summary>
    [Theory]
    [InlineData("engage supercruise", S)]
    [InlineData("engage boost", B)]
    [InlineData("engage the frame shift drive", F)]
    [InlineData("take us to supercruise", S)]
    public async Task ALongerPhraseContainingEngageStillReachesItsOwnAction(string utterance, uint expected)
    {
        var fixture = Build(AllBinds(), Flying());

        var result = await Say(fixture, utterance);

        Assert.False(result.IsError);
        Assert.Equal([expected], Pressed(fixture.Input));
    }

    /// <summary>
    /// A sentence that merely contains the word is not a command. The router falls through to the
    /// model, which is the cheap outcome; acting on it would fly the ship on a question.
    /// </summary>
    [Theory]
    [InlineData("should I engage")]
    [InlineData("what happens when you engage")]
    [InlineData("engage the enemy")]
    [InlineData("is supercruise faster")]
    public void ASentenceThatMerelyContainsTheWordIsNotACommand(string utterance)
    {
        var fixture = Build(AllBinds(), Flying());

        Assert.Null(fixture.Router.MatchToolCommand(utterance));
    }

    /// <summary>
    /// The guard above is only worth having while the overlap it guards against is real, and
    /// nothing else in the suite would notice it going away. If the longer phrases were renamed
    /// so that none of them contained a bare word any more, every assertion here would keep
    /// passing while testing nothing at all — so the overlap itself is asserted.
    /// </summary>
    [Theory]
    [InlineData("engage")]
    [InlineData("supercruise")]
    public void TheBareWordIsGenuinelyContainedInOtherLivePhrases(string bare)
    {
        var longer = (
            from action in GameActions.All
            from phrase in action.Phrases
            where phrase.Phrase.Contains(bare, StringComparison.OrdinalIgnoreCase)
            where !string.Equals(phrase.Phrase, bare, StringComparison.OrdinalIgnoreCase)
            select phrase.Phrase).ToArray();

        Assert.NotEmpty(longer);
    }

    // ---- Separate (list.md Phase 52, item 3) -------------------------------------------------

    private static ActionSurface Surface(RecordingGameInput input, GameStatus status, EliteBinds? binds = null) =>
        new()
        {
            Binds = () => binds ?? AllBinds(),
            Status = () => status,
            Input = input,
            Enabled = () => true,
        };

    /// <summary>
    /// The acceptance the checklist names: a stream that clears the flag on the third sample. The
    /// loop boosts while it is set and finishes the moment it is not, so the count is evidence it
    /// watched rather than waited.
    /// </summary>
    [Fact]
    public async Task SeparateBoostsUntilTheMassLockBreaksAndThenEngages()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, Locked(0)),
            "hyperspace",
            Stream(Locked(1), Locked(2), Clear(3)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal(3, outcome.Boosts);

        // Throttle up, three boosts, then the jump — in that order.
        Assert.Equal([T, B, B, B, E], Pressed(input));
    }

    /// <summary>The other acceptance: a stream that never clears.</summary>
    [Fact]
    public async Task SeparateGivesUpAfterItsCeilingAndSaysWhy()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, Locked(0)),
            "hyperspace",
            Stream(Locked(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.StillMassLocked, outcome.Ending);
        Assert.Equal(4, outcome.Boosts);

        // Four boosts and no jump. The finishing key is the one that must not be pressed here:
        // engaging while still mass locked is the failure this bound exists to prevent.
        Assert.Equal([T, B, B, B, B], Pressed(input));
        Assert.Contains("still mass locked", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("too close to the station", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The wall-clock bound, which is a separate ending from the boost count: a status stream that
    /// keeps reporting locked while its own timestamps run past the ceiling stops the loop even
    /// though there are boosts left.
    /// </summary>
    [Fact]
    public async Task SeparateAlsoStopsWhenTheSamplesRunPastTheCeiling()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, Locked(0)),
            "hyperspace",
            Stream(Locked(30)),
            new SeparationLimits(MaxBoosts: 99, Ceiling: TimeSpan.FromSeconds(20)),
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.StillMassLocked, outcome.Ending);
        Assert.Equal(1, outcome.Boosts);
        Assert.Equal([T, B], Pressed(input));
    }

    /// <summary>Not mass locked at all: no boost, straight to the finish.</summary>
    [Fact]
    public async Task SeparateWithNoMassLockJustEngages()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, Clear(0)),
            "supercruise",
            Stream(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal(0, outcome.Boosts);
        Assert.Equal([T, S], Pressed(input));
    }

    /// <summary>
    /// The second command ends in supercruise whatever the ask said (list.md Phase 52, item 4), so
    /// the two differ in their last key and in nothing else.
    /// </summary>
    [Fact]
    public async Task TheTwoSeparationsDifferOnlyInTheKeyTheyEndOn()
    {
        var jump = new RecordingGameInput();
        var cruise = new RecordingGameInput();

        await Separation.RunAsync(
            Surface(jump, Locked(0)), "hyperspace", Stream(Clear(1)),
            SeparationLimits.Default, TestContext.Current.CancellationToken);

        await Separation.RunAsync(
            Surface(cruise, Locked(0)), "supercruise", Stream(Clear(1)),
            SeparationLimits.Default, TestContext.Current.CancellationToken);

        Assert.Equal([T, B, E], Pressed(jump));
        Assert.Equal([T, B, S], Pressed(cruise));
    }

    /// <summary>
    /// All the bindings or none. A ship left accelerating at a station because the sequence got
    /// half way and found no boost binding is worse than one that never started.
    /// </summary>
    [Fact]
    public async Task SeparateWithAMissingBindingPressesNothingAtAll()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, Locked(0), Binds(("Hyperspace", "Key_E"), ("SetSpeed100", "Key_T"))),
            "hyperspace",
            Stream(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    /// <summary>
    /// No status file means no flag to watch. Boosting to the ceiling and engaging anyway would be
    /// the sequence working by accident.
    /// </summary>
    [Fact]
    public async Task SeparateWithNoStatusAtAllRefusesRatherThanGuessing()
    {
        var input = new RecordingGameInput();

        var outcome = await Separation.RunAsync(
            Surface(input, GameStatus.Unknown),
            "hyperspace",
            Stream(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    /// <summary>
    /// Interrupted half way through, everything held is released. Unconditional, in a finally, and
    /// the reason architecture.md D4 gives: a stranded key here is a throttle that will not stop.
    /// </summary>
    [Fact]
    public async Task AnInterruptedSeparationReleasesWhatItWasHolding()
    {
        var input = new RecordingGameInput();
        using var cancelled = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Separation.RunAsync(
            Surface(input, Locked(0)),
            "hyperspace",
            _ =>
            {
                cancelled.Cancel();
                cancelled.Token.ThrowIfCancellationRequested();
                return Task.FromResult(Clear(1));
            },
            SeparationLimits.Default,
            cancelled.Token));

        Assert.True(input.ReleaseAllCalls > 0);
    }
}
