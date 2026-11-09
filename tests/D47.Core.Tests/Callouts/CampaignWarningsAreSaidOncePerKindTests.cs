using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Tests.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class CampaignWarningsAreSaidOncePerKindTests
{
    private const string Ours = "Lave Revolutionary Party";

    private const string Failed = "{ \"timestamp\":\"2026-11-09T12:00:00Z\", \"event\":\"MissionFailed\", \"MissionID\":9 }";

    private const string Sale = "{ \"timestamp\":\"2026-11-09T12:00:00Z\", \"event\":\"MarketSell\", \"MarketID\":77, \"Type\":\"gold\", \"Count\":10, \"SellPrice\":9000, \"TotalSale\":90000, \"AvgPricePaid\":8000 }";

    private static JournalEvent Line(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string At(string body) => "{ \"timestamp\":\"2026-11-09T12:00:00Z\", " + body + " }";

    private static JournalEvent Jump(params string[] factions) =>
        Line(At("\"event\":\"FSDJump\", \"StarSystem\":\"Lave\", \"SystemAddress\":1, \"StarPos\":[0,0,0], \"Factions\":["
            + string.Join(",", factions.Select(name => $"{{\"Name\":\"{name}\"}}")) + "]"));

    private static JournalEvent Dock(string stationType) =>
        Line(At($"\"event\":\"Docked\", \"StarSystem\":\"Lave\", \"SystemAddress\":1, \"StationName\":\"Lave Station\", \"StationType\":\"{stationType}\", \"MarketID\":77"));

    private static CommanderGameState Where(params JournalEvent[] events) =>
        JournalScript.Commander("F1", "Fixture", events);

    private static CampaignWarningCallout Callout(string? faction = Ours) => new() { Faction = () => faction };

    private static string[] Said(CampaignWarningCallout callout, CommanderGameState? state, bool priming, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(DateTimeOffset.Parse("2026-11-09T12:00:00Z"), priming, state, GameStatus.Unknown, NavRoute.None, events)).Select(a => a.Text)];

    private static JournalEvent Voucher(string faction) =>
        Line(At($"\"event\":\"RedeemVoucher\", \"Type\":\"bounty\", \"Factions\":[{{\"Faction\":\"{faction}\", \"Amount\":5000}}], \"BrokerPercentage\":0"));

    private static JournalEvent Accept(string issuer, string target) =>
        Line(At($"\"event\":\"MissionAccepted\", \"MissionID\":9, \"Name\":\"Mission_Massacre\", \"Faction\":\"{issuer}\", \"TargetFaction\":\"{target}\""));

    [Fact]
    public void ABountyForARivalWhereTheFactionIsPresentIsWarnedOf()
    {
        var state = Where(Jump(Ours, "Lave Pirates"));

        var said = Said(Callout(), state, false, Voucher("Lave Pirates"));

        Assert.Equal(["That bounty voucher raises Lave Pirates here, where Lave Revolutionary Party is present. It works against the campaign."], said);
    }

    [Fact]
    public void ABountyWithNoFactionNamedIsSilent()
    {
        var state = Where(Jump(Ours));

        Assert.Empty(Said(Callout(), state, false, Voucher("")));
    }

    [Fact]
    public void ABountyWhereTheFactionIsAbsentIsSilent()
    {
        var state = Where(Jump("Lave Pirates"));

        Assert.Empty(Said(Callout(), state, false, Voucher("Lave Pirates")));
    }

    [Fact]
    public void ABountyForTheCampaignFactionItselfIsSilent()
    {
        var state = Where(Jump(Ours));

        Assert.Empty(Said(Callout(), state, false, Voucher(Ours)));
    }

    [Fact]
    public void AMissionFailedForTheFactionIsWarnedOf()
    {
        var callout = Callout();
        Said(callout, null, false, Accept(Ours, "Lave Pirates"));

        var said = Said(callout, null, false, Line(Failed));

        Assert.Equal(["Failing that mission costs Lave Revolutionary Party influence."], said);
    }

    [Fact]
    public void AMissionFailedForAnotherFactionIsSilent()
    {
        var callout = Callout();
        Said(callout, null, false, Accept("Lave Pirates", "Somebody"));

        Assert.Empty(Said(callout, null, false, Line(Failed)));
    }

    [Fact]
    public void AMissionAcceptedAgainstTheFactionByAnotherIsWarnedOf()
    {
        var said = Said(Callout(), null, false, Accept("Lave Pirates", Ours));

        Assert.Equal(["That mission, for Lave Pirates, is against Lave Revolutionary Party. Completing it works against the campaign."], said);
    }

    [Fact]
    public void AMissionTheFactionIssuesAgainstItselfIsSilent()
    {
        Assert.Empty(Said(Callout(), null, false, Accept(Ours, Ours)));
    }

    [Fact]
    public void AMurderInTheFactionsJurisdictionIsWarnedOf()
    {
        var state = Where(Jump(Ours));

        var said = Said(Callout(), state, false, Line(At($"\"event\":\"CommitCrime\", \"CrimeType\":\"murder\", \"Faction\":\"{Ours}\", \"Victim\":\"X\", \"Bounty\":1000")));

        Assert.Equal(["A murder in Lave counts against Lave Revolutionary Party."], said);
    }

    [Fact]
    public void AnAssaultIsNotAMurder()
    {
        var state = Where(Jump(Ours));

        Assert.Empty(Said(Callout(), state, false, Line(At($"\"event\":\"CommitCrime\", \"CrimeType\":\"assault\", \"Faction\":\"{Ours}\", \"Bounty\":100"))));
    }

    [Fact]
    public void ASaleToAFleetCarrierWhereTheFactionIsPresentIsWarnedOf()
    {
        var state = Where(Jump(Ours), Dock("FleetCarrier"));

        var said = Said(Callout(), state, false, Line(Sale));

        Assert.Equal(["A sale to a fleet carrier does not count toward the BGS, so it does nothing for Lave Revolutionary Party."], said);
    }

    [Fact]
    public void ASaleToAStationIsSilent()
    {
        var state = Where(Jump(Ours), Dock("Coriolis"));

        Assert.Empty(Said(Callout(), state, false, Line(Sale)));
    }

    [Fact]
    public void EachKindIsSaidOnceAndNotAgain()
    {
        var state = Where(Jump(Ours, "Lave Pirates"));
        var callout = Callout();

        Assert.Single(Said(callout, state, false, Voucher("Lave Pirates")));
        Assert.Empty(Said(callout, state, false, Voucher("Lave Pirates")));
    }

    [Fact]
    public void NothingIsSaidWhilePrimingAndItsMissionsAreStillRemembered()
    {
        var state = Where(Jump(Ours, "Lave Pirates"));
        var callout = Callout();

        Assert.Empty(Said(callout, state, true, Voucher("Lave Pirates"), Accept(Ours, "Lave Pirates")));
        Assert.Single(Said(callout, state, false, Line(Failed)));
        Assert.Single(Said(callout, state, false, Voucher("Lave Pirates")));
    }

    [Fact]
    public void WithNoCampaignFactionNothingIsSaid()
    {
        var state = Where(Jump(Ours, "Lave Pirates"));

        Assert.Empty(Said(Callout(null), state, false, Voucher("Lave Pirates")));
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().CampaignWarnings);

    [Fact]
    public void TheRowTurnsTheCalloutOff()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(Callout());
        engine.SetEnabled("campaign-warnings", false);

        engine.Tick(new CalloutContext(DateTimeOffset.Parse("2026-11-09T12:00:00Z"), false, Where(Jump(Ours, "Lave Pirates")), GameStatus.Unknown, NavRoute.None, [Voucher("Lave Pirates")]));

        Assert.Empty(engine.Drain());
    }
}
