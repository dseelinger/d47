using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// <see cref="EngineerAccess.CriteriaFor"/> reading the structured tests <c>tools/gen-engineers.py</c>
/// derives from Frontier's prose (#183), rather than <see cref="EngineerProgressState"/> alone.
/// </summary>
public class EngineerPrerequisitesAreDecidedFromFoldedEvidenceTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    private static D47.Core.Engineers.UnlockEvidence Evidence(
        EngineerProgressState? progress = null,
        RankState? ranks = null,
        CareerStatistics? statistics = null,
        ReputationState? reputation = null,
        EngineerContributions? contributions = null,
        DateTimeOffset? sessionStart = null) =>
        new(progress, ranks, statistics, reputation, contributions, sessionStart);

    /// <summary>Not invited: the invitation task reads straight off the reputation evidence.</summary>
    [Fact]
    public void RankDecidesBothWaysAndIsNullWithNoRankEvent()
    {
        var hera = Named("Hera Tani");

        var atRank = RankState.Empty.Apply(
            Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Rank","Combat":0,"Trade":0,"Explore":0,"Soldier":0,"Exobiologist":0,"CQC":0,"Empire":1,"Federation":0}"""));

        var met = EngineerAccess.CriteriaFor(hera, Evidence(ranks: atRank))
            .Single(criterion => criterion.Text == hera.Meeting);
        Assert.True(met.Met);
        Assert.Null(met.Reading);

        var belowRank = RankState.Empty.Apply(
            Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Rank","Combat":0,"Trade":0,"Explore":0,"Soldier":0,"Exobiologist":0,"CQC":0,"Empire":0,"Federation":0}"""));

        var unmet = EngineerAccess.CriteriaFor(hera, Evidence(ranks: belowRank))
            .Single(criterion => criterion.Text == hera.Meeting);
        Assert.False(unmet.Met);

        var noRank = EngineerAccess.CriteriaFor(hera, Evidence())
            .Single(criterion => criterion.Text == hera.Meeting);
        Assert.Null(noRank.Met);
    }

    /// <summary>A statistic decides true only — below the threshold is unknown, with the reading shown.</summary>
    [Fact]
    public void AStatisticBelowThresholdIsNullWithAReadingNotAFalse()
    {
        var palin = Named("Professor Palin");

        var short_ = CareerStatistics.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Statistics","Exploration":{"Greatest_Distance_From_Start":4557.72}}"""));

        var below = EngineerAccess.CriteriaFor(palin, Evidence(statistics: short_))
            .Single(criterion => criterion.Text == palin.Meeting);
        Assert.Null(below.Met);
        Assert.Equal("Last reported 4,558", below.Reading);

        var reached = CareerStatistics.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Statistics","Exploration":{"Greatest_Distance_From_Start":7500.41}}"""));

        var above = EngineerAccess.CriteriaFor(palin, Evidence(statistics: reached))
            .Single(criterion => criterion.Text == palin.Meeting);
        Assert.True(above.Met);
        Assert.Null(above.Reading);
    }

    /// <summary>A contribution decides both ways once a total exists, and reads out the shortfall.</summary>
    [Fact]
    public void AContributionShortOfTheQuantityIsFalseWithAReading()
    {
        var marco = Named("Marco Qwent");

        var short_ = EngineerContributions.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerContribution","EngineerID":300200,"Type":"Commodity","Commodity":"modularterminals","TotalQuantity":18}"""));

        var below = EngineerAccess.CriteriaFor(marco, Evidence(contributions: short_))
            .Single(criterion => criterion.Text == marco.Unlock);
        Assert.False(below.Met);
        Assert.Equal("18 of 25 handed over", below.Reading);

        var reached = EngineerContributions.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerContribution","EngineerID":300200,"Type":"Commodity","Commodity":"modularterminals","TotalQuantity":25}"""));

        var above = EngineerAccess.CriteriaFor(marco, Evidence(contributions: reached))
            .Single(criterion => criterion.Text == marco.Unlock);
        Assert.True(above.Met);

        var none = EngineerAccess.CriteriaFor(marco, Evidence())
            .Single(criterion => criterion.Text == marco.Unlock);
        Assert.Null(none.Met);
    }

    /// <summary>A reputation reading is used however old, and its date shows only once it predates the session.</summary>
    [Fact]
    public void AReputationReadingShowsItsDateOnlyWhenStale()
    {
        var baltanos = Named("Baltanos");
        var sessionStart = DateTimeOffset.Parse("2026-08-18T09:00:00Z");

        var freshLow = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:30:00Z","event":"Location","StarSystem":"Deriso","Factions":[{"Name":"Colonia Council","MyReputation":23.49}]}"""));

        var fresh = EngineerAccess.CriteriaFor(baltanos, Evidence(reputation: freshLow, sessionStart: sessionStart))
            .Single(criterion => criterion.Text == baltanos.Meeting);
        Assert.False(fresh.Met);
        Assert.Null(fresh.Reading);

        var staleLow = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T08:00:00Z","event":"Location","StarSystem":"Deriso","Factions":[{"Name":"Colonia Council","MyReputation":23.49}]}"""));

        var stale = EngineerAccess.CriteriaFor(baltanos, Evidence(reputation: staleLow, sessionStart: sessionStart))
            .Single(criterion => criterion.Text == baltanos.Meeting);
        Assert.False(stale.Met);
        Assert.NotNull(stale.Reading);

        var none = EngineerAccess.CriteriaFor(baltanos, Evidence(sessionStart: sessionStart))
            .Single(criterion => criterion.Text == baltanos.Meeting);
        Assert.Null(none.Met);
    }

    /// <summary>An "at-most" reputation test passes below the next band's floor and fails at or above it.</summary>
    [Fact]
    public void AnAtMostReputationTestIsTheCeilingOfTheNextBand()
    {
        var uma = Named("Uma Laszlo");

        var wellBelow = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Xuane","Factions":[{"Name":"Sirius Corporation","MyReputation":-40}]}"""));

        Assert.True(EngineerAccess.CriteriaFor(uma, Evidence(reputation: wellBelow))
            .Single(criterion => criterion.Text == uma.Meeting).Met);

        var atNeutral = ReputationState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Xuane","Factions":[{"Name":"Sirius Corporation","MyReputation":-20}]}"""));

        Assert.False(EngineerAccess.CriteriaFor(uma, Evidence(reputation: atNeutral))
            .Single(criterion => criterion.Text == uma.Meeting).Met);
    }

    /// <summary>An unlocked engineer's criteria are all true, and none carries a reading.</summary>
    [Fact]
    public void AnUnlockedEngineersCriteriaAreAllTrueWithNoReading()
    {
        var progress = EngineerProgressState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Hera Tani","EngineerID":300090,"Progress":"Unlocked","Rank":1}]}"""));

        var criteria = EngineerAccess.CriteriaFor(Named("Hera Tani"), Evidence(progress: progress));

        Assert.All(criteria, criterion =>
        {
            Assert.True(criterion.Met);
            Assert.Null(criterion.Reading);
        });
    }

    /// <summary>Invited makes the invitation task true without evidence saying anything about the reading.</summary>
    [Fact]
    public void AnInvitedEngineersMeetingTaskIsTrueEvenWithAStaleReading()
    {
        var baltanos = Named("Baltanos");

        var invited = EngineerProgressState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Baltanos","EngineerID":400010,"Progress":"Invited"}]}"""));

        var criterion = EngineerAccess.CriteriaFor(baltanos, Evidence(progress: invited))
            .Single(entry => entry.Text == baltanos.Meeting);

        Assert.True(criterion.Met);
        Assert.Null(criterion.Reading);
    }

    /// <summary>Domino Green's tribute carries no unlock test at all, so it is null whatever the state holds.</summary>
    [Fact]
    public void DominoGreensTributeIsNullWhateverTheStateHolds()
    {
        var domino = Named("Domino Green");
        Assert.Null(domino.UnlockTest);

        var withEverything = Evidence(
            ranks: RankState.Empty,
            statistics: CareerStatistics.Empty,
            reputation: ReputationState.Empty,
            contributions: EngineerContributions.Empty,
            sessionStart: DateTimeOffset.UtcNow);

        var criterion = EngineerAccess.CriteriaFor(domino, withEverything)
            .Single(entry => entry.Text == domino.Unlock);

        Assert.Null(criterion.Met);
        Assert.Null(criterion.Reading);
    }
}
