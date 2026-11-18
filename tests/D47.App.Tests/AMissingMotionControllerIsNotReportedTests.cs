using D47.App.Voice;
using D47.Core.Hotas;
using D47.Core.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A push-to-talk bound to a motion controller is not called missing while there is no headset session, and the
/// stick poll reads the motion controllers beside the sticks (#2548).
/// </summary>
public class AMissingMotionControllerIsNotReportedTests
{
    [Fact]
    public void APushToTalkOnAMotionControllerIsNeverCalledMissing()
    {
        var reader = new FakeHotasReader { IsSettled = true };
        var pushToTalk = new BoundButton();
        var comms = new BoundButton();
        var log = new RecordingLogger();

        pushToTalk.Bind(new HotasButton(MotionControllerButtons.RightId, (int)VrButton.Secondary));
        comms.Bind(new HotasButton(MotionControllerButtons.LeftId, (int)VrButton.Trigger));

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled * 2; tick++)
        {
            Listener.PollTheStick(reader, pushToTalk, comms, new BoundButton(), new ActionButtons(), log, []);
        }

        Assert.Empty(log.Lines);
    }

    [Fact]
    public void TheStickPollPressesAButtonBoundToAMotionController()
    {
        var reader = new FakeHotasReader { IsSettled = true }.Holding("hid:stick", 8);
        var controllers = new MotionControllerButtons();
        var now = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);
        var pushToTalk = new BoundButton();
        var presses = 0;

        pushToTalk.Pressed += () => presses++;
        pushToTalk.Bind(new HotasButton(MotionControllerButtons.RightId, (int)VrButton.Primary));

        controllers.Observe(
            [new VrHand(4, default, default, VrHand.Bit(VrButton.Primary), default, VrSide.Right)], _ => false, now);
        Listener.PollTheStick(
            reader, pushToTalk, new BoundButton(), new BoundButton(), new ActionButtons(), new RecordingLogger(), controllers.Poll(now));

        Assert.Equal(1, presses);
    }
}
