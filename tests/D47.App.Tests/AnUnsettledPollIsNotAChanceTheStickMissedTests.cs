using D47.App.Voice;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A reader that has not settled reports nothing by construction, so the polls it answers are not
/// chances the bound stick had to turn up (#146).
/// </summary>
public class AnUnsettledPollIsNotAChanceTheStickMissedTests
{
    private const string Stick = "stick-1";

    [Fact]
    public void AStickThatIsHereIsNotCalledMissingBecauseTheReaderWasSlow()
    {
        var reader = new FakeHotasReader { IsSettled = false }.Holding(Stick, 8, 0);
        var (pushToTalk, cancel, log) = Bound();

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled * 2; tick++)
        {
            Poll(reader, pushToTalk, cancel, log);
        }

        reader.IsSettled = true;

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled * 2; tick++)
        {
            Poll(reader, pushToTalk, cancel, log);
        }

        Assert.Empty(log.Lines);
        Assert.True(pushToTalk.DevicePresent);
    }

    [Fact]
    public void AStickThatIsGoneIsCalledMissingOnceTheSettledPollsRunOut()
    {
        var reader = new FakeHotasReader { IsSettled = true };
        var (pushToTalk, cancel, log) = Bound();

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled - 1; tick++)
        {
            Poll(reader, pushToTalk, cancel, log);
        }

        Assert.Empty(log.Lines);

        Poll(reader, pushToTalk, cancel, log);

        Assert.Single(log.Lines, line => line.Contains("controller that is not here", StringComparison.Ordinal));

        Poll(reader, pushToTalk, cancel, log);

        Assert.Single(log.Lines);
    }

    [Fact]
    public void TheFirstSettledTickThatReadsItDownIsAPress()
    {
        var reader = new FakeHotasReader { IsSettled = false }.Holding(Stick, 8, 3);
        var (pushToTalk, cancel, log) = Bound();

        var presses = 0;
        pushToTalk.Pressed += () => presses++;

        Poll(reader, pushToTalk, cancel, log);

        Assert.Equal(0, presses);

        reader.IsSettled = true;
        Poll(reader, pushToTalk, cancel, log);

        Assert.Equal(1, presses);
    }

    private static (BoundButton PushToTalk, BoundButton Cancel, RecordingLogger Log) Bound()
    {
        var pushToTalk = new BoundButton();
        var cancel = new BoundButton();

        pushToTalk.Bind(new HotasButton(Stick, 3));

        return (pushToTalk, cancel, new RecordingLogger());
    }

    private static void Poll(IHotasReader reader, BoundButton pushToTalk, BoundButton cancel, RecordingLogger log) =>
        Listener.PollTheStick(reader, pushToTalk, new BoundButton(), cancel, new ActionButtons(), log);
}
