using System.Reflection;
using D47.App;
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
        var (pushToTalk, cancel, lines) = Bound();

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled * 2; tick++)
        {
            Poll(reader, pushToTalk, cancel, lines);
        }

        reader.IsSettled = true;

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled * 2; tick++)
        {
            Poll(reader, pushToTalk, cancel, lines);
        }

        Assert.Empty(lines);
        Assert.True(pushToTalk.DevicePresent);
    }

    [Fact]
    public void AStickThatIsGoneIsCalledMissingOnceTheSettledPollsRunOut()
    {
        var reader = new FakeHotasReader { IsSettled = true };
        var (pushToTalk, cancel, lines) = Bound();

        for (var tick = 0; tick < BoundButton.PollsBeforeAbsenceIsCalled - 1; tick++)
        {
            Poll(reader, pushToTalk, cancel, lines);
        }

        Assert.Empty(lines);

        Poll(reader, pushToTalk, cancel, lines);

        Assert.Single(lines, line => line.Contains("controller that is not here", StringComparison.Ordinal));

        Poll(reader, pushToTalk, cancel, lines);

        Assert.Single(lines);
    }

    [Fact]
    public void TheFirstSettledTickThatReadsItDownIsAPress()
    {
        var reader = new FakeHotasReader { IsSettled = false }.Holding(Stick, 8, 3);
        var (pushToTalk, cancel, lines) = Bound();

        var presses = 0;
        pushToTalk.Pressed += () => presses++;

        Poll(reader, pushToTalk, cancel, lines);

        Assert.Equal(0, presses);

        reader.IsSettled = true;
        Poll(reader, pushToTalk, cancel, lines);

        Assert.Equal(1, presses);
    }

    private static (BoundButton PushToTalk, BoundButton Cancel, List<string> Lines) Bound()
    {
        var pushToTalk = new BoundButton();
        var cancel = new BoundButton();

        pushToTalk.Bind(new HotasButton(Stick, 3));

        return (pushToTalk, cancel, []);
    }

    private static readonly MethodInfo PollTheStick =
        typeof(AppHost).GetMethod(
            "PollTheStick",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("AppHost.PollTheStick is not where the test expects it");

    private static void Poll(IHotasReader reader, BoundButton pushToTalk, BoundButton cancel, List<string> lines) =>
        PollTheStick.Invoke(null, [reader, pushToTalk, cancel, new Capture(lines)]);

    private sealed class Capture(List<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            lines.Add(formatter(state, exception));
    }
}
