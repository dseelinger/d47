using System.Collections.Immutable;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>The Campaign page's Systems rows: influence, lead, frozen, state, and the intent set or defaulted.</summary>
public sealed class CampaignSystemRowsGiveEachSystemItsFiguresTests
{
    private static readonly ImmutableDictionary<string, SystemIntent> NoIntents = Campaign.None.Intents;

    private static IReadOnlyList<CampaignSystemRow> Rows(
        FactionReport report,
        SystemStandings? standings = null,
        IReadOnlyDictionary<string, SystemIntent>? intents = null) =>
        CampaignSystemRows.Of(report, standings ?? SystemStandings.Empty, intents ?? NoIntents, Now);

    [Fact]
    public void ASystemWithNoIntentTakesHoldWhereControlledAndLeaveAloneElsewhere()
    {
        var rows = Rows(Report(Place("Lave", 0.50, controls: true), Place("Diso", 0.20, controls: false)));

        var lave = rows.Single(row => row.System == "Lave");
        var diso = rows.Single(row => row.System == "Diso");

        Assert.Equal((SystemIntent.Hold, true), (lave.Intent, lave.IntentIsDefault));
        Assert.Equal((SystemIntent.LeaveAlone, true), (diso.Intent, diso.IntentIsDefault));
    }

    [Fact]
    public void AnIntentSetIsNotTheDefaultEvenWhenItEqualsIt()
    {
        var intents = NoIntents.SetItem("lave", SystemIntent.Hold).SetItem("Diso", SystemIntent.TakeControl);

        var rows = Rows(Report(Place("Lave", 0.50), Place("Diso", 0.20, controls: false)), intents: intents);

        Assert.Equal((SystemIntent.Hold, false), (rows[0].Intent, rows[0].IntentIsDefault));
        Assert.Equal((SystemIntent.TakeControl, false), (rows[1].Intent, rows[1].IntentIsDefault));
    }

    [Fact]
    public void TheLeadIsOverTheNearestRivalAndNegativeWhenBehind()
    {
        var ahead = Place("Lave", 0.50, others: [new FactionNeighbour("Lave Purple Gang", 0.20), new FactionNeighbour("Lave Party", 0.30)]);
        var behind = Place("Diso", 0.20, controls: false, others: [new FactionNeighbour("Diso Labour", 0.35)]);

        var rows = Rows(Report(ahead, behind));

        Assert.Equal(0.20, rows[0].Lead!.Value, 6);
        Assert.Equal(-0.15, rows[1].Lead!.Value, 6);
    }

    [Fact]
    public void ASystemWithNoRivalHasNoLead()
    {
        var row = Assert.Single(Rows(Report(Place("Lave", 1.0, others: []))));

        Assert.Null(row.Lead);
    }

    [Fact]
    public void ANewerJournalReadingGivesTheInfluenceAndTheLead()
    {
        var lave = Place("Lave", 0.50, others: [new FactionNeighbour("Lave Purple Gang", 0.20)], reportedAt: Now.AddDays(-2));
        var standings = SystemStandings.Empty.Apply(Jump(
            "Lave",
            Now.AddHours(-1),
            $"{Faction(Us, 0.45, active: "Boom")},{Faction("Lave Purple Gang", 0.40)}"));

        var row = Assert.Single(Rows(Report(lave), standings));

        Assert.True(row.OwnReading);
        Assert.Equal(0.45, row.Influence!.Value, 6);
        Assert.Equal(0.05, row.Lead!.Value, 6);
        Assert.Equal("Boom", row.State);
    }

    [Theory]
    [InlineData("War", true)]
    [InlineData("Election", false)]
    public void AConflictActiveOrPendingFreezesTheLead(string state, bool active)
    {
        var lave = Place("Lave", 0.40, active: active ? [state] : null, pending: active ? null : [state]);

        var row = Assert.Single(Rows(Report(lave)));

        Assert.True(row.Frozen);
    }

    [Fact]
    public void ABoomDoesNotFreezeTheLead()
    {
        var row = Assert.Single(Rows(Report(Place("Lave", 0.40, active: ["Boom"]))));

        Assert.False(row.Frozen);
    }

    [Fact]
    public void TheStateNamesActiveStatesThenPendingOnesAndNoneWhenThereAreNone()
    {
        var rows = Rows(Report(
            Place("Lave", 0.60, active: ["Boom"], pending: ["Retreat"]),
            Place("Diso", 0.40, pending: ["CivilUnrest"]),
            Place("Leesti", 0.20)));

        Assert.Equal(["Boom, Retreat pending", "Civil unrest pending", "None"], rows.Select(row => row.State));
    }

    [Fact]
    public void AReportOlderThanThirtyDaysIsOld()
    {
        var rows = Rows(Report(
            Place("Lave", 0.60, reportedAt: Now.AddDays(-41)),
            Place("Diso", 0.40, reportedAt: Now.AddDays(-29))));

        Assert.True(rows[0].Old);
        Assert.False(rows[1].Old);
    }

    [Fact]
    public void AJournalReadingIsNeverOld()
    {
        var lave = Place("Lave", 0.60, reportedAt: Now.AddDays(-60));
        var standings = SystemStandings.Empty.Apply(Jump("Lave", Now.AddDays(-45), Faction(Us, 0.55)));

        var row = Assert.Single(Rows(Report(lave), standings));

        Assert.True(row.OwnReading);
        Assert.False(row.Old);
    }

    [Fact]
    public void RowsComeMostInfluenceFirst()
    {
        var rows = Rows(Report(Place("Diso", 0.20, controls: false), Place("Lave", 0.60), Place("Leesti", 0.40)));

        Assert.Equal(["Lave", "Leesti", "Diso"], rows.Select(row => row.System));
    }
}
