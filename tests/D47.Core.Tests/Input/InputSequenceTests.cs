using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// The ordering rules for turning a binding into presses. All of this is arithmetic, and all
/// of it fails invisibly: a modifier released early produces a keystroke the Commander did not
/// ask for, and the only symptom is the game doing something else.
/// </summary>
public class InputSequenceTests
{
    private static EliteBinding Binding(string key, params string[] modifiers) =>
        new("TestAction", "Primary", "Keyboard", key) { Modifiers = modifiers };

    [Fact]
    public void APlainKeyGoesDownWaitsAndComesUp()
    {
        var steps = InputSequence.Tap(Binding("Key_L"));

        Assert.Collection(
            steps,
            step => Assert.Equal(new InputStep(InputStepKind.KeyDown, 0x4C), step),
            step => Assert.Equal(InputStepKind.Delay, step.Kind),
            step => Assert.Equal(new InputStep(InputStepKind.KeyUp, 0x4C), step));
    }

    [Fact]
    public void TheTapHoldsLongEnoughForTheGameToSampleIt()
    {
        // Elite samples input per frame, so a press and release inside one frame is a press it
        // never sees. The symptom is a key that works four times in five.
        Assert.True(InputSequence.TapHold >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void ModifiersGoDownBeforeTheKeyAndComeUpAfterItInReverse()
    {
        var steps = InputSequence.Tap(Binding("Key_X", "Key_LeftControl", "Key_LeftAlt"));

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0xA2),
                new InputStep(InputStepKind.KeyDown, 0xA4),
                new InputStep(InputStepKind.KeyDown, 0x58),
            ],
            steps.Where(step => step.Kind == InputStepKind.KeyDown));

        // Reverse on the way up. Releasing Control before X while X is still down sends a bare
        // X to whatever reads it next.
        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyUp, 0x58),
                new InputStep(InputStepKind.KeyUp, 0xA4),
                new InputStep(InputStepKind.KeyUp, 0xA2),
            ],
            steps.Where(step => step.Kind == InputStepKind.KeyUp));
    }

    [Fact]
    public void AnExtendedKeyCarriesItsFlagThroughToTheStep()
    {
        // The scancode for Delete and for the numpad's decimal point are the same number; this
        // bit is the whole difference.
        var steps = InputSequence.Tap(Binding("Key_Delete"));

        Assert.All(
            steps.Where(step => step.Kind != InputStepKind.Delay),
            step => Assert.True(step.Extended));
    }

    [Fact]
    public void AMouseBindingProducesButtonStepsRatherThanKeyStrokes()
    {
        var steps = InputSequence.Tap(new EliteBinding("PrimaryFire", "Primary", "Mouse", "Mouse_1"));

        Assert.Equal(InputStepKind.MouseDown, steps[0].Kind);
        Assert.Equal(1u, steps[0].Code);
        Assert.Equal(InputStepKind.MouseUp, steps[^1].Kind);
    }

    [Fact]
    public void AMouseBindingStillHonoursItsKeyboardModifiers()
    {
        var binding = new EliteBinding("PrimaryFire", "Primary", "Mouse", "Mouse_1")
        {
            Modifiers = ["Key_LeftShift"],
        };

        var steps = InputSequence.Tap(binding);

        Assert.Equal(new InputStep(InputStepKind.KeyDown, 0xA0), steps[0]);
        Assert.Equal(new InputStep(InputStepKind.KeyUp, 0xA0), steps[^1]);
    }

    [Fact]
    public void AnUnpressableBindingProducesNothingRatherThanThrowing()
    {
        var steps = InputSequence.Tap(new EliteBinding("YawLeftButton", "Primary", "Joystick", "Joy_XAxis"));

        Assert.Empty(steps);
    }

    [Fact]
    public void AHoldWaitsForAsLongAsItWasAskedTo()
    {
        var steps = InputSequence.Hold(Binding("Key_L"), TimeSpan.FromSeconds(6));

        Assert.Equal(TimeSpan.FromSeconds(6), steps.Single(step => step.Kind == InputStepKind.Delay).Delay);
    }

    [Fact]
    public void ReleaseForNamesEverythingLeftDownInReverseOrder()
    {
        // A sequence cut off after the key went down: the release has to let go of the key and
        // both modifiers, newest first.
        IReadOnlyList<InputStep> partial =
        [
            new InputStep(InputStepKind.KeyDown, 0xA2),
            new InputStep(InputStepKind.KeyDown, 0xA4),
            new InputStep(InputStepKind.KeyDown, 0x58),
        ];

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyUp, 0x58),
                new InputStep(InputStepKind.KeyUp, 0xA4),
                new InputStep(InputStepKind.KeyUp, 0xA2),
            ],
            InputSequence.ReleaseFor(partial));
    }

    [Fact]
    public void ReleaseForLeavesOutAnythingAlreadyReleased()
    {
        Assert.Empty(InputSequence.ReleaseFor(InputSequence.Tap(Binding("Key_L", "Key_LeftControl"))));
    }
}
