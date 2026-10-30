using System.Text.Json;
using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>No line about the honk in a system whose every body is already found; the press is unchanged.</summary>
public class AHonkWhereEveryBodyIsFoundSaysNothingTests
{
    private const long Here = 1234;

    private const long Elsewhere = 5678;

    private static readonly EliteBinding Fire = new("PrimaryFire", "Primary", "Mouse", "Mouse_1");

    private static readonly EliteBinds Bound = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [Fire],
    };

    private static readonly DateTimeOffset Jumped = DateTimeOffset.UnixEpoch;

    private static readonly StatusFlags Witchspace = StatusFlags.InMainShip | StatusFlags.FsdJump;

    private static readonly StatusFlags Analysis =
        StatusFlags.InMainShip | StatusFlags.Supercruise | StatusFlags.AnalysisMode;

    private bool _inFront;

    private static JournalEvent Event(double seconds, string kind, string json) =>
        new(Jumped + TimeSpan.FromSeconds(seconds), kind, JsonDocument.Parse(json).RootElement.Clone());

    private static JournalEvent JumpTo(double seconds, long address) =>
        Event(seconds, "FSDJump", $$"""{"SystemAddress":{{address}}}""");

    private static JournalEvent AllFound(double seconds, long address) =>
        Event(seconds, "FSSAllBodiesFound", $$"""{"SystemAddress":{{address}},"Count":1}""");

    private static JournalEvent Discovery(double seconds, long address, string progress) =>
        Event(seconds, "FSSDiscoveryScan", $$"""{"SystemAddress":{{address}},"Progress":{{progress}},"BodyCount":12}""");

    private static CalloutContext At(StatusFlags flags, double seconds, bool priming = false, params JournalEvent[] events)
    {
        var now = Jumped + TimeSpan.FromSeconds(seconds);
        return new(now, priming, null, new GameStatus { Flags = flags, ReadAt = now }, NavRoute.None, events);
    }

    private HonkOnArrival Honk() => new(() => true, () => Bound, () => _inFront);

    /// <summary>Every line the honk says from <paramref name="from"/> to a minute after the jump.</summary>
    private static List<string> SaidFrom(HonkOnArrival honk, double from, params (double At, JournalEvent Event)[] events)
    {
        var said = new List<string>();

        for (var second = from; second < 60; second = Math.Round(second + 0.1, 1))
        {
            var arriving = events.Where(e => e.At == second).Select(e => e.Event).ToArray();

            if (honk.Examine(At(Analysis, second, events: arriving)).Say is { } line)
            {
                said.Add(line);
            }
        }

        return said;
    }

    [Fact]
    public void AnAutoscanThatFindsEveryBodyWithholdsTheLineForEliteBehind()
    {
        var honk = Honk();
        honk.Examine(At(Witchspace, 0, events: JumpTo(0, Here)));

        Assert.Empty(SaidFrom(honk, 1, (6, AllFound(6, Here))));
    }

    [Fact]
    public void AnAutoscanThatFindsEveryBodyWithholdsDidNotTake()
    {
        _inFront = true;
        var honk = Honk();
        honk.Examine(At(Witchspace, 0, events: JumpTo(0, Here)));

        Assert.Empty(SaidFrom(honk, 1, (6, AllFound(6, Here))));
    }

    [Fact]
    public void ASystemScannedInFullEarlierTodayIsKnownOnReturn()
    {
        var honk = Honk();

        // Today's backlog, folded while priming.
        honk.Examine(At(Analysis, -600, priming: true, Discovery(-600, Here, "1.0")));
        honk.Examine(At(Witchspace, 0, events: JumpTo(0, Here)));

        Assert.Empty(SaidFrom(honk, 1));
    }

    [Fact]
    public void ASystemScannedInPartIsNotKnown()
    {
        var honk = Honk();
        honk.Examine(At(Analysis, -600, priming: true, Discovery(-600, Here, "0.3")));
        honk.Examine(At(Witchspace, 0, events: JumpTo(0, Here)));

        Assert.Equal([HonkOnArrival.NotInFront], SaidFrom(honk, 1));
    }

    [Fact]
    public void AnotherSystemFoundInFullDoesNotSilenceThisOne()
    {
        var honk = Honk();
        honk.Examine(At(Analysis, -600, priming: true, AllFound(-600, Elsewhere)));
        honk.Examine(At(Witchspace, 0, events: JumpTo(0, Here)));

        Assert.Equal([HonkOnArrival.NotInFront], SaidFrom(honk, 1));
    }

    [Fact]
    public void AKnownSystemIsStillHonked()
    {
        _inFront = true;
        var honk = Honk();
        honk.Examine(At(Witchspace, 0, events: [JumpTo(0, Here), AllFound(0, Here)]));

        var decision = honk.Examine(At(Analysis, 1));

        Assert.True(decision.Steps.SequenceEqual(InputSequence.Hold(Fire, HonkOnArrival.Charge)));
    }
}
