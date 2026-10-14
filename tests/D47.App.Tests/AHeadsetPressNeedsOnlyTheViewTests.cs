using D47.App.Input;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A switch's headset press is carried out off duty and with no Elite window in front: it reads only the view
/// (#1153).
/// </summary>
public class AHeadsetPressNeedsOnlyTheViewTests
{
    private sealed class FixedView(HeadsetView showing) : IHeadsetViewSource
    {
        public HeadsetView Read() => showing;
    }

    private static HeadsetViewKey Key(HeadsetView showing) =>
        new(new FixedView(showing), NullLogger<HeadsetViewKey>.Instance) { DryRun = true };

    private static SwitchReconciler Reconciler() => new(NullLogger<SwitchReconciler>.Instance);

    private static PendingReconcile Press(HeadsetView view) => new("view switch", "SteamVR", [], View: view);

    [Fact]
    public void AHeadsetPressIsKeptOffDutyAndAKeyPressIsNot()
    {
        var keys = new PendingReconcile("gear switch", "the landing gear", [new InputStep(InputStepKind.KeyDown, 0x47)]);
        var page = new PendingReconcile("transcript switch", "Log File", [], Destination: "transcript.log");

        var route = ReconcileRoute.Of([keys, Press(HeadsetView.Vr), page], offDuty: true);

        Assert.Equal(HeadsetView.Vr, Assert.Single(route.Headset).View);
        Assert.Equal("transcript.log", Assert.Single(route.Pages).Destination);
        Assert.Empty(route.Game);
    }

    [Fact]
    public void OnDutyEachReconcileGoesOneWay()
    {
        var keys = new PendingReconcile("gear switch", "the landing gear", [new InputStep(InputStepKind.KeyDown, 0x47)]);

        var route = ReconcileRoute.Of([keys, Press(HeadsetView.Desktop)], offDuty: false);

        Assert.Equal("gear switch", Assert.Single(route.Game).Switch);
        Assert.Equal("view switch", Assert.Single(route.Headset).Switch);
        Assert.Empty(route.Pages);
    }

    [Fact]
    public void ThePressIsSentAgainstTheOtherViewWithNoEliteWindow()
    {
        // The key has no window and no game status to ask, so nothing about Elite can refuse it.
        var key = Key(HeadsetView.Desktop);

        Assert.Null(key.CarryOut(Press(HeadsetView.Vr), Reconciler()));
        Assert.Equal(1, key.Presses);
    }

    [Fact]
    public void NothingIsPressedWhenTheViewIsAlreadyThere()
    {
        var key = Key(HeadsetView.Vr);

        Assert.Null(key.CarryOut(Press(HeadsetView.Vr), Reconciler()));
        Assert.Equal(0, key.Presses);
    }

    [Fact]
    public void APressNotSentLeavesTheSwitchUnpaused()
    {
        // Decided against SteamVR; by the time it is carried out the headset is already on the desktop.
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance, new FixedView(HeadsetView.Vr));
        var mapping = new SwitchMapping
        {
            Name = "view switch",
            DeviceId = "d",
            Positions = [new SwitchPosition(4, View: HeadsetView.Vr), new SwitchPosition(5, View: HeadsetView.Desktop)],
        };

        reconciler.Poll(Tick(4, 0), [mapping]);
        reconciler.Poll(Tick(5, 1), [mapping]);

        var press = Assert.Single(reconciler.Drain());
        Assert.Null(Key(HeadsetView.Desktop).CarryOut(press, reconciler));

        reconciler.Poll(Tick(5, 2), [mapping]);
        reconciler.Poll(Tick(5, 2 + SwitchReconciler.ContestWindow.TotalSeconds + 1), [mapping]);

        Assert.False(reconciler.IsPaused("view switch"));
    }

    private static SwitchTick Tick(int button, double seconds)
    {
        var buttons = new bool[32];
        buttons[button] = true;

        return new SwitchTick
        {
            Now = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
            Readings = [new HotasReading { Id = "d", Buttons = buttons }],
            Status = new D47.Core.Journal.GameStatus { ReadAt = DateTimeOffset.UnixEpoch },
            Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
            Enabled = false,
            HeadsetEnabled = true,
        };
    }

    [Fact]
    public void AnUnreadableViewIsRefusedOutLoud()
    {
        var key = Key(HeadsetView.Unknown);

        var said = key.CarryOut(Press(HeadsetView.Vr), Reconciler());

        Assert.Equal(
            "I could not change the headset to SteamVR from view switch. "
            + "Virtual Desktop stopped streaming, so I left the headset view alone.",
            said);
        Assert.Equal(0, key.Presses);
    }

    [Fact]
    public void APauseIsSaidAndNotPressed()
    {
        var key = Key(HeadsetView.Vr);

        Assert.Equal("paused", key.CarryOut(new PendingReconcile("view switch", "the desktop view", [], "paused", View: HeadsetView.Desktop), Reconciler()));
        Assert.Equal(0, key.Presses);
    }
}
