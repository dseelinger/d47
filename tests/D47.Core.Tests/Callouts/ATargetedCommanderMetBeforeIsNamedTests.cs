using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ATargetedCommanderMetBeforeIsNamedTests
{
    private const string Fid = "F100";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static string Line(string date, string kind, string rest = "") =>
        "{\"timestamp\":\"" + date + "T10:00:00Z\",\"event\":\"" + kind + "\"" + rest + "}";

    private static string Target(string date, string name) =>
        Line(date, "ShipTargeted", ",\"TargetLocked\":true,\"PilotName\":\"$cmdr_decorate:#name=" + name + ";\"");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string[] EarlierSession(params string[] lines) =>
    [
        Line("2026-09-30", "LoadGame", ",\"FID\":\"" + Fid + "\""),
        Line("2026-09-30", "FSDJump", ",\"StarSystem\":\"Shinrarta Dezhra\""),
        .. lines,
    ];

    private static (MetBeforeCallout Callout, CommanderLog Log) Build(bool folded, params string[] history)
    {
        var log = new CommanderLog(new MemoryFileSystem(), NullLogger.Instance);
        log.Fold(history);

        if (folded)
        {
            log.FoldHistory([], TestContext.Current.CancellationToken);
        }

        return (new MetBeforeCallout(log), log);
    }

    private static CalloutContext Tick(CommanderLog log, string[] live, bool priming = false, GameStatus? status = null)
    {
        var events = live.Select(Event).ToList();
        log.Apply(events);

        var store = new GameStateStore();
        store.Apply(Event(Line("2026-10-21", "Commander", ",\"FID\":\"" + Fid + "\",\"Name\":\"Jameson\"")));

        return new CalloutContext(Now, priming, store.Active, status ?? GameStatus.Unknown, NavRoute.None, events);
    }

    private static string[] NewSession(params string[] lines) =>
        [Line("2026-10-21", "LoadGame", ",\"FID\":\"" + Fid + "\""), .. lines];

    [Fact]
    public void ACommanderFromAnEarlierSessionIsNamedWithWhenWhereAndWhatHappened()
    {
        var (callout, log) = Build(
            true,
            EarlierSession(
                Target("2026-09-30", "Arden Vale"),
                Line("2026-09-30", "CrimeVictim", ",\"Offender\":\"Arden Vale\",\"CrimeType\":\"assault\"")));

        var said = callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")))).ToList();

        var announcement = Assert.Single(said);
        Assert.Equal(CalloutUrgency.Routine, announcement.Urgency);
        Assert.Equal(
            "Commander Arden Vale — you met three weeks ago in Shinrarta Dezhra; they assaulted you.",
            announcement.Text);
    }

    [Fact]
    public void TheMostNotableKindWins()
    {
        var (callout, log) = Build(
            true,
            EarlierSession(
                Target("2026-09-30", "Arden Vale"),
                Line("2026-09-30", "Died", ",\"KillerName\":\"Cmdr Arden Vale\"")));

        var text = Assert.Single(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale"))))).Text;

        Assert.EndsWith("they killed you.", text);
    }

    [Fact]
    public void ASecondTargetOfTheSameCommanderInTheSessionIsSilent()
    {
        var (callout, log) = Build(true, EarlierSession(Target("2026-09-30", "Arden Vale")));

        Assert.Single(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")))));
        Assert.Empty(callout.Examine(Tick(log, [Target("2026-10-21", "arden vale")])));
    }

    [Fact]
    public void ACommanderMetOnlyInTheCurrentSessionIsSilent()
    {
        var (callout, log) = Build(true);

        Assert.Empty(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")))));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var (callout, log) = Build(true, EarlierSession(Target("2026-09-30", "Arden Vale")));

        Assert.Empty(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")), priming: true)));
    }

    [Fact]
    public void NothingIsSaidUntilHistoryIsFoldedAndThenTheCommanderStillSpeaks()
    {
        var (callout, log) = Build(false, EarlierSession(Target("2026-09-30", "Arden Vale")));

        Assert.Empty(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")))));

        log.FoldHistory([], TestContext.Current.CancellationToken);

        Assert.Single(callout.Examine(Tick(log, [Target("2026-10-21", "Arden Vale")])));
    }

    [Fact]
    public void UnderThreatNothingIsSaidAndTheCommanderIsNotMarkedAsTold()
    {
        var (callout, log) = Build(true, EarlierSession(Target("2026-09-30", "Arden Vale")));

        var danger = new GameStatus { Flags = StatusFlags.InMainShip | StatusFlags.ShieldsUp | StatusFlags.InDanger, ReadAt = Now };

        Assert.Empty(callout.Examine(Tick(log, NewSession(Target("2026-10-21", "Arden Vale")), status: danger)));
        Assert.Single(callout.Examine(Tick(log, [Target("2026-10-21", "Arden Vale")])));
    }

    [Fact]
    public void AnNpcTargetIsSilent()
    {
        var (callout, log) = Build(true, EarlierSession(Target("2026-09-30", "Arden Vale")));

        var npc = Line("2026-10-21", "ShipTargeted", ",\"TargetLocked\":true,\"PilotName\":\"$npc_name_decorate:#name=Arden Vale;\"");

        Assert.Empty(callout.Examine(Tick(log, NewSession(npc))));
    }
}
