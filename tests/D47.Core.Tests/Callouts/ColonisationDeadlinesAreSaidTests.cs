using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The colonisation deadlines, the squadron rule and the haul size (#580).</summary>
public class ColonisationDeadlinesAreSaidTests
{
    private static readonly DateTimeOffset Claimed = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static string Stamp(DateTimeOffset at) => at.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static JournalEvent Event(DateTimeOffset at, string kind, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?> { ["timestamp"] = Stamp(at), ["event"] = kind };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Claim(DateTimeOffset at) =>
        Event(at, "ColonisationSystemClaim", ("StarSystem", "Wolf 359"), ("SystemAddress", 1234L));

    private static JournalEvent Docked(DateTimeOffset at) =>
        Event(at, "Docked", ("StationName", "Orbital Construction Site: Hub"), ("StarSystem", "Wolf 359"), ("MarketID", 77L));

    private static JournalEvent Depot(DateTimeOffset at, bool complete = false, int provided = 0) =>
        Event(
            at,
            "ColonisationConstructionDepot",
            ("MarketID", 77L),
            ("ConstructionProgress", 0.1),
            ("ConstructionComplete", complete),
            ("ConstructionFailed", false),
            ("ResourcesRequired", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["Name"] = "$steel_name;", ["Name_Localised"] = "Steel", ["RequiredAmount"] = 1000, ["ProvidedAmount"] = provided,
                },
                new Dictionary<string, object?>
                {
                    ["Name"] = "$aluminium_name;", ["Name_Localised"] = "Aluminium", ["RequiredAmount"] = 500, ["ProvidedAmount"] = 0,
                },
            }));

    private static CommanderGameState StateAfter(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Event(Claimed, "Loadout", ("Ship", "python"), ("ShipID", 7), ("CargoCapacity", 256), ("Modules", Array.Empty<object>())));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static List<Announcement> Say(
        ColonisationCallout callout, CommanderGameState state, DateTimeOffset now, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, false, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void AClaimSaysTheDeadlinesAndTheSquadronRuleWhenInNoSquadron()
    {
        var claim = Claim(Claimed);
        var state = StateAfter(claim);

        var said = Assert.Single(Say(new ColonisationCallout(), state, Claimed, claim));

        Assert.Equal(
            "You have 24 hours to deploy the beacon in Wolf 359, and four weeks to finish its primary port. "
            + "A squadron of your own, even of one, extends the exclusive claim window from thirty minutes to a day.",
            said.Text);
    }

    [Fact]
    public void AClaimInASquadronLeavesTheSquadronRuleOut()
    {
        var claim = Claim(Claimed);
        var state = StateAfter(Event(Claimed, "SquadronStartup", ("SquadronName", "Test")), claim);

        var said = Assert.Single(Say(new ColonisationCallout(), state, Claimed, claim));

        Assert.Equal(
            "You have 24 hours to deploy the beacon in Wolf 359, and four weeks to finish its primary port.",
            said.Text);
    }

    [Fact]
    public void TheBeaconReminderComesAtTwentyTwoHoursAndOnlyOnce()
    {
        var callout = new ColonisationCallout();
        var state = StateAfter(Claim(Claimed));

        Assert.Empty(Say(callout, state, Claimed.AddHours(21)));

        var said = Assert.Single(Say(callout, state, Claimed.AddHours(22)));
        Assert.Equal("You have 2 hours left to deploy the beacon in Wolf 359.", said.Text);

        Assert.Empty(Say(callout, state, Claimed.AddHours(22.5)));
    }

    [Fact]
    public void TheBeaconReminderIsNotSaidOnceTheBeaconIsUp()
    {
        var state = StateAfter(Claim(Claimed), Event(Claimed.AddHours(1), "ColonisationBeaconDeployed"));

        Assert.Empty(Say(new ColonisationCallout(), state, Claimed.AddHours(22)));
    }

    [Fact]
    public void ThePrimaryPortRemindersComeAtTwentyOneAndTwentySixDays()
    {
        var callout = new ColonisationCallout();
        var state = StateAfter(Claim(Claimed), Event(Claimed.AddHours(1), "ColonisationBeaconDeployed"));

        Assert.Empty(Say(callout, state, Claimed.AddDays(20)));

        var week = Assert.Single(Say(callout, state, Claimed.AddDays(21)));
        Assert.Equal("The primary port in Wolf 359 has 7 days left to finish.", week.Text);
        Assert.Empty(Say(callout, state, Claimed.AddDays(22)));

        var last = Assert.Single(Say(callout, state, Claimed.AddDays(26)));
        Assert.Equal("The primary port in Wolf 359 has 48 hours left to finish.", last.Text);
        Assert.Empty(Say(callout, state, Claimed.AddDays(26.5)));
    }

    [Fact]
    public void ThePrimaryPortRemindersStopWhenTheFirstSiteIsComplete()
    {
        var state = StateAfter(
            Claim(Claimed),
            Event(Claimed.AddHours(1), "ColonisationBeaconDeployed"),
            Docked(Claimed.AddHours(2)),
            Depot(Claimed.AddHours(2)),
            Depot(Claimed.AddDays(10), complete: true));

        Assert.Empty(Say(new ColonisationCallout(), state, Claimed.AddDays(21)));
        Assert.Empty(Say(new ColonisationCallout(), state, Claimed.AddDays(26)));
    }

    [Fact]
    public void AMissedMomentIsSaidOnceAtTheNextStart()
    {
        var state = StateAfter(Claim(Claimed), Event(Claimed.AddHours(1), "ColonisationBeaconDeployed"));
        var restarted = new ColonisationCallout();

        var said = Assert.Single(Say(restarted, state, Claimed.AddDays(27)));

        Assert.Equal("The primary port in Wolf 359 has 24 hours left to finish.", said.Text);
        Assert.Empty(Say(restarted, state, Claimed.AddDays(27.1)));
    }

    [Fact]
    public void ADeadlineThatHasPassedIsNotSaid()
    {
        var state = StateAfter(Claim(Claimed));

        Assert.Empty(Say(new ColonisationCallout(), state, Claimed.AddDays(29)));
    }

    [Fact]
    public void TheHaulIsTheRemainingTonnesOverTheHoldRoundedUpAndSaidOncePerSite()
    {
        var callout = new ColonisationCallout();
        var depot = Depot(Claimed.AddHours(2), provided: 200);
        var state = StateAfter(Docked(Claimed.AddHours(2)), depot);

        var said = Assert.Single(Say(callout, state, Claimed.AddHours(2), depot));

        // 800 + 500 tonnes over a 256 tonne hold is 5.08 trips.
        Assert.Equal("1300 tonnes to go: about 6 trips in this ship.", said.Text);
        Assert.Empty(Say(callout, state, Claimed.AddHours(3), depot));
    }

    [Fact]
    public void TheNeedsAnswerCarriesTheSameHaulSentence()
    {
        var state = StateAfter(Docked(Claimed), Depot(Claimed, provided: 200));
        var site = state.Colonisation.ById(77)!;

        Assert.Equal(
            "1300 tonnes to go: about 6 trips in this ship",
            ColonisationRules.HaulSentence(site, state.Ship.CargoCapacity));
    }

    [Fact]
    public void ARowSwitchedOffIsNotSaid()
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new ColonisationCallout());
        engine.SetEnabled("colonisation", false, DateTimeOffset.UnixEpoch);

        Assert.False(engine.IsEnabled("colonisation"));
        Assert.Equal("colonisation", new ColonisationCallout().Id);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.ColonisationKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(new CalloutSettings().Colonisation);
        Assert.False(row.Binding!.Write!(D47Settings.Defaults, "false")!.Callouts.Colonisation);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AClaimMadeInAnEarlierSessionComesBackFromTheJournalHistory()
    {
        using var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-10-01T120000.01.log"),
            [
                """{"timestamp":"2026-10-01T12:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                """{"timestamp":"2026-10-01T12:00:01Z","event":"ColonisationSystemClaim","StarSystem":"Wolf 359","SystemAddress":1234}""",
            ]);

        var found = ColonisationBackfill.FromHistory(install.Root, NullLogger.Instance, TestContext.Current.CancellationToken);

        var claim = Assert.Single(found["F1"].Claims);
        Assert.Equal("Wolf 359", claim.StarSystem);
    }
}
