using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>The five spoken ship commands.</summary>
public class SayItAndTheShipDoesItTests
{
    private const uint E = 0x45;
    private const uint S = 0x53;
    private const uint B = 0x42;
    private const uint F = 0x46;
    private const uint T = 0x54;
    private const uint P = 0x50;
    private const uint G = 0x47;

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

    /// <summary>A scripted status stream and the clock that runs beside it.</summary>
    private sealed class Samples(params GameStatus[] samples)
    {
        /// <summary>The app's Status.json poll interval, which is what one sample costs.</summary>
        private static readonly TimeSpan PerSample = TimeSpan.FromMilliseconds(250);

        private int _next;

        public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

        public Task<GameStatus> Next(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Now += PerSample;

            return Task.FromResult(_next < samples.Length ? samples[_next++] : samples[^1]);
        }

        public Func<DateTimeOffset> Clock => () => Now;
    }

    /// <summary>The common case: samples that stay locked until one clears.</summary>
    private static Samples LockedUntilClear(int locked) =>
        new([.. Enumerable.Repeat(Locked(0), locked), Clear(0)]);

    /// <summary>
    /// <see cref="Separation.RunAsync"/> with the stream and its clock supplied together, since a test
    /// that scripts one is always scripting both.
    /// </summary>
    private static Task<SeparationOutcome> RunSeparation(
        ActionSurface actions,
        string finisherId,
        Samples samples,
        SeparationLimits limits,
        CancellationToken cancellationToken) =>
        Separation.RunAsync(actions, finisherId, samples.Next, samples.Clock, limits, cancellationToken);

    private sealed record Fixture(CapabilityRegistry Registry, RecordingGameInput Input, KeywordRouter Router);

    private static Fixture Build(
        EliteBinds binds,
        GameStatus status,
        bool enabled = true,
        bool targeted = false)
    {
        var input = new RecordingGameInput();

        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => status,
            Input = input,
            Enabled = () => enabled,
            SystemTargeted = () => targeted,
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
    /// The other half of the acceptance, and the one that would fail if the bare word were a keyword
    /// rather than a whole utterance.
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
    /// The guard above is only worth having while the overlap it guards against is real, and nothing
    /// else in the suite would notice it going away.
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

 // ---- Separate -------------------------------------------------

    private static ActionSurface Surface(
        RecordingGameInput input,
        GameStatus status,
        EliteBinds? binds = null,
        bool targeted = false) =>
        new()
        {
            Binds = () => binds ?? AllBinds(),
            Status = () => status,
            Input = input,
            Enabled = () => true,
            SystemTargeted = () => targeted,
        };

    /// <summary>The acceptance the checklist names: a stream that clears the flag on the third sample.</summary>
    [Fact]
    public async Task SeparateBoostsUntilTheMassLockBreaksAndThenEngages()
    {
        var input = new RecordingGameInput();

        // A boost a second, and a sample is 250 ms, so each boost costs four samples.
        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            LockedUntilClear(11),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal(3, outcome.Boosts);

        // Throttle up, three boosts, then the jump — in that order.
        Assert.Equal([T, B, B, B, E], Pressed(input));
    }

    /// <summary>The boosts are spaced rather than fired on consecutive samples.</summary>
    [Fact]
    public async Task SeparateLeavesTheDistributorTimeToRefillBetweenBoosts()
    {
        var input = new RecordingGameInput();

        // Samples a quarter-second apart, the way the app really supplies them, running past the one-second
        // spacing once.
        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            new Samples(Locked(0.25), Locked(0.5), Locked(0.75), Locked(1), Clear(1.25)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal(2, outcome.Boosts);
        Assert.Equal([T, B, B, E], Pressed(input));
    }

    /// <summary>The other acceptance: a stream that never clears.</summary>
    [Fact]
    public async Task SeparateGivesUpAfterItsCeilingAndSaysWhy()
    {
        var input = new RecordingGameInput();

        // A minute and a half of samples that stay locked, a second apart: the boost count is a runaway guard
        // set high enough that what stops the loop is the ceiling.
        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            new Samples([.. Enumerable.Range(1, 90).Select(step => Locked(step))]),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.StillMassLocked, outcome.Ending);

        // A boost a second from zero to sixty inclusive, and no jump.
        Assert.Equal(61, outcome.Boosts);
        Assert.Equal([T, .. Enumerable.Repeat(B, 61)], Pressed(input));
        Assert.Contains("still mass locked", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("too close to the station", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The defect this loop shipped with, and the reason it is paced on a clock.</summary>
    [Fact]
    public async Task BoostsKeepComingWhileStatusJsonsTimestampStandsStill()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            new Samples(Locked(0)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.StillMassLocked, outcome.Ending);

        // A boost a second from zero to sixty inclusive.
        Assert.Equal(61, outcome.Boosts);
    }

    /// <summary>All pips to engines before the first boost.</summary>
    [Fact]
    public async Task SeparatePutsThePipsInTheEnginesFirst()
    {
        var input = new RecordingGameInput();

        var binds = Binds(
            ("Hyperspace", "Key_E"),
            ("UseBoostJuice", "Key_B"),
            ("SetSpeed100", "Key_T"),
            ("IncreaseEnginesPower", "Key_P"));

        var outcome = await RunSeparation(
            Surface(input, Locked(0), binds),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);

        // Four presses reaches the maximum from any split, and a press at the maximum does nothing — so this
        // needs no read of where the pips started.
        Assert.Equal([P, P, P, P, T, B, E], Pressed(input));
    }

    /// <summary>
    /// Not mass locked at the start and the power settings are left alone entirely — no pips moved, no
    /// boost pressed.
    /// </summary>
    [Fact]
    public async Task WithNoMassLockThePipsAreLeftWhereTheCommanderPutThem()
    {
        var input = new RecordingGameInput();

        var binds = Binds(
            ("Hyperspace", "Key_E"),
            ("UseBoostJuice", "Key_B"),
            ("SetSpeed100", "Key_T"),
            ("IncreaseEnginesPower", "Key_P"));

        var outcome = await RunSeparation(
            Surface(input, Clear(0), binds),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([T, E], Pressed(input));
    }

    /// <summary>
    /// The gear stops everything else working: deployed, the throttle is capped, boost does nothing and
    /// the drive will not engage.
    /// </summary>
    [Fact]
    public async Task SeparateRaisesTheLandingGearBeforeAnythingElse()
    {
        var input = new RecordingGameInput();

        var binds = Binds(
            ("Hyperspace", "Key_E"),
            ("UseBoostJuice", "Key_B"),
            ("SetSpeed100", "Key_T"),
            ("IncreaseEnginesPower", "Key_P"),
            ("LandingGearToggle", "Key_G"));

        var down = new GameStatus
        {
            Flags = StatusFlags.InMainShip | StatusFlags.FsdMassLocked | StatusFlags.LandingGearDown,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        var outcome = await RunSeparation(
            Surface(input, down, binds),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([G, P, P, P, P, T, B, E], Pressed(input));
    }

    /// <summary>Gear already up: the key is not touched, and its binding is never needed.</summary>
    [Fact]
    public async Task GearThatIsAlreadyUpIsLeftAlone()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Clear(0)),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([T, E], Pressed(input));
    }

    /// <summary>Gear down with no binding to raise it is a refusal, not a best-effort.</summary>
    [Fact]
    public async Task GearDownWithNoBindingRefusesBeforePressingAnything()
    {
        var input = new RecordingGameInput();

        var down = new GameStatus
        {
            Flags = StatusFlags.InMainShip | StatusFlags.LandingGearDown,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        var outcome = await RunSeparation(
            Surface(input, down),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("landing gear", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And the pips are the one part of the sequence that is best-effort.</summary>
    [Fact]
    public async Task WithNoPipsBindingTheSeparationStillRuns()
    {
        var input = new RecordingGameInput();

        // Mass locked, so the pips are wanted — and unbindable, so they are skipped rather than refused.
        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([T, B, E], Pressed(input));
    }

    /// <summary>
    /// The ceiling is a separate ending from the boost count, and it is the one that should stop a lock
    /// that never breaks: with the spacing removed the loop boosts on every sample, and twenty seconds
    /// of them ends it well short of the ninety-boost guard.
    /// </summary>
    [Fact]
    public async Task SeparateStopsAtTheCeilingRatherThanTheBoostCount()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0)),
            "hyperspace",
            new Samples(Locked(0)),
            new SeparationLimits(MaxBoosts: 99, Ceiling: TimeSpan.FromSeconds(20), BoostSpacing: TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.StillMassLocked, outcome.Ending);

        // Twenty seconds of quarter-second samples, and the count never came into it.
        Assert.Equal(81, outcome.Boosts);
        Assert.True(outcome.Boosts < 99);
    }

    /// <summary>Not mass locked at all: no boost, straight to the finish.</summary>
    [Fact]
    public async Task SeparateWithNoMassLockJustEngages()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Clear(0)),
            "supercruise",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal(0, outcome.Boosts);
        Assert.Equal([T, S], Pressed(input));
    }

    /// <summary>
 /// The second command ends in supercruise whatever the ask said, so the two
    /// differ in their last key and in nothing else.
    /// </summary>
    [Fact]
    public async Task TheTwoSeparationsDifferOnlyInTheKeyTheyEndOn()
    {
        var jump = new RecordingGameInput();
        var cruise = new RecordingGameInput();

        await RunSeparation(
            Surface(jump, Locked(0)), "hyperspace", new Samples(Clear(1)),
            SeparationLimits.Default, TestContext.Current.CancellationToken);

        await RunSeparation(
            Surface(cruise, Locked(0)), "supercruise", new Samples(Clear(1)),
            SeparationLimits.Default, TestContext.Current.CancellationToken);

        Assert.Equal([T, B, E], Pressed(jump));
        Assert.Equal([T, B, S], Pressed(cruise));
    }

    /// <summary>All the bindings or none.</summary>
    [Fact]
    public async Task SeparateWithAMissingBindingPressesNothingAtAll()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0), Binds(("Hyperspace", "Key_E"), ("SetSpeed100", "Key_T"))),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    /// <summary>No status file means no flag to watch.</summary>
    [Fact]
    public async Task SeparateWithNoStatusAtAllRefusesRatherThanGuessing()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, GameStatus.Unknown),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    /// <summary>Interrupted half way through, everything held is released.</summary>
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
            () => DateTimeOffset.UnixEpoch,
            SeparationLimits.Default,
            cancelled.Token));

        Assert.True(input.ReleaseAllCalls > 0);
    }

 // ---- The combined FSD key as a fallback ------------------------------------

    /// <summary>
    /// The binds off the ship this was heard on: the dedicated jump on a hat, the combined FSD key on a
    /// keyboard key, and everything the separation needs besides.
    /// </summary>
    private static EliteBinds JumpOnTheStick() => Binds(
        ("Hyperspace", "Joy_POV1Left"),
        ("HyperSuperCombination", "Key_F"),
        ("Supercruise", "Key_S"),
        ("UseBoostJuice", "Key_B"),
        ("SetSpeed100", "Key_T"));

    /// <summary>
    /// The defect itself: J makes this jump, so refusing it because the hat cannot be pressed refuses
    /// something the ship plainly does.
    /// </summary>
    [Fact]
    public async Task SeparateFallsBackToTheCombinedKeyWhenTheJumpIsOnTheStick()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0), JumpOnTheStick(), targeted: true),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([T, B, F], Pressed(input));
    }

    /// <summary>The guard.</summary>
    [Fact]
    public async Task WithNothingTargetedTheFallbackIsRefusedAndSaysToSetACourse()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0), JumpOnTheStick(), targeted: false),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("Set a course", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>With neither pressable the refusal names both, so the Commander can bind either.</summary>
    [Fact]
    public async Task WithNeitherJumpBindPressableTheRefusalNamesBoth()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(
                input,
                Locked(0),
                Binds(("Hyperspace", "Joy_POV1Left"), ("UseBoostJuice", "Key_B"), ("SetSpeed100", "Key_T")),
                targeted: true),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("hyperspace jump is on your joystick", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("frame shift drive key is not bound", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No fallback the other way, and this is the case that would be dangerous: with a system targeted
    /// the combined key jumps, so a separate and supercruise reaching for it would fly the opposite
    /// manoeuvre to the one that was asked for.
    /// </summary>
    [Fact]
    public async Task SeparateAndSupercruiseNeverReachesForTheCombinedKey()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(
                input,
                Locked(0),
                Binds(
                    ("Supercruise", "Joy_POV1Left"),
                    ("HyperSuperCombination", "Key_F"),
                    ("UseBoostJuice", "Key_B"),
                    ("SetSpeed100", "Key_T")),
                targeted: true),
            "supercruise",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    /// <summary>The dedicated key wins whenever it can be pressed.</summary>
    [Fact]
    public async Task APressableHyperspaceBindIsStillTheOnePressed()
    {
        var input = new RecordingGameInput();

        var outcome = await RunSeparation(
            Surface(input, Locked(0), AllBinds(), targeted: true),
            "hyperspace",
            new Samples(Clear(1)),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(SeparationEnding.Away, outcome.Ending);
        Assert.Equal([T, B, E], Pressed(input));
    }

    /// <summary>
    /// The same fallback under the same guard for the jump asked for by name, so the two paths cannot
    /// drift apart into two different answers to one question.
    /// </summary>
    [Theory]
    [InlineData("engage")]
    [InlineData("hyperspace jump")]
    public async Task TheSpokenJumpTakesTheSameFallback(string utterance)
    {
        var targeted = Build(JumpOnTheStick(), Flying(), targeted: true);

        var jumped = await Say(targeted, utterance);

        Assert.False(jumped.IsError);
        Assert.Equal([F], Pressed(targeted.Input));

        var untargeted = Build(JumpOnTheStick(), Flying(), targeted: false);

        var refused = await Say(untargeted, utterance);

        Assert.True(refused.IsError);
        Assert.Empty(Pressed(untargeted.Input));
        Assert.Contains("Set a course", refused.Content, StringComparison.Ordinal);
    }

 // ---- Take us out ----------------------------------------------

    private const uint Panel = 0x31;
    private const uint Back = 0x42;
    private const uint Down = 0x53;
    private const uint Select = 0x5A;

    private static EliteBinds PanelBinds() => Binds(
        ("FocusLeftPanel", "Key_1"),
        ("UI_Back", "Key_B"),
        ("UI_Down", "Key_S"),
        ("UI_Select", "Key_Z"));

    private static GameStatus Docked(GuiFocus focus = GuiFocus.None) => new()
    {
        Flags = StatusFlags.InMainShip | StatusFlags.Docked,
        GuiFocus = focus,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static Func<bool, CancellationToken, Task<bool?>> Panels(bool? answer) => (_, _) => Task.FromResult(answer);

    private static Func<CancellationToken, Task<bool?>> Undocks(bool? answer) => _ => Task.FromResult(answer);

    /// <summary>The whole walk, in order: open, back out to the tabs, down into the list, select.</summary>
    [Fact]
    public async Task TakeUsOutOpensTheLeftPanelAndWalksItToTheLaunchItem()
    {
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), PanelBinds()),
            Panels(true),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Launched, outcome.Ending);

        Assert.Equal([Panel, Back, Down, Select], Pressed(input));
    }

    /// <summary>
    /// A panel that is already open is not toggled shut first — and already open means the left one,
    /// which reports <c>ExternalPanel</c>.
    /// </summary>
    [Fact]
    public async Task TakeUsOutDoesNotReopenAPanelThatIsAlreadyShowing()
    {
        var input = new RecordingGameInput();

        await Launch.RunAsync(
            Surface(input, Docked(GuiFocus.ExternalPanel), PanelBinds()),
            Panels(true),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(Panel, Pressed(input));
        Assert.Equal(Back, Pressed(input)[0]);
    }

    /// <summary>The whole of #106's first fault, pinned as a value.</summary>
    [Fact]
    public void TheLeftPanelIsTheExternalOneWhateverTheNamesSuggest()
    {
        Assert.Equal(GuiFocus.ExternalPanel, Launch.Panel);
        Assert.Equal(2, (int)Launch.Panel);
    }

    /// <summary>
    /// A right panel showing is not the left one being open, which is the reading that failed in the
    /// field: the macro must press its key rather than assume it is already there.
    /// </summary>
    [Fact]
    public async Task TakeUsOutOpensTheLeftPanelEvenWhenTheRightOneIsShowing()
    {
        var input = new RecordingGameInput();

        await Launch.RunAsync(
            Surface(input, Docked(GuiFocus.InternalPanel), PanelBinds()),
            Panels(true),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal([Panel, Back, Down, Select], Pressed(input));
    }

    /// <summary>Down and select wait for the panel to actually go (#106, second report).</summary>
    [Fact]
    public async Task TakeUsOutWaitsForThePanelToCloseBeforeReachingTheStationMenu()
    {
        var input = new RecordingGameInput();
        var pressedWhenAsked = new List<int>();

        // Records how much had been pressed at each check, which is the assertion: nothing beyond back has
        // gone out by the time the close is awaited.
        Task<bool?> Watching(bool open, CancellationToken token)
        {
            pressedWhenAsked.Add(Pressed(input).Length);
            return Task.FromResult<bool?>(true);
        }

        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), PanelBinds()),
            Watching,
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Launched, outcome.Ending);
        Assert.Equal([Panel, Back, Down, Select], Pressed(input));

        // Asked twice: that the panel opened, then that it closed.
        Assert.Equal([1, 2], pressedWhenAsked);
    }

    /// <summary>And a panel that will not close stops the walk there: down and select with no station menu in front of them are flight controls typed into a docked ship.</summary>
    [Fact]
    public async Task TakeUsOutStopsIfBackLeavesThePanelOpen()
    {
        var input = new RecordingGameInput();

        // Open when asked whether it opened, still open when asked whether it closed.
        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), PanelBinds()),
            (open, _) => Task.FromResult<bool?>(open),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Refused, outcome.Ending);
        Assert.Equal([Panel, Back], Pressed(input));
        Assert.Contains("stayed open", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Not docked is a refusal rather than an attempt.</summary>
    [Fact]
    public async Task TakeUsOutInSpacePressesNothing()
    {
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            Surface(input, Flying(), PanelBinds()),
            Panels(true),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("not docked", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The direction keys are only safe inside the panel.</summary>
    [Fact]
    public async Task TakeUsOutStopsIfThePanelNeverOpens()
    {
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), PanelBinds()),
            Panels(false),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Refused, outcome.Ending);
        Assert.Equal([Panel], Pressed(input));
    }

    /// <summary>
    /// Still docked afterwards is its own answer, and it is the reason the macro verifies at all:
    /// believing the ship launched when it did not leaves a Commander talking to a docked ship.
    /// </summary>
    [Theory]
    [InlineData(false, LaunchEnding.StillDocked)]
    [InlineData(null, LaunchEnding.Unknown)]
    public async Task TakeUsOutSaysSoWhenTheShipDidNotLeave(bool? undocked, LaunchEnding expected)
    {
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), PanelBinds()),
            Panels(true),
            Undocks(undocked),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, outcome.Ending);
        Assert.False(outcome.Ok);
    }

    /// <summary>All four bindings or none, before a key is sent.</summary>
    [Fact]
    public async Task TakeUsOutWithNoSelectBindingPressesNothingAtAll()
    {
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            Surface(input, Docked(), Binds(("FocusLeftPanel", "Key_1"), ("UI_Back", "Key_B"), ("UI_Down", "Key_S"))),
            Panels(true),
            Undocks(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

 // ---- The route in, and the switches ---------------------------

    private static CapabilityRegistry Commands(
        RecordingGameInput input,
        GameStatus status,
        bool keyboard = true,
        bool permitted = true,
        Samples? stream = null,
        IClipboard? clipboard = null,
        LastFoundSystem? lastFound = null)
    {
        var samples = stream ?? new Samples(Clear(0));

        return CapabilityRegistry.Build(ActionCapabilities.All(
            new ActionSurface
            {
                Binds = () => Binds(
                    ("Hyperspace", "Key_E"),
                    ("Supercruise", "Key_S"),
                    ("UseBoostJuice", "Key_B"),
                    ("SetSpeed100", "Key_T"),
                    ("FocusLeftPanel", "Key_1"),
                    ("UI_Back", "Key_B"),
                    ("UI_Down", "Key_S"),
                    ("UI_Select", "Key_Z")),
                Status = () => status,
                Input = input,
                Enabled = () => keyboard,
            },
            new ShipCommandSurface
            {
                Enabled = _ => permitted,
                AwaitLeftPanel = (_, _) => Task.FromResult<bool?>(true),
                AwaitUndocked = _ => Task.FromResult<bool?>(true),
                NextStatus = samples.Next,
                Now = samples.Clock,
            },

            // Auto-plot switched off, so "set a course and take us out" only ever writes the clipboard and
            // never tries to drive the galaxy map — the launch half is what these tests are about.
            NavigationSurface.Inert with { Clipboard = clipboard ?? new RecordingClipboard() },
            lastFound ?? new LastFoundSystem()));
    }

    [Theory]
    [InlineData("take us out", ShipCommands.TakeUsOut)]
    [InlineData("separate and engage", ShipCommands.SeparateAndEngage)]
    [InlineData("get us clear and jump", ShipCommands.SeparateAndEngage)]
    [InlineData("separate and supercruise", ShipCommands.SeparateAndSupercruise)]
    [InlineData("set a course and take us out", ShipCommands.SetCourseAndTakeUsOut)]

    // The whole pattern: one dropped word is the difference between the boost loop and a bare jump key pressed against a mass lock.
    [InlineData("get clear and jump", ShipCommands.SeparateAndEngage)]
    [InlineData("get clear and engage", ShipCommands.SeparateAndEngage)]
    [InlineData("get clear and hyperspace", ShipCommands.SeparateAndEngage)]
    [InlineData("get us clear and engage", ShipCommands.SeparateAndEngage)]
    [InlineData("get us clear and hyperspace", ShipCommands.SeparateAndEngage)]
    [InlineData("separate and jump", ShipCommands.SeparateAndEngage)]
    [InlineData("separate and hyperspace", ShipCommands.SeparateAndEngage)]
    [InlineData("boost and jump", ShipCommands.SeparateAndEngage)]
    [InlineData("boost and engage", ShipCommands.SeparateAndEngage)]
    [InlineData("boost and hyperspace", ShipCommands.SeparateAndEngage)]
    public void EachCompoundCommandHasAModelFreeRouteIn(string utterance, string command)
    {
        var registry = Commands(new RecordingGameInput(), Docked());
        var match = new KeywordRouter(registry).MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal("ship_command", match.ToolName);
        Assert.True(match.Arguments.TryGetString("command", out var named));
        Assert.Equal(command, named);
    }

    /// <summary>
    /// These reach the ship, so the model never sees them: it reads untrusted text, and a hostile
    /// in-game message that can make d47 boost out of a station is a different category of problem from
    /// one that can turn the lights on.
    /// </summary>
    [Fact]
    public void TheCompoundCommandsAreNeverAdvertisedToTheModel()
    {
        var registry = Commands(new RecordingGameInput(), Docked());

        var tool = registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Single(t => t.Name == "ship_command");

        Assert.True(tool.Protected);

        foreach (var profile in ToolProfiles.All(registry))
        {
            Assert.DoesNotContain(profile.Tools, advert => advert.Name == "ship_command");
        }
    }

    /// <summary>
    /// Two gates and two different sentences. "You have not let me press keys at all" and "you have not
    /// let me do this one" send the Commander to different rows.
    /// </summary>
    [Theory]
    [InlineData(false, true, "switched off")]
    [InlineData(true, false, "its own row")]
    public async Task ACommandThatIsSwitchedOffPressesNothingAndSaysWhichSwitch(
        bool keyboard, bool permitted, string expected)
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Docked(), keyboard, permitted);

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.TakeUsOut,
            }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(Pressed(input));
        Assert.Contains(expected, result.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Switched on, the routed command reaches the sequence and presses real keys.</summary>
    [Fact]
    public async Task ASwitchedOnCommandRunsTheWholeSequence()
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Flying(StatusFlags.FsdMassLocked), stream: new Samples(Clear(0)));

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.SeparateAndSupercruise,
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal([T, B, S], Pressed(input));
    }

 // ---- Set a course and take us out ------------------------------------------

    /// <summary>Nothing found yet: the command errors and presses nothing.</summary>
    [Fact]
    public async Task SetCourseAndTakeUsOutWithNothingFoundRefuses()
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Docked(), lastFound: new LastFoundSystem());

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.SetCourseAndTakeUsOut,
            }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(Pressed(input));
    }

    /// <summary>
    /// With something found and a working clipboard, it plots first — the sentence says the system is
    /// on the clipboard — and then goes on to walk the launch sequence.
    /// </summary>
    [Fact]
    public async Task SetCourseAndTakeUsOutPlotsThenLaunches()
    {
        var input = new RecordingGameInput();
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");

        var registry = Commands(
            input, Docked(), stream: new Samples(Clear(0)), lastFound: lastFound);

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.SetCourseAndTakeUsOut,
            }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("HR 6012 is on your clipboard", result.Content, StringComparison.Ordinal);
        Assert.Equal([Panel, Back, Down, Select], Pressed(input));

        // A plot outcome, so heard as written on the model route the same way plot_course itself is (#112).
        Assert.True(result.Relayed);
    }

    /// <summary>
    /// A clipboard that cannot be written to is the one thing <c>plot_course</c> treats as an actual
    /// error, and this is best-effort by design: rather than launch towards a course that was never
    /// even offered to the Commander, the compound command stops there.
    /// </summary>
    [Fact]
    public async Task SetCourseAndTakeUsOutDoesNotLaunchIfTheClipboardFails()
    {
        var input = new RecordingGameInput();
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");

        var registry = Commands(
            input, Docked(), clipboard: new RecordingClipboard { Works = false }, lastFound: lastFound);

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.SetCourseAndTakeUsOut,
            }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(Pressed(input));
    }

    /// <summary>Elite has no launch control, and the catalogue must not grow one.</summary>
    [Fact]
    public void NoActionClaimsALaunchBindingEliteDoesNotHave()
    {
        var invented = (
            from action in GameActions.All
            from variant in action.Variants
            where variant.EliteAction.Contains("launch", StringComparison.OrdinalIgnoreCase)
               || variant.EliteAction.Contains("undock", StringComparison.OrdinalIgnoreCase)
            select variant.EliteAction).ToArray();

        Assert.Empty(invented);
    }
}
