using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A switch position can mean a headset view, pressed as Shift+Win+D only against the other view (#1153).</summary>
public class ASwitchSetsTheHeadsetViewTests
{
    private const string Stick = "{wgi/nrid/throttle}";

    private sealed class FakeView : IHeadsetViewSource
    {
        public HeadsetView Showing { get; set; } = HeadsetView.Vr;

        public int Reads { get; private set; }

        public HeadsetView Read()
        {
            Reads++;
            return Showing;
        }
    }

    /// <summary>Button 4 is VR, 5 is the desktop.</summary>
    private static SwitchMapping ViewSwitch() => new()
    {
        Name = "view switch",
        DeviceId = Stick,
        Positions =
        [
            new SwitchPosition(4, View: HeadsetView.Vr),
            new SwitchPosition(5, View: HeadsetView.Desktop),
        ],
    };

    private static HotasReading Held(int button)
    {
        var buttons = new bool[32];
        buttons[button] = true;

        return new HotasReading { Id = Stick, Buttons = buttons };
    }

    private static SwitchTick Tick(int button, double seconds = 0, bool headset = true, bool enabled = false) => new()
    {
        Now = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
        Readings = [Held(button)],
        Status = new GameStatus { Flags = StatusFlags.None, ReadAt = DateTimeOffset.UnixEpoch },
        Binds = new EliteBinds { PresetName = "Test", SourceFile = "Test.binds", Bindings = [] },
        Enabled = enabled,
        HeadsetEnabled = headset,
    };

    private static SwitchReconciler New(FakeView view) => new(NullLogger<SwitchReconciler>.Instance, view);

    [Fact]
    public void AFlipToTheOtherViewQueuesOneHeadsetPress()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5), switches);

        var pending = Assert.Single(reconciler.Drain());

        Assert.Equal(HeadsetView.Desktop, pending.View);
        Assert.Null(pending.Say);
        Assert.Null(pending.Destination);
        Assert.Empty(pending.Steps);
    }

    [Fact]
    public void AFlipToTheViewShowingQueuesNothing()
    {
        var view = new FakeView { Showing = HeadsetView.Desktop };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Fact]
    public void AnUnknownViewQueuesNothingAndSaysSo()
    {
        var view = new FakeView { Showing = HeadsetView.Unknown };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Equal("Virtual Desktop is not streaming, so the headset view was left alone.", reconciler.States[0].Note);
        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Fact]
    public void NoChangeAfterAPressPausesTheSwitch()
    {
        const string why = "Shift+Win+D did not change the headset view. If you changed Virtual Desktop's shortcut, "
                           + "set it back, then press Resume.";

        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5, 1), switches);
        Assert.Single(reconciler.Drain());
        reconciler.HeadsetPressed("view switch", HeadsetView.Desktop, sent: true);

        reconciler.Poll(Tick(5, 2), switches);
        Assert.Equal(SwitchHealth.Ready, reconciler.States[0].Health);

        reconciler.Poll(Tick(5, 2 + SwitchReconciler.ContestWindow.TotalSeconds + 1), switches);

        Assert.True(reconciler.IsPaused("view switch"));
        Assert.Equal(SwitchHealth.Paused, reconciler.States[0].Health);
        Assert.Equal(why, reconciler.States[0].Note);

        // Said as well, on the headset's path rather than the game's.
        var said = Assert.Single(reconciler.Drain());
        Assert.Equal(why, said.Say);
        Assert.NotNull(said.View);
    }

    [Fact]
    public void AViewThatChangesEndsTheWatch()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5, 1), switches);
        reconciler.Drain();
        reconciler.HeadsetPressed("view switch", HeadsetView.Desktop, sent: true);

        view.Showing = HeadsetView.Desktop;
        reconciler.Poll(Tick(5, 2), switches);

        // A later press by hand is not blamed on the shortcut.
        view.Showing = HeadsetView.Vr;
        reconciler.Poll(Tick(5, 1 + SwitchReconciler.ContestWindow.TotalSeconds + 1), switches);

        Assert.False(reconciler.IsPaused("view switch"));
        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void AReadingLostAfterAPressDoesNotPause()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5, 1), switches);
        reconciler.Drain();
        reconciler.HeadsetPressed("view switch", HeadsetView.Desktop, sent: true);

        view.Showing = HeadsetView.Unknown;
        reconciler.Poll(Tick(5, 2), switches);
        reconciler.Poll(Tick(5, 2 + SwitchReconciler.ContestWindow.TotalSeconds + 1), switches);

        Assert.False(reconciler.IsPaused("view switch"));
    }

    [Fact]
    public void TheWatchIsTimedFromThePressNotTheFlip()
    {
        // The press waits for anything else sending keys to finish, which can take longer than the window.
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5, 1), switches);
        reconciler.Drain();

        reconciler.Poll(Tick(5, 10), switches);
        Assert.False(reconciler.IsPaused("view switch"));

        reconciler.HeadsetPressed("view switch", HeadsetView.Desktop, sent: true);
        reconciler.Poll(Tick(5, 11), switches);
        reconciler.Poll(Tick(5, 12), switches);
        Assert.False(reconciler.IsPaused("view switch"));

        reconciler.Poll(Tick(5, 11 + SwitchReconciler.ContestWindow.TotalSeconds + 1), switches);
        Assert.True(reconciler.IsPaused("view switch"));
    }

    [Fact]
    public void APressThatWasNotSentDoesNotPause()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4), switches);
        reconciler.Poll(Tick(5, 1), switches);
        reconciler.Drain();

        reconciler.HeadsetPressed("view switch", HeadsetView.Desktop, sent: false);
        reconciler.Poll(Tick(5, 2), switches);
        reconciler.Poll(Tick(5, 2 + SwitchReconciler.ContestWindow.TotalSeconds + 1), switches);

        Assert.False(reconciler.IsPaused("view switch"));
        Assert.Empty(reconciler.Drain());
    }

    [Fact]
    public void WithItsRowOffTheViewIsNotReadAndNothingDisagrees()
    {
        var view = new FakeView { Showing = HeadsetView.Desktop };
        var reconciler = New(view);

        reconciler.Poll(Tick(4, headset: false), [ViewSwitch()]);

        Assert.Equal(0, view.Reads);
        Assert.Null(reconciler.States[0].Disagrees);
    }

    [Theory]
    [InlineData(HeadsetView.Desktop, 4, "the headset is showing the desktop")]
    [InlineData(HeadsetView.Vr, 5, "the headset is showing SteamVR")]
    public void ASwitchSittingAgainstTheViewDisagrees(HeadsetView showing, int button, string disagrees)
    {
        var view = new FakeView { Showing = showing };
        var reconciler = New(view);

        reconciler.Poll(Tick(button), [ViewSwitch()]);

        Assert.Equal(disagrees, reconciler.States[0].Disagrees);
    }

    [Fact]
    public void AViewPositionIsReconciledWithItsOwnRowOnAndTheOtherTwoOff()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4, headset: true, enabled: false), switches);
        reconciler.Poll(Tick(5, headset: true, enabled: false), switches);

        Assert.Single(reconciler.Drain());
        Assert.Equal(SwitchHealth.Ready, reconciler.States[0].Health);
    }

    [Fact]
    public void AViewPositionIsNotReconciledWithItsOwnRowOff()
    {
        var view = new FakeView { Showing = HeadsetView.Vr };
        var reconciler = New(view);
        var switches = new[] { ViewSwitch() };

        reconciler.Poll(Tick(4, headset: false, enabled: true), switches);
        reconciler.Poll(Tick(5, headset: false, enabled: true), switches);

        Assert.Empty(reconciler.Drain());
        Assert.Equal(SwitchHealth.Off, reconciler.States[0].Health);
    }

    [Fact]
    public void TheViewIsReadAtMostOnceAPollAndNotForAnActionSwitch()
    {
        var view = new FakeView();
        var reconciler = New(view);

        var gear = new SwitchMapping
        {
            Name = "gear switch",
            DeviceId = Stick,
            Positions = [new SwitchPosition(8, "landing_gear", DesiredState.On), new SwitchPosition(9, "landing_gear", DesiredState.Off)],
        };

        reconciler.Poll(Tick(8), [gear]);
        Assert.Equal(0, view.Reads);

        reconciler.Poll(Tick(4), [ViewSwitch(), ViewSwitch() with { Name = "second" }]);
        Assert.Equal(1, view.Reads);
    }

    [Fact]
    public void TheRowExistsWithKeyInjectionOffAndStartsOff()
    {
        var row = SwitchCapability.Create(SwitchSurface.Inert, () => false)
            .Settings.Single(setting => setting.Key == SwitchCapability.HeadsetViewKey);

        Assert.Equal("Let a HOTAS switch change the headset view", row.Label);
        Assert.Equal(
            "When you flip a switch position that names a headset view, D47 reads which view Virtual Desktop is "
            + "showing and presses Shift+Win+D only if it is the other one. Off until you turn it on.",
            row.Help);
        Assert.True(row.Protected);
        Assert.Null(row.AppliesWhen);
        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.Equal("false", row.Binding!.Read!(new D47Settings()));
        Assert.Equal("true", row.Binding.Read(row.Binding.Write!(new D47Settings(), "true")));
    }
}
