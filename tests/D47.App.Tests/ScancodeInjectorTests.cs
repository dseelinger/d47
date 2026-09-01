using D47.Core.Capabilities.Builtin;
using D47.App.Input;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The injector's three load-bearing rules from architecture.md D4, driven in dry-run mode so
/// the real composition path runs and nothing reaches the system (architecture.md §8).
/// <para>
/// The refusals are the important half. A voice command that types into a browser is the
/// failure this component exists to prevent, and it is not observable by running the real
/// thing and watching.
/// </para>
/// </summary>
public class ScancodeInjectorTests
{
    private sealed class FakeElite : IEliteWindow
    {
        public bool IsRunning { get; set; } = true;

        public bool IsForeground { get; set; } = true;

        /// <summary>
        /// Nothing here raises anything. Injection never asks — a command that quietly brought
        /// the game forward so it could type into it would defeat the foreground check these
        /// tests are about, so this failing loudly if it were ever called is the point.
        /// </summary>
        /// <summary>Not this test's subject: the injector never asks where Elite's window is.</summary>
        public (int X, int Y, int Width, int Height)? Bounds => null;

        public FocusResult Raise() =>
            throw new InvalidOperationException("the injector must never raise the game itself");
    }

    // No status by default, which is the old contract and stays a real one: the harness and the
    // diagnostics card drive the injector with no Status.json to read (#242). Every test above
    // the online ones doubles as proof that a status-less injector refuses nothing new.
    private static ScancodeInjector Injector(FakeElite elite, Func<D47.Core.Journal.GameStatus>? status = null) =>
        new(elite, NullLogger<ScancodeInjector>.Instance, status) { DryRun = true };

    private static IReadOnlyList<InputStep> Tap() =>
        InputSequence.Tap(new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L"));

    [Fact]
    public async Task NothingIsSentWhenEliteIsNotTheWindowInFront()
    {
        var elite = new FakeElite { IsForeground = false };

        var result = await Injector(elite).SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.Equal(InjectionOutcome.NotForeground, result.Outcome);
        Assert.False(result.Sent);
        Assert.Contains("in front", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NothingIsSentWhenEliteIsNotRunning()
    {
        var elite = new FakeElite { IsRunning = false };

        var result = await Injector(elite).SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.Equal(InjectionOutcome.GameNotFound, result.Outcome);
    }

    [Fact]
    public async Task AnEmptySequenceIsReportedRatherThanSentAsNothing()
    {
        var result = await Injector(new FakeElite()).SendAsync([], TestContext.Current.CancellationToken);

        Assert.Equal(InjectionOutcome.NothingToSend, result.Outcome);
    }

    [Fact]
    public async Task AForegroundEliteGetsTheWholeSequence()
    {
        using var injector = Injector(new FakeElite());

        var result = await injector.SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.True(result.Sent);
        Assert.Equal(Tap(), injector.LastSequence);
    }

    /// <summary>
    /// Running is not the same as being in the game (#242). At the main menu the window is
    /// there and holds the foreground, and a keystroke aimed at a ship lands in a menu.
    /// </summary>
    [Fact]
    public async Task NothingIsSentAtTheMainMenu()
    {
        var menu = D47.Core.Journal.GameStatus.Unknown with
        {
            Flags = D47.Core.Journal.StatusFlags.None,
            ReadAt = DateTimeOffset.Now,
        };

        var result = await Injector(new FakeElite(), () => menu)
            .SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.Equal(InjectionOutcome.NotOnline, result.Outcome);
        Assert.False(result.Sent);
        Assert.Contains("not in the game", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A status file from yesterday still says "in the ship", because IsKnown is one-way and
    /// nothing ever unsets it — so old is treated as the menu, not as the game.
    /// </summary>
    [Fact]
    public async Task AStaleStatusFileDoesNotCountAsBeingInTheGame()
    {
        var yesterday = D47.Core.Journal.GameStatus.Unknown with
        {
            Flags = D47.Core.Journal.StatusFlags.InMainShip,
            ReadAt = DateTimeOffset.Now - TimeSpan.FromHours(20),
        };

        var result = await Injector(new FakeElite(), () => yesterday)
            .SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.Equal(InjectionOutcome.NotOnline, result.Outcome);
    }

    /// <summary>Going online is what enables the game-dependent features (#242).</summary>
    [Fact]
    public async Task GoingOnlineIsWhatTurnsTheKeysBackOn()
    {
        var aboard = D47.Core.Journal.GameStatus.Unknown with
        {
            Flags = D47.Core.Journal.StatusFlags.InMainShip,
            ReadAt = DateTimeOffset.Now,
        };

        using var injector = Injector(new FakeElite(), () => aboard);

        var result = await injector.SendAsync(Tap(), TestContext.Current.CancellationToken);

        Assert.True(result.Sent);
    }

    [Fact]
    public async Task LosingTheForegroundPartWayThroughStopsTheSequence()
    {
        // A hold can span a second, and the Commander alt-tabbing mid-hold must not carry on
        // pressing keys into whatever is now in front.
        var elite = new FakeElite();
        using var injector = Injector(elite);

        var steps = new List<InputStep>
        {
            new(InputStepKind.KeyDown, 0x4C),
            InputStep.Wait(TimeSpan.FromMilliseconds(1)),
            new(InputStepKind.KeyUp, 0x4C),
        };

        var sending = injector.SendAsync(steps, TestContext.Current.CancellationToken);
        elite.IsForeground = false;

        var result = await sending;

        Assert.Equal(InjectionOutcome.NotForeground, result.Outcome);
    }

    /// <summary>
    /// And it lets go <em>during</em> the hold, not at the end of it
    /// (<a href="https://github.com/dseelinger/d47/issues/206">#206</a>). The honk is 5.3
    /// seconds of a Commander's own modifier held down, fired on arrival with nothing asked
    /// for; every shortcut pressed meanwhile reaches Windows as a different chord.
    /// </summary>
    [Fact]
    public async Task ALongHoldLetsGoTheMomentEliteStopsBeingInFront()
    {
        var elite = new FakeElite();
        using var injector = Injector(elite);

        // The honk's shape, two seconds instead of 5.3 so the test is quick: a modifier down,
        // the key under it, the charge, then the releases.
        var hold = TimeSpan.FromSeconds(2);

        var steps = new List<InputStep>
        {
            new(InputStepKind.KeyDown, 0xA2),
            new(InputStepKind.KeyDown, 0x58),
            InputStep.Wait(hold),
            new(InputStepKind.KeyUp, 0x58),
            new(InputStepKind.KeyUp, 0xA2),
        };

        var ran = System.Diagnostics.Stopwatch.StartNew();
        var sending = injector.SendAsync(steps, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        elite.IsForeground = false;

        var result = await sending;
        ran.Stop();

        Assert.Equal(InjectionOutcome.NotForeground, result.Outcome);

        // The outcome on its own proved nothing before this fix: the step after the wait
        // reported it too, a whole hold later. The elapsed time is the property.
        Assert.True(ran.Elapsed < hold, $"the hold ran on for {ran.Elapsed} of {hold}");
    }

    /// <summary>Leaving the game mid-hold ends it on the same terms (#206, #242).</summary>
    [Fact]
    public async Task ALongHoldLetsGoWhenTheCommanderLeavesTheGame()
    {
        var aboard = D47.Core.Journal.GameStatus.Unknown with
        {
            Flags = D47.Core.Journal.StatusFlags.InMainShip,
            ReadAt = DateTimeOffset.Now,
        };

        var live = aboard;

        using var injector = Injector(new FakeElite(), () => live);

        var hold = TimeSpan.FromSeconds(2);

        var steps = new List<InputStep>
        {
            new(InputStepKind.KeyDown, 0x58),
            InputStep.Wait(hold),
            new(InputStepKind.KeyUp, 0x58),
        };

        var ran = System.Diagnostics.Stopwatch.StartNew();
        var sending = injector.SendAsync(steps, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        live = aboard with { Flags = D47.Core.Journal.StatusFlags.None };

        var result = await sending;
        ran.Stop();

        Assert.Equal(InjectionOutcome.NotOnline, result.Outcome);
        Assert.True(ran.Elapsed < hold, $"the hold ran on for {ran.Elapsed} of {hold}");
    }

    /// <summary>
    /// A hold nothing interrupts still lasts as long as it was asked to (#206) — the watch
    /// wakes about a hundred times across the honk, and the scanner needs the whole 5.3
    /// seconds. What this catches is a watch that cuts a hold short or stretches it out of
    /// recognition; the per-wake rounding it is written to avoid is smaller than any tolerance
    /// a suite can assert in under a second, and was measured too small to fail this test even
    /// with the naive version in place.
    /// </summary>
    [Fact]
    public async Task AnUninterruptedHoldStillLastsAsLongAsItWasAsked()
    {
        using var injector = Injector(new FakeElite());

        var hold = TimeSpan.FromMilliseconds(600);

        var ran = System.Diagnostics.Stopwatch.StartNew();

        var result = await injector.SendAsync(
            [new InputStep(InputStepKind.KeyDown, 0x58), InputStep.Wait(hold), new InputStep(InputStepKind.KeyUp, 0x58)],
            TestContext.Current.CancellationToken);

        ran.Stop();

        Assert.True(result.Sent);
        Assert.True(ran.Elapsed >= hold, $"the hold ended early, at {ran.Elapsed} of {hold}");

        // Generous, because a loaded machine can lose a slice anywhere: what this refuses is
        // the accumulated rounding, which would be measured in hundreds of milliseconds.
        Assert.True(ran.Elapsed < hold + TimeSpan.FromMilliseconds(300), $"the hold overran, at {ran.Elapsed}");
    }

    [Fact]
    public async Task ReleaseAllRunsAfterEverySendWhetherOrNotItWorked()
    {
        // Unconditional means in a finally, so a send that failed part-way still lets go. The
        // observable version of that here is that a second release finds nothing left to do.
        using var injector = Injector(new FakeElite());

        await injector.SendAsync(Tap(), TestContext.Current.CancellationToken);

        injector.ReleaseAll();
        injector.ReleaseAll();
    }

    [Fact]
    public void ReleaseAllIsSafeWithNothingHeld()
    {
        // Called from a finally, a focus-loss handler and a shutdown path, none of which
        // coordinate with each other.
        using var injector = Injector(new FakeElite());

        injector.ReleaseAll();
        injector.ReleaseAll();
    }

    [Fact]
    public async Task ADryRunSendsNothingButStillComposesTheSequence()
    {
        using var injector = Injector(new FakeElite());

        await injector.SendAsync(InputSequence.Tap(
            new EliteBinding("PrimaryFire", "Primary", "Mouse", "Mouse_1")),
            TestContext.Current.CancellationToken);

        Assert.Contains(injector.LastSequence, step => step.Kind == InputStepKind.MouseDown);
        Assert.Contains(injector.LastSequence, step => step.Kind == InputStepKind.MouseUp);
    }

    [Fact]
    public async Task TextIsSentAsTextRatherThanAsKeystrokes()
    {
        using var injector = Injector(new FakeElite());

        await injector.SendAsync([InputStep.Type("Shinrarta Dezhra")], TestContext.Current.CancellationToken);

        var step = Assert.Single(injector.LastSequence);
        Assert.Equal(InputStepKind.Text, step.Kind);
        Assert.Equal("Shinrarta Dezhra", step.Text);
    }
}
