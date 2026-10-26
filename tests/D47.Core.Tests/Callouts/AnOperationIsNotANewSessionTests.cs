using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Reminders;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Reminders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Callouts;

/// <summary>Entering or leaving an Operation writes a LoadGame, which is not a session start for any consumer of it.</summary>
public class AnOperationIsNotANewSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 20, 0, 0, TimeSpan.Zero);

    private static string Line(TimeSpan since, string kind, string fields = "") =>
        $$"""{ "timestamp":"{{(Start + since).ToString("yyyy-MM-ddTHH:mm:ssZ")}}", "event":"{{kind}}", "FID":"F1", "Commander":"Fixture"{{fields}} }""";

    private static readonly string[] SessionStart =
    [
        Line(TimeSpan.Zero, "Commander"),
        Line(TimeSpan.Zero, "LoadGame", ", \"Credits\":1000, \"Odyssey\":true"),
        Line(TimeSpan.Zero, "GameModeChange", ", \"GameMode\":\"MainGame\""),
        Line(TimeSpan.FromMinutes(1), "Bounty", ", \"TotalReward\":1000"),
    ];

    private static readonly string[] OperationEntry =
    [
        Line(TimeSpan.FromMinutes(5), "Commander"),
        Line(TimeSpan.FromMinutes(5), "LoadGame", ", \"Credits\":2000"),
        Line(TimeSpan.FromMinutes(5), "GameModeChange", ", \"GameMode\":\"Operation\""),
        Line(TimeSpan.FromMinutes(10), "Bounty", ", \"TotalReward\":500"),
    ];

    private static readonly string[] OperationExit =
    [
        Line(TimeSpan.FromMinutes(40), "Commander"),
        Line(TimeSpan.FromMinutes(40), "LoadGame", ", \"Credits\":2500"),
        Line(TimeSpan.FromMinutes(40), "GameModeChange", ", \"GameMode\":\"MainGame\""),
        Line(TimeSpan.FromMinutes(41), "Bounty", ", \"TotalReward\":500"),
    ];

    private static List<JournalEvent> Marked(params string[][] groups) =>
        [.. OperationMarker.Mark(groups.SelectMany(group => group).Select(Parse))];

    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext At(TimeSpan since, bool priming, params JournalEvent[] events) =>
        new(Start + since, priming, null, GameStatus.Unknown, NavRoute.None, events);

    [Fact]
    public void TheSessionSummaryKeepsWhatWasEarnedBeforeAndInsideTheOperation()
    {
        var summary = SessionSummary.Empty;

        foreach (var journalEvent in Marked(SessionStart, OperationEntry, OperationExit))
        {
            summary = summary.Apply(journalEvent);
        }

        Assert.Equal(Start, summary.StartedAt);
        Assert.Equal(1000, summary.Balance);
        Assert.Equal(2000, summary.BountyEarnings);
    }

    [Fact]
    public void TheSessionLengthClockCountsFromTheSessionStart()
    {
        var callout = new SessionLengthCallout();

        callout.Examine(At(TimeSpan.FromMinutes(1), false, [.. Marked(SessionStart, OperationEntry, OperationExit)])).ToList();

        var said = Assert.Single(callout.Examine(At(TimeSpan.FromHours(4), false)));

        Assert.Equal(SessionLengthCallout.Key, said.Key);
    }

    [Fact]
    public void NoWelcomeIsSpokenOnTheExit()
    {
        var callout = new SessionCallout();
        var marked = Marked(SessionStart, OperationEntry, OperationExit);

        Assert.Single(callout.Examine(At(TimeSpan.Zero, false, [.. marked.Take(SessionStart.Length)])));

        var rest = marked.Skip(SessionStart.Length).ToArray();

        Assert.Empty(callout.Examine(At(TimeSpan.FromMinutes(5), false, [.. rest.Take(OperationEntry.Length)])));
        Assert.Empty(callout.Examine(At(TimeSpan.FromMinutes(41), false, [.. rest.Skip(OperationEntry.Length)])));
    }

    [Fact]
    public void PerSessionMemorySurvivesBothBoundaries()
    {
        var callout = new KillCallout();
        var marked = Marked(SessionStart, OperationEntry, OperationExit);

        var said = callout.Examine(At(TimeSpan.FromMinutes(41), false, [.. marked])).ToList();

        var first = Assert.Single(said, announcement => announcement.Key == KillCallout.FirstKey);
        Assert.Contains("First kill of the session", first.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ANextSessionReminderFiresAtNeitherBoundary()
    {
        var bench = new ReminderBench();
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var marked = Marked(SessionStart, OperationEntry, OperationExit);

        Assert.Empty(bench.Say(state, false, [.. marked.Take(SessionStart.Length)]));

        bench.Arm("F1", JournalTrigger.NextSession, "Check the Community Goal.");

        Assert.Empty(bench.Say(state, false, [.. marked.Skip(SessionStart.Length)]));
        Assert.Equal(JournalReminderState.Armed, Assert.Single(bench.Store.For("F1")).State);

        var later = Line(TimeSpan.FromHours(3), "LoadGame");
        var next = Marked([later, Line(TimeSpan.FromHours(3), "GameModeChange", ", \"GameMode\":\"MainGame\"")]);

        Assert.Single(bench.Say(state, false, [.. next]));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AStoryCountsOneSessionNotThree()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Start.AddHours(-1), CancellationToken.None));

        foreach (var journalEvent in Marked(SessionStart, OperationEntry, OperationExit))
        {
            fixtures.Director.Observe(journalEvent, "F1");
        }

        Assert.Equal(1, fixtures.Stories.Current("F1")!.Sessions);
    }
}
