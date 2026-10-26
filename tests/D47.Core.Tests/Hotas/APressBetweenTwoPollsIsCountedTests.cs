using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>A press that goes down and up between two polls is seen from the reader's count (#1617).</summary>
public sealed class APressBetweenTwoPollsIsCountedTests
{
    private const string Stick = "hid:stick";

    private static readonly int[] Untouched = new int[8];

    private static int[] Presses(int button, int count)
    {
        var presses = new int[8];
        presses[button] = count;
        return presses;
    }

    private static (ActionButtons Buttons, List<string> Pressed) ChannelUpOn(int button)
    {
        var slot = ActionButtons.Slots.Single(s => s.Key == CommsCapability.ChannelUpButtonKey);
        var buttons = new ActionButtons();
        var pressed = new List<string>();

        buttons.Pressed += pressed.Add;
        buttons.Bind(slot.Write(new D47Settings(), $"{Stick}#{button}"));

        return (buttons, pressed);
    }

    [Fact]
    public void ThreePressesBetweenTwoPollsPressTheSlotThreeTimes()
    {
        var reader = new FakeHotasReader();
        var (buttons, pressed) = ChannelUpOn(3);

        buttons.Poll(reader.Counting(Stick, Presses(3, 4)).Poll());
        buttons.Poll(reader.Counting(Stick, Presses(3, 7)).Poll());

        Assert.Equal(Enumerable.Repeat(CommsCapability.ChannelUpButtonKey, 3), pressed);
    }

    [Fact]
    public void APressSeenHeldAndCountedPressesTheSlotOnce()
    {
        var reader = new FakeHotasReader();
        var (buttons, pressed) = ChannelUpOn(3);

        buttons.Poll(reader.Counting(Stick, Untouched).Poll());
        buttons.Poll(reader.Counting(Stick, Presses(3, 1), 3).Poll());
        buttons.Poll(reader.Counting(Stick, Presses(3, 1)).Poll());

        Assert.Single(pressed);
    }

    [Fact]
    public void ACountThatFallsSetsANewBaselineAndPressesNothing()
    {
        var reader = new FakeHotasReader();
        var (buttons, pressed) = ChannelUpOn(3);

        buttons.Poll(reader.Counting(Stick, Presses(3, 9)).Poll());
        buttons.Poll(reader.Counting(Stick, Presses(3, 2)).Poll());

        Assert.Empty(pressed);

        buttons.Poll(reader.Counting(Stick, Presses(3, 3)).Poll());

        Assert.Single(pressed);
    }

    [Fact]
    public void TheFirstCountSeenPressesNothing()
    {
        var reader = new FakeHotasReader();
        var (buttons, pressed) = ChannelUpOn(3);

        buttons.Poll(reader.Counting(Stick, Presses(3, 5)).Poll());

        Assert.Empty(pressed);
    }

    [Fact]
    public void PushToTalkIgnoresAPressSeenOnlyInTheCount()
    {
        var reader = new FakeHotasReader();
        var button = new BoundButton();
        var pressed = 0;
        var released = 0;

        button.Pressed += () => pressed++;
        button.Released += () => released++;
        button.Bind(new HotasButton(Stick, 3));

        button.Poll(reader.Counting(Stick, Untouched).Poll());
        button.Poll(reader.Counting(Stick, Presses(3, 2)).Poll());

        Assert.Equal(0, pressed);
        Assert.Equal(0, released);
        Assert.False(button.IsDown);

        button.Poll(reader.Counting(Stick, Presses(3, 3), 3).Poll());

        Assert.Equal(1, pressed);
        Assert.True(button.IsDown);
    }

    [Fact]
    public void TheBindPageCapturesAPressItNeverSawHeld()
    {
        var reader = new FakeHotasReader();
        var capture = new ButtonCapture();

        Assert.Equal(ButtonCaptureStage.Waiting, capture.Poll(reader.Counting(Stick, Untouched).Poll(), TimeSpan.Zero).Stage);

        var done = capture.Poll(reader.Counting(Stick, Presses(5, 1)).Poll(), TimeSpan.FromSeconds(0.1));

        Assert.Equal(ButtonCaptureStage.Captured, done.Stage);
        Assert.Equal(new HotasButton(Stick, 5), done.Binding);
    }

    [Fact]
    public void TheBindPageDeclinesTwoButtonsCountedInOnePoll()
    {
        var reader = new FakeHotasReader();
        var capture = new ButtonCapture();
        var both = Presses(5, 1);
        both[6] = 1;

        capture.Poll(reader.Counting(Stick, Untouched).Poll(), TimeSpan.Zero);

        Assert.Equal(ButtonCaptureStage.Declined, capture.Poll(reader.Counting(Stick, both).Poll(), TimeSpan.FromSeconds(0.1)).Stage);
    }

    [Fact]
    public void TheBindPageTakesTheFirstCountSeenAsItsBaseline()
    {
        var reader = new FakeHotasReader();
        var capture = new ButtonCapture();

        capture.Poll(reader.Counting(Stick, Presses(5, 4)).Poll(), TimeSpan.Zero);

        Assert.Equal(ButtonCaptureStage.Waiting, capture.Poll(reader.Counting(Stick, Presses(5, 4)).Poll(), TimeSpan.FromSeconds(0.1)).Stage);
    }
}
