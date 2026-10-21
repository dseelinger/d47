using D47.App.Input;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A switch's key chord is routed apart from the game, kept off duty, and pressed by the shared sender (#1428).</summary>
public class ASwitchChordGoesToAnyWindowTests
{
    private static readonly KeyChord Paste = new("Key_V", ChordModifiers.Ctrl);

    private static PendingReconcile Chord() => new("paste button", Paste.Describe(), [], Chord: Paste);

    private static ChordSender DryRun() => new(NullLogger.Instance) { DryRun = true };

    private sealed class FixedView(HeadsetView showing) : IHeadsetViewSource
    {
        public HeadsetView Read() => showing;
    }

    [Fact]
    public void OffDutyAChordIsKeptInItsOwnShareAndLeftOutOfTheGames()
    {
        var keys = new PendingReconcile("gear switch", "the landing gear", [new InputStep(InputStepKind.KeyDown, 0x47)]);

        var route = ReconcileRoute.Of([keys, Chord()], offDuty: true);

        Assert.Equal(Paste, Assert.Single(route.Chords).Chord);
        Assert.Empty(route.Game);
        Assert.Empty(route.Headset);
        Assert.Empty(route.Pages);
    }

    [Fact]
    public void OnDutyAChordStillStaysOutOfTheGameShare()
    {
        var keys = new PendingReconcile("gear switch", "the landing gear", [new InputStep(InputStepKind.KeyDown, 0x47)]);

        var route = ReconcileRoute.Of([keys, Chord()], offDuty: false);

        Assert.Equal("gear switch", Assert.Single(route.Game).Switch);
        Assert.Equal("paste button", Assert.Single(route.Chords).Switch);
    }

    [Fact]
    public void ADryRunSenderRecordsTheCtrlVSteps()
    {
        var sender = DryRun();

        Assert.Null(sender.Press(Chord().Chord!));

        Assert.Equal(1, sender.Presses);
        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0xA2),
                new InputStep(InputStepKind.KeyDown, 0x56),
                InputStep.Wait(InputSequence.TapHold),
                new InputStep(InputStepKind.KeyUp, 0x56),
                new InputStep(InputStepKind.KeyUp, 0xA2),
            ],
            sender.Recorded);
    }

    [Fact]
    public void ARefusedChordIsNotPressed()
    {
        var sender = DryRun();

        Assert.NotNull(sender.Press(new KeyChord("Key_L", ChordModifiers.Win)));
        Assert.Equal(0, sender.Presses);
        Assert.Empty(sender.Recorded);
    }

    [Fact]
    public void TheHeadsetShortcutGoesThroughTheSharedSender()
    {
        var sender = DryRun();
        var key = new HeadsetViewKey(new FixedView(HeadsetView.Desktop), NullLogger<HeadsetViewKey>.Instance, sender);
        var reconciler = new SwitchReconciler(NullLogger<SwitchReconciler>.Instance);

        Assert.Null(key.CarryOut(new PendingReconcile("view switch", "SteamVR", [], View: HeadsetView.Vr), reconciler));

        Assert.Equal(1, key.Presses);
        Assert.Equal(1, sender.Presses);
        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0xA0),
                new InputStep(InputStepKind.KeyDown, 0x5B, Extended: true),
                new InputStep(InputStepKind.KeyDown, 0x44),
                InputStep.Wait(InputSequence.TapHold),
                new InputStep(InputStepKind.KeyUp, 0x44),
                new InputStep(InputStepKind.KeyUp, 0x5B, Extended: true),
                new InputStep(InputStepKind.KeyUp, 0xA0),
            ],
            sender.Recorded);
    }
}
