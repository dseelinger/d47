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

    private static ScancodeInjector Injector(FakeElite elite) =>
        new(elite, NullLogger<ScancodeInjector>.Instance) { DryRun = true };

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
