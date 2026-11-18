using System.Numerics;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// Each motion controller is a stick-button device to the button poll: its inputs raise the slots bound to them,
/// except the trigger, grip and stick of a hand the panel took when the input went down (#2548).
/// </summary>
public sealed class AMotionControllerButtonActsAsAStickButtonTests
{
    private const uint LeftDevice = 3;
    private const uint RightDevice = 4;

    private static readonly DateTimeOffset T0 = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    private static readonly Func<VrHand, bool> OffThePanel = _ => false;
    private static readonly Func<VrHand, bool> OnThePanel = _ => true;

    private static VrHand Right(uint held = 0, Vector2 stick = default) =>
        new(RightDevice, default, default, held, stick, VrSide.Right);

    private static VrHand Left(uint held = 0, Vector2 stick = default) =>
        new(LeftDevice, default, default, held, stick, VrSide.Left);

    /// <summary>The hand with one input down: a button's bit, or the stick pushed fully that way.</summary>
    private static VrHand RightWith(int input) => input switch
    {
        MotionControllerButtons.StickUp => Right(stick: new Vector2(0f, 1f)),
        MotionControllerButtons.StickDown => Right(stick: new Vector2(0f, -1f)),
        MotionControllerButtons.StickLeft => Right(stick: new Vector2(-1f, 0f)),
        MotionControllerButtons.StickRight => Right(stick: new Vector2(1f, 0f)),
        _ => Right(held: 1u << input),
    };

    private static (ActionButtons Buttons, List<string> Pressed) ShowOverlayOn(string device, int input)
    {
        var slot = ActionButtons.Slots.Single(s => s.Key == InterfaceCapability.ShowOverlayButtonKey);
        var buttons = new ActionButtons();
        var pressed = new List<string>();

        buttons.Pressed += pressed.Add;
        buttons.Bind(slot.Write(new D47Settings(), new HotasButton(device, input).ToString()));

        return (buttons, pressed);
    }

    public static TheoryData<int> EveryInput() => [.. Enumerable.Range(0, MotionControllerButtons.Inputs)];

    [Theory]
    [MemberData(nameof(EveryInput))]
    public void APressOfEachInputRaisesItsSlotOnce(int input)
    {
        var controllers = new MotionControllerButtons();
        var (buttons, pressed) = ShowOverlayOn(MotionControllerButtons.RightId, input);

        controllers.Observe([Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        controllers.Observe([RightWith(input)], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));
        buttons.Poll(controllers.Poll(T0));

        controllers.Observe([Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        Assert.Equal([InterfaceCapability.ShowOverlayButtonKey], pressed);
    }

    [Fact]
    public void TheOtherHandsSameInputRaisesNothing()
    {
        var controllers = new MotionControllerButtons();
        var (buttons, pressed) = ShowOverlayOn(MotionControllerButtons.RightId, (int)VrButton.Secondary);

        controllers.Observe([Left(), Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));
        controllers.Observe([Left(VrHand.Bit(VrButton.Secondary)), Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        Assert.Empty(pressed);
    }

    [Fact]
    public void APressShorterThanATickIsCounted()
    {
        var controllers = new MotionControllerButtons();
        var (buttons, pressed) = ShowOverlayOn(MotionControllerButtons.RightId, (int)VrButton.Primary);

        controllers.Observe([Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        controllers.Observe([Right(VrHand.Bit(VrButton.Primary))], OffThePanel, T0);
        controllers.Observe([Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        Assert.Single(pressed);
    }

    [Fact]
    public void AHeldPushToTalkOnAControllerButtonHoldsAndReleases()
    {
        var controllers = new MotionControllerButtons();
        var pushToTalk = new BoundButton();
        var events = new List<string>();

        pushToTalk.Pressed += () => events.Add("pressed");
        pushToTalk.Released += () => events.Add("released");
        pushToTalk.Bind(new HotasButton(MotionControllerButtons.LeftId, (int)VrButton.Grip));

        controllers.Observe([Left()], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        for (var frame = 0; frame < 5; frame++)
        {
            controllers.Observe([Left(VrHand.Bit(VrButton.Grip))], OffThePanel, T0);
            pushToTalk.Poll(controllers.Poll(T0));
        }

        Assert.Equal(["pressed"], events);

        controllers.Observe([Left()], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        Assert.Equal(["pressed", "released"], events);
    }

    [Fact]
    public void ATriggerPressedWithTheRayOnThePanelRaisesNothing()
    {
        var controllers = new MotionControllerButtons();
        var (buttons, pressed) = ShowOverlayOn(MotionControllerButtons.RightId, (int)VrButton.Trigger);

        controllers.Observe([Right()], OnThePanel, T0);
        buttons.Poll(controllers.Poll(T0));
        controllers.Observe([Right(VrHand.Bit(VrButton.Trigger))], OnThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        Assert.False(controllers.KeepsFromThePanel(VrButton.Trigger, T0));

        controllers.Observe([Right(VrHand.Bit(VrButton.Trigger))], OffThePanel, T0);
        controllers.Observe([Right()], OffThePanel, T0);
        buttons.Poll(controllers.Poll(T0));

        Assert.Empty(pressed);
    }

    [Fact]
    public void ATriggerPressedOffThePanelThenSweptOntoItStaysWithItsBoundAction()
    {
        var controllers = new MotionControllerButtons();
        var pushToTalk = new BoundButton();
        var events = new List<string>();

        pushToTalk.Pressed += () => events.Add("pressed");
        pushToTalk.Released += () => events.Add("released");
        pushToTalk.Bind(new HotasButton(MotionControllerButtons.RightId, (int)VrButton.Trigger));

        controllers.Observe([Right()], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));
        controllers.Observe([Right(VrHand.Bit(VrButton.Trigger))], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        controllers.Observe([Right(VrHand.Bit(VrButton.Trigger))], OnThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        Assert.Equal(["pressed"], events);
        Assert.True(controllers.KeepsFromThePanel(VrButton.Trigger, T0));

        controllers.Observe([Right()], OnThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        Assert.Equal(["pressed", "released"], events);
        Assert.False(controllers.KeepsFromThePanel(VrButton.Trigger, T0));
    }

    [Fact]
    public void AStickPushedOffThePanelThenSweptOntoItKeepsItFromThePanel()
    {
        var controllers = new MotionControllerButtons();

        controllers.Observe([Right(stick: new Vector2(0f, 1f))], OffThePanel, T0);
        controllers.Observe([Right(stick: new Vector2(0f, 1f))], OnThePanel, T0);

        Assert.True(controllers.KeepsTheStickFromThePanel(RightDevice, T0));
        Assert.False(controllers.KeepsTheStickFromThePanel(LeftDevice, T0));
    }

    [Fact]
    public void ButtonsThePanelCannotTakeReachTheirActionOnThePanel()
    {
        foreach (var button in new[] { VrButton.Primary, VrButton.Secondary, VrButton.Menu, VrButton.StickClick })
        {
            var controllers = new MotionControllerButtons();
            var (buttons, pressed) = ShowOverlayOn(MotionControllerButtons.RightId, (int)button);

            controllers.Observe([Right()], OnThePanel, T0);
            buttons.Poll(controllers.Poll(T0));
            controllers.Observe([Right(VrHand.Bit(button))], OnThePanel, T0);
            buttons.Poll(controllers.Poll(T0));

            Assert.Single(pressed);
        }
    }

    [Fact]
    public void AHandMissingFromTheListReleasesWhatItWasHolding()
    {
        var controllers = new MotionControllerButtons();
        var pushToTalk = new BoundButton();
        var released = 0;

        pushToTalk.Released += () => released++;
        pushToTalk.Bind(new HotasButton(MotionControllerButtons.RightId, (int)VrButton.Primary));

        controllers.Observe([Right(VrHand.Bit(VrButton.Primary))], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));
        controllers.Observe([Left()], OffThePanel, T0);
        pushToTalk.Poll(controllers.Poll(T0));

        Assert.Equal(1, released);
        Assert.Equal([MotionControllerButtons.LeftId], controllers.Poll(T0).Select(reading => reading.Id));
    }

    [Fact]
    public void HandsReadLongerAgoThanTheStaleLimitAreAbsent()
    {
        var controllers = new MotionControllerButtons();

        controllers.Observe([Right(VrHand.Bit(VrButton.Trigger))], OffThePanel, T0);

        Assert.Single(controllers.Poll(T0 + MotionControllerButtons.Stale));
        Assert.Empty(controllers.Poll(T0 + MotionControllerButtons.Stale + TimeSpan.FromMilliseconds(1)));
        Assert.False(controllers.KeepsFromThePanel(VrButton.Trigger, T0 + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void AClearedOrUnhandedControllerIsAbsent()
    {
        var controllers = new MotionControllerButtons();

        controllers.Observe([new VrHand(9, default, default, VrHand.Bit(VrButton.Trigger))], OffThePanel, T0);

        Assert.Empty(controllers.Poll(T0));

        controllers.Observe([Left(), Right()], OffThePanel, T0);
        controllers.Clear();

        Assert.Empty(controllers.Poll(T0));
    }

    [Fact]
    public void ACaptureTakesAControllerButtonAndNamesItsHandAndInput()
    {
        var controllers = new MotionControllerButtons();
        var capture = new ButtonCapture();

        controllers.Observe([Right()], OffThePanel, T0);
        capture.Poll(controllers.Poll(T0), TimeSpan.Zero);
        controllers.Observe([Right(VrHand.Bit(VrButton.Secondary))], OffThePanel, T0);
        capture.Poll(controllers.Poll(T0), TimeSpan.FromMilliseconds(100));
        controllers.Observe([Right()], OffThePanel, T0);
        var result = capture.Poll(controllers.Poll(T0), TimeSpan.FromMilliseconds(200));

        Assert.Equal(ButtonCaptureStage.Captured, result.Stage);
        Assert.Equal("vr:right#3", result.Binding.ToString());
        Assert.StartsWith("Right controller B.", result.Says, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vr:right#0", "right controller trigger")]
    [InlineData("vr:right#2", "right controller A")]
    [InlineData("vr:right#3", "right controller B")]
    [InlineData("vr:left#2", "left controller X")]
    [InlineData("vr:left#3", "left controller Y")]
    [InlineData("vr:left#4", "left controller menu button")]
    [InlineData("vr:left#5", "left controller stick click")]
    [InlineData("vr:right#9", "right controller stick right")]
    [InlineData("hid:stick#4", "button 5")]
    public void AControllerButtonIsNamedByItsHandAndInput(string stored, string named) =>
        Assert.Equal(named, HotasButton.Parse(stored)!.Value.Describe());
}
