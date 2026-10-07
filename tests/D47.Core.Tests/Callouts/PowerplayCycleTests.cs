using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class PowerplayCycleTests
{
    // Thursday 2026-10-01 07:00 UTC to Thursday 2026-10-08 07:00 UTC.
    private static readonly DateTimeOffset CycleStart = DateTimeOffset.Parse("2026-10-01T07:00:00Z");
    private static readonly DateTimeOffset CycleEnd = CycleStart.AddDays(7);

    private const string Pledge =
        """{"timestamp":"2026-09-28T10:00:00Z","event":"Powerplay","Power":"Li Yong-Rui","Rank":8,"Merits":44000}""";

    private static string Merits(string at, int gained, string power = "Li Yong-Rui") =>
        "{\"timestamp\":\"" + at + "\",\"event\":\"PowerplayMerits\",\"Power\":\"" + power
        + "\",\"MeritsGained\":" + gained + ",\"TotalMerits\":45000}";

    private const string Login =
        """{"timestamp":"2026-09-28T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines) => StoreFrom(new GameStateStore(), lines).Active!;

    private static GameStateStore StoreFrom(GameStateStore store, params string[] lines)
    {
        store.Apply(Event(Login));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store;
    }

    private static CalloutContext Context(CommanderGameState state, DateTimeOffset now, bool priming = false) =>
        new(now, priming, state, GameStatus.Unknown, NavRoute.None, []);

    private static long ThisCycle(CommanderGameState state) => state.CycleMerits.Since(CycleStart);

    [Fact]
    public void OnlyMeritsAfterTheBoundaryCountThisCycle()
    {
        var state = StateFrom(
            Pledge,
            Merits("2026-09-30T20:00:00Z", 500),
            Merits("2026-10-01T06:59:59Z", 40),
            Merits("2026-10-01T07:00:00Z", 29),
            Merits("2026-10-03T12:00:00Z", 1000));

        var week = CommodityLedger.Week(DateTimeOffset.Parse("2026-10-05T12:00:00Z"), DayOfWeek.Thursday, 7);

        Assert.Equal(CycleStart, week.From);
        Assert.Equal(1029, state.CycleMerits.Since(week.From));
    }

    [Fact]
    public void MeritsForAnotherPowerLeaveTheCycleTotalAlone()
    {
        var state = StateFrom(
            Pledge,
            Merits("2026-10-02T12:00:00Z", 100),
            Merits("2026-10-02T12:05:00Z", 300, "Aisling Duval"));

        Assert.Equal(100, ThisCycle(state));
    }

    [Fact]
    public void DefectingStartsTheCycleTotalAgain()
    {
        var state = StateFrom(
            Pledge,
            Merits("2026-10-02T12:00:00Z", 100),
            """{"timestamp":"2026-10-02T13:00:00Z","event":"PowerplayDefect","FromPower":"Li Yong-Rui","ToPower":"Aisling Duval"}""",
            Merits("2026-10-02T14:00:00Z", 7, "Aisling Duval"));

        Assert.Equal(7, ThisCycle(state));
    }

    [Fact]
    public void JoiningAndLeavingStartTheCycleTotalAgain()
    {
        var left = StateFrom(
            Pledge,
            Merits("2026-10-02T12:00:00Z", 100),
            """{"timestamp":"2026-10-02T13:00:00Z","event":"PowerplayLeave","Power":"Li Yong-Rui"}""");

        Assert.Equal(0, ThisCycle(left));

        var joined = StateFrom(
            Pledge,
            Merits("2026-10-02T12:00:00Z", 100),
            """{"timestamp":"2026-10-02T13:00:00Z","event":"PowerplayJoin","Power":"Li Yong-Rui"}""");

        Assert.Equal(0, ThisCycle(joined));
    }

    [Fact]
    public void TheLoginSnapshotDoesNotStartItAgain()
    {
        var state = StateFrom(
            Pledge,
            Merits("2026-10-02T12:00:00Z", 100),
            Pledge.Replace("2026-09-28T10:00:00Z", "2026-10-03T09:00:00Z", StringComparison.Ordinal));

        Assert.Equal(100, ThisCycle(state));
    }

    [Fact]
    public void MeritsFromAnEarlierJournalThisCycleCountAfterARestart()
    {
        var folder = Directory.CreateTempSubdirectory("d47-powerplay-cycle-");

        try
        {
            var earlier = Path.Combine(folder.FullName, "Journal.2026-10-02T120000.01.log");
            var current = Path.Combine(folder.FullName, "Journal.2026-10-04T090000.01.log");

            File.WriteAllLines(earlier, [Login, Pledge, Merits("2026-09-30T12:00:00Z", 900), Merits("2026-10-02T12:30:00Z", 400)]);

            string[] today = [Login, Pledge, Merits("2026-10-04T09:30:00Z", 29), Merits("2026-10-04T09:30:00Z", 29)];
            File.WriteAllLines(current, today);

            // The walk finished before the Commander was met: the live journal adds to what it found.
            var walkedFirst = PowerplayCycleBackfill.FromHistory([earlier], NullLogger.Instance, TestContext.Current.CancellationToken);
            var store = StoreFrom(new GameStateStore { RestoreCycleMerits = walkedFirst.GetValueOrDefault }, today[1..]);

            Assert.Equal(458, ThisCycle(store.Active!));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void ALateWalkCountsTheCurrentJournalOnce()
    {
        var folder = Directory.CreateTempSubdirectory("d47-powerplay-cycle-");

        try
        {
            var earlier = Path.Combine(folder.FullName, "Journal.2026-10-02T120000.01.log");
            var current = Path.Combine(folder.FullName, "Journal.2026-10-04T090000.01.log");

            File.WriteAllLines(earlier, [Login, Pledge, Merits("2026-10-02T12:30:00Z", 400)]);

            string[] read = [Login, Pledge, Merits("2026-10-04T09:30:00Z", 29), Merits("2026-10-04T09:30:00Z", 29)];
            File.WriteAllLines(current, read);

            IReadOnlyDictionary<string, PowerplayCycleMerits>? walked = null;
            var store = new GameStateStore { RestoreCycleMerits = fid => walked?.GetValueOrDefault(fid) };

            // Primed from the current journal before the walk is done.
            StoreFrom(store, read[1..]);
            Assert.Equal(58, ThisCycle(store.Active!));

            walked = PowerplayCycleBackfill.FromHistory([earlier, current], NullLogger.Instance, TestContext.Current.CancellationToken);

            // Then one more gain in the same second as the walk's last, and one after.
            store.Apply(Event(Merits("2026-10-04T09:30:00Z", 29)));
            store.Apply(Event(Merits("2026-10-04T10:00:00Z", 5)));
            store.RestoreLate();

            Assert.Equal(400 + 29 + 29 + 29 + 5, ThisCycle(store.Active!));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void TheSituationLineCarriesTheCycleAndWhenItEnds()
    {
        var state = StateFrom(
            Pledge.Replace("\"Merits\":44000", "\"Merits\":45292", StringComparison.Ordinal),
            Merits("2026-10-02T12:00:00Z", 1000),
            Merits("2026-10-03T12:00:00Z", 29).Replace("45000", "45292", StringComparison.Ordinal));

        var said = Situation.Describe(state, now: CycleEnd - new TimeSpan(2, 5, 0, 0));

        Assert.Contains(
            "Powerplay: pledged to Li Yong-Rui, rank 8, 45,292 merits; 1,029 this cycle, which ends in 2 days 5 hours.",
            said,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSituationLineFollowsTheBoundarySetting()
    {
        var state = StateFrom(Pledge, Merits("2026-10-02T12:00:00Z", 10));

        // Friday 00:00 UTC: the cycle from Friday 2026-10-02 runs past the Thursday boundary.
        var said = Situation.Describe(
            state, now: DateTimeOffset.Parse("2026-10-08T18:00:00Z"), weekBoundaryDay: DayOfWeek.Friday, weekBoundaryHourUtc: 0);

        Assert.Contains("; 10 this cycle, which ends in 6 hours.", said, StringComparison.Ordinal);
    }

    [Fact]
    public void TwelveHoursBeforeTheEndItSaysTheMeritsEarned()
    {
        var state = StateFrom(Pledge, Merits("2026-10-02T12:00:00Z", 1000), Merits("2026-10-03T12:00:00Z", 29));
        var callout = new PowerplayCycleCallout();

        Assert.Empty(callout.Examine(Context(state, CycleEnd - TimeSpan.FromHours(12) - TimeSpan.FromSeconds(1))));

        var spoken = Assert.Single(callout.Examine(Context(state, CycleEnd - TimeSpan.FromHours(12))));

        Assert.Equal(
            "The Powerplay cycle ends in twelve hours. You have earned 1,029 merits for Li Yong-Rui this cycle.",
            spoken.Text);

        Assert.Empty(callout.Examine(Context(state, CycleEnd - TimeSpan.FromHours(6))));
    }

    [Fact]
    public void WithNothingEarnedItSaysSo()
    {
        var state = StateFrom(Pledge, Merits("2026-09-30T12:00:00Z", 1000));

        var spoken = Assert.Single(new PowerplayCycleCallout().Examine(Context(state, CycleEnd - TimeSpan.FromHours(12))));

        Assert.Equal("The Powerplay cycle ends in twelve hours, and you have earned no merits this cycle.", spoken.Text);
    }

    [Fact]
    public void NothingIsSaidWhilePrimingAndItIsSaidOnTheFirstTickAfter()
    {
        var state = StateFrom(Pledge);
        var callout = new PowerplayCycleCallout();
        var late = CycleEnd - TimeSpan.FromHours(5);

        Assert.Empty(callout.Examine(Context(state, late, priming: true)));

        var spoken = Assert.Single(callout.Examine(Context(state, late)));
        Assert.StartsWith("The Powerplay cycle ends in five hours", spoken.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ItIsSaidOncePerCycleAcrossRestarts()
    {
        var state = StateFrom(Pledge);
        string? remembered = null;

        PowerplayCycleCallout Started() => new()
        {
            LastSaidCycle = () => remembered,
            RememberSaidCycle = cycle => remembered = cycle,
        };

        Assert.Single(Started().Examine(Context(state, CycleEnd - TimeSpan.FromHours(10))));
        Assert.Empty(Started().Examine(Context(state, CycleEnd - TimeSpan.FromHours(3))));

        // The next cycle says it again.
        Assert.Single(Started().Examine(Context(state, CycleEnd.AddDays(7) - TimeSpan.FromHours(11))));
    }

    [Fact]
    public void TheSaidCycleIsLoadedOnceNotOnEveryTick()
    {
        var state = StateFrom(Pledge);
        var loads = 0;
        string? remembered = null;

        var callout = new PowerplayCycleCallout
        {
            LastSaidCycle = () =>
            {
                loads++;
                return remembered;
            },
            RememberSaidCycle = cycle => remembered = cycle,
        };

        Assert.Single(callout.Examine(Context(state, CycleEnd - TimeSpan.FromHours(10))));

        for (var hour = 9; hour >= 1; hour--)
        {
            Assert.Empty(callout.Examine(Context(state, CycleEnd - TimeSpan.FromHours(hour))));
        }

        Assert.Equal(1, loads);
    }

    [Fact]
    public void ACycleThatEndedWhileOffIsNotMentioned()
    {
        var state = StateFrom(Pledge);

        Assert.Empty(new PowerplayCycleCallout().Examine(Context(state, CycleEnd + TimeSpan.FromHours(1))));
    }

    [Fact]
    public void UnpledgedNothingIsSaid()
    {
        var state = StateFrom();

        Assert.Empty(new PowerplayCycleCallout().Examine(Context(state, CycleEnd - TimeSpan.FromHours(12))));
    }

    [Fact]
    public void WithTheRowOffNothingIsSaid()
    {
        var state = StateFrom(Pledge);
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(new PowerplayCycleCallout());
        engine.SetEnabled("powerplay-cycle", false);

        engine.Tick(Context(state, CycleEnd - TimeSpan.FromHours(12)));

        Assert.Empty(engine.Drain());
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().PowerplayCycle);
}
