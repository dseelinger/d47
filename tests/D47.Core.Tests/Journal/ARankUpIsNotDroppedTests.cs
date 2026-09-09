using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A rank-up survives being read: "repeated once per module, even though my relationship with the
/// engineer is 5".</summary>
public class ARankUpIsNotDroppedTests
{
    /// <summary>The Commander's own morning, verbatim: Selene Jean, invited to rank 5.</summary>
    private const string Snapshot =
        """
        { "timestamp":"2026-08-24T11:57:12Z", "event":"EngineerProgress",
          "Engineers":[ {"Engineer":"Selene Jean","EngineerID":300210,"Progress":"Invited"},
                        {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":3,
                         "RankProgress":40} ] }
        """;

    private static EngineerProgressState Fold(params string[] lines)
    {
        var state = EngineerProgressState.Empty;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state = state.Apply(parsed!);
        }

        return state;
    }

    private static string Delta(string body) =>
        $$"""{ "timestamp":"2026-08-24T14:30:00Z", "event":"EngineerProgress", {{body}} }""";

    /// <summary>
    /// The reported sequence, timestamps and all: unlocked at 14:28 and rank 5 by 14:32, four minutes
    /// and four rank-ups later.
    /// </summary>
    [Fact]
    public void FourRankUpsInFourMinutesAllLand()
    {
        var state = Fold(
            Snapshot,
            Delta("""
                  "Engineer":"Selene Jean","EngineerID":300210,"Progress":"Unlocked","Rank":1
                  """),
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":2 """),
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":3 """),
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":4 """),
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":5 """));

        var selene = state.For(300210);

        Assert.NotNull(selene);
        Assert.Equal(5, selene!.Rank);
        Assert.True(selene.IsUnlocked);
    }

    /// <summary>
    /// The unlock word is not in a rank-up and must not be lost with it: a merge that took only the
    /// fields present would leave a standing nobody can name, which is the very thing the snapshot
    /// reader's all-three rule exists to refuse.
    /// </summary>
    [Fact]
    public void ARankUpKeepsTheProgressWordItDoesNotCarry()
    {
        var state = Fold(
            Snapshot,
            Delta("""
                  "Engineer":"Selene Jean","EngineerID":300210,"Progress":"Unlocked","Rank":1
                  """),
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":2 """));

        Assert.Equal("Unlocked", state.For(300210)!.Progress);
    }

    /// <summary>One engineer moving leaves the others exactly as they were.</summary>
    [Fact]
    public void TheOtherEngineersAreUntouched()
    {
        var state = Fold(Snapshot, Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":4 """));

        var farseer = state.For(300100);

        Assert.NotNull(farseer);
        Assert.Equal(3, farseer!.Rank);
        Assert.Equal(40, farseer.RankProgress);
        Assert.Equal(2, state.Standings.Count);
    }

    /// <summary>The percentage does not ride along.</summary>
    [Fact]
    public void APercentageDoesNotSurviveTheRankItWasMeasuredAgainst()
    {
        var state = Fold(Snapshot, Delta(""" "Engineer":"Felicity Farseer","EngineerID":300100,"Rank":4 """));

        var farseer = state.For(300100);

        Assert.Equal(4, farseer!.Rank);
        Assert.Null(farseer.RankProgress);
    }

    /// <summary>
    /// And it does survive a delta that is not a rank-up, which is the other half of the same rule:
    /// absent means unchanged, so a <c>Progress</c>-only event leaves both numbers alone.
    /// </summary>
    [Fact]
    public void APercentageSurvivesADeltaThatIsNotARankUp()
    {
        var state = Fold(
            Snapshot,
            Delta(""" "Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked" """));

        var farseer = state.For(300100);

        Assert.Equal(3, farseer!.Rank);
        Assert.Equal(40, farseer.RankProgress);
    }

    /// <summary>A rank for an engineer with no row yet.</summary>
    [Fact]
    public void ARankForAnEngineerNobodyHasHeardOfIsAnUnlock()
    {
        var state = Fold(Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":2 """));

        var selene = state.For(300210);

        Assert.NotNull(selene);
        Assert.Equal(2, selene!.Rank);
        Assert.True(selene.IsUnlocked);
    }

    /// <summary>
    /// An event with no id is still nothing at all: it cannot be tied to the directory and there is no
    /// row it could be merged onto.
    /// </summary>
    [Fact]
    public void ADeltaWithNoIdChangesNothing()
    {
        var state = Fold(Snapshot, Delta(""" "Engineer":"Selene Jean","Rank":2 """));

        Assert.Equal("Invited", state.For(300210)!.Progress);
        Assert.Null(state.For(300210)!.Rank);
    }

    /// <summary>
    /// The snapshot still replaces rather than merging, which is the distinction this change carries
    /// down into the fields rather than replacing.
    /// </summary>
    [Fact]
    public void ASnapshotStillReplacesTheWholeSet()
    {
        var state = Fold(
            Snapshot,
            Delta(""" "Engineer":"Selene Jean","EngineerID":300210,"Rank":5 """),
            """
            { "timestamp":"2026-08-24T15:00:00Z", "event":"EngineerProgress",
              "Engineers":[ {"Engineer":"Selene Jean","EngineerID":300210,"Progress":"Unlocked","Rank":5,
                             "RankProgress":12} ] }
            """);

        Assert.Single(state.Standings);
        Assert.Equal(12, state.For(300210)!.RankProgress);
        Assert.Null(state.For(300100));
    }
}
