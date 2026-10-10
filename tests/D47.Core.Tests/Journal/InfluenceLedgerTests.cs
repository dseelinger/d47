using System.Text;
using D47.Core.Journal;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class InfluenceLedgerTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    private static string Completed(int minute, params (string Faction, string Trend, string Marks)[] effects)
    {
        var factions = new StringBuilder();

        foreach (var (faction, trend, marks) in effects)
        {
            factions.Append(factions.Length > 0 ? "," : "")
                .Append($$"""{"Faction":"{{faction}}","Effects":[{"Effect":"x","Effect_Localised":"$#MinorFaction;"}],"Influence":[{"SystemAddress":1,"Trend":"{{trend}}","Influence":"{{marks}}"}],"ReputationTrend":"UpGood","Reputation":"+"}""");
        }

        return JournalCorpus.Event(
            Evening.AddMinutes(minute),
            "MissionCompleted",
            $"\"Faction\":\"Party of Yoru\",\"Name\":\"Mission_Delivery\",\"MissionID\":{minute},\"Reward\":1000,\"FactionEffects\":[{factions}]");
    }

    private static InfluenceLedger Fold(params string[] lines)
    {
        var ledger = InfluenceLedger.Empty;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            ledger = ledger.Apply(parsed!);
        }

        return ledger;
    }

    [Fact]
    public void AnUpAndADownForTheSameFactionAreReportedSeparately()
    {
        var ledger = Fold(
            Completed(1, ("Party of Yoru", "UpGood", "++")),
            Completed(2, ("Party of Yoru", "UpGood", "++")),
            Completed(3, ("Party of Yoru", "UpGood", "+")),
            Completed(4, ("Party of Yoru", "DownBad", "+")));

        Assert.Equal("Influence from missions: Party of Yoru up ++ x2, + x1, down + x1.", ledger.Describe());
    }

    [Fact]
    public void ADownBadTrendWithPlusMarksIsReportedAsDown()
    {
        var ledger = Fold(Completed(1, ("Party of Yoru", "UpGood", "+"), ("Tirada Jet Comms Limited", "DownBad", "+")));

        Assert.Equal(
            "Influence from missions: Party of Yoru up + x1; Tirada Jet Comms Limited down + x1.",
            ledger.Describe());
    }

    [Fact]
    public void FactionsAreOrderedByTotalMarksThenFirstSeen()
    {
        var ledger = Fold(
            Completed(1, ("B", "UpGood", "+")),
            Completed(2, ("A", "UpGood", "+")),
            Completed(3, ("A", "UpGood", "+")));

        Assert.Equal("Influence from missions: A up + x2; B up + x1.", ledger.Describe());
    }

    [Fact]
    public void OnlyFiveFactionsAreNamedAndTheRestAreCounted()
    {
        var ledger = Fold(Completed(
            1,
            ("F1", "UpGood", "+"),
            ("F2", "UpGood", "+"),
            ("F3", "UpGood", "+"),
            ("F4", "UpGood", "+"),
            ("F5", "UpGood", "+"),
            ("F6", "UpGood", "+"),
            ("F7", "UpGood", "+")));

        Assert.Equal(
            "Influence from missions: F1 up + x1; F2 up + x1; F3 up + x1; F4 up + x1; F5 up + x1; and 2 more factions.",
            ledger.Describe());
    }

    [Fact]
    public void AnUnknownTrendOrNoInfluenceAddsNothing()
    {
        var ledger = Fold(
            Completed(1, ("A", "None", "+")),
            JournalCorpus.Event(Evening, "MissionCompleted", "\"Reward\":5,\"FactionEffects\":[{\"Faction\":\"B\",\"Influence\":[]}]"));

        Assert.True(ledger.IsEmpty);
        Assert.Null(ledger.Describe());
    }

    [Fact]
    public void ASessionWithNoMissionCompletionsHasNoInfluenceLine()
    {
        var ledger = Fold(JournalCorpus.Jump(Evening, "Deciat", 8.09));

        Assert.Null(ledger.Describe());
    }

    [Fact]
    public void ALoadGameClearsTheSessionsInfluence()
    {
        var session = SessionSummary.Empty;

        foreach (var line in new[] { JournalCorpus.LoadGame(Evening), Completed(1, ("A", "UpGood", "+")) })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            session = session.Apply(parsed!);
        }

        Assert.False(session.Influence.IsEmpty);

        Assert.True(JournalEvent.TryParse(JournalCorpus.LoadGame(Evening.AddHours(1)), NullLogger.Instance, out var again));
        Assert.True(session.Apply(again!).Influence.IsEmpty);
    }

    [Fact]
    public void TheLogbookStatesTheSameInfluenceLineAsOneMissionFact()
    {
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            Completed(1, ("Party of Yoru", "UpGood", "++")),
            Completed(2, ("Party of Yoru", "DownBad", "+")));

        var digest = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(
            _corpus.Files,
            new LogRange { Span = LogSpan.Session, From = Evening.AddMinutes(-1), To = Evening.AddDays(1), Label = "the last session" });

        var fact = Assert.Single(digest.Facts, fact => fact.Statement.StartsWith("Influence from missions", StringComparison.Ordinal));
        Assert.Equal(LogFactKind.Mission, fact.Kind);
        Assert.Equal("Influence from missions: Party of Yoru up ++ x1, down + x1.", fact.Statement);
    }

    [Fact]
    public void ALogbookWithoutMissionInfluenceHasNoInfluenceFact()
    {
        _corpus.Journal(Evening, JournalCorpus.LoadGame(Evening), JournalCorpus.Jump(Evening.AddMinutes(1), "Deciat", 8.09));

        var digest = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(
            _corpus.Files,
            new LogRange { Span = LogSpan.Session, From = Evening.AddMinutes(-1), To = Evening.AddDays(1), Label = "the last session" });

        Assert.DoesNotContain(digest.Facts, fact => fact.Statement.StartsWith("Influence from missions", StringComparison.Ordinal));
    }
}
