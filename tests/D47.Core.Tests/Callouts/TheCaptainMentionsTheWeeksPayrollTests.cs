using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The carrier captain saying the week's payroll has gone out, made out to Elite's name for them.</summary>
public class TheCaptainMentionsTheWeeksPayrollTests
{
    private const string CallSign = "K7Q-B4X";

    private static readonly DateTimeOffset AfterTick = new(2026, 8, 27, 20, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SameWeek = AfterTick.AddDays(2);

    private static readonly DateTimeOffset NextWeek = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, DateTimeOffset at, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Stats(DateTimeOffset at, string type, string? captain) =>
        Event(
            "CarrierStats",
            at,
            ("CarrierType", type),
            ("Callsign", CallSign),
            ("Name", "Sacred Fire"),
            ("CarrierID", 3700000000L),
            ("Crew", captain is null
                ? Array.Empty<object>()
                : new object[] { new Dictionary<string, object> { ["CrewRole"] = "Captain", ["CrewName"] = captain, ["Activated"] = true, ["Enabled"] = true } }));

    private static JournalEvent DockAt(DateTimeOffset at, string station = CallSign, long market = 3700000000L) =>
        Event("Docked", at, ("StationName", station), ("StationType", "FleetCarrier"), ("MarketID", market));

    private static CommanderGameState State(JournalEvent after, string type = "FleetCarrier", string? captain = "Jane Doe")
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Stats(AfterTick.AddHours(-1), type, captain));
        state.Apply(after);
        return state;
    }

    private static List<Announcement> Say(
        CarrierPayrollCallout callout, JournalEvent journalEvent, bool priming = false, string type = "FleetCarrier", string? captain = "Jane Doe") =>
        [.. callout.Examine(new CalloutContext(
            journalEvent.Timestamp, priming, State(journalEvent, type, captain), GameStatus.Unknown, NavRoute.None, [journalEvent]))];

    private static string Docking(CarrierPayrollCallout callout, DateTimeOffset? at = null)
    {
        var said = Assert.Single(Say(callout, DockAt(at ?? SameWeek)));

        Assert.Equal(CarrierPayrollCallout.Key, said.Key);
        Assert.Equal(VoiceRole.CarrierCaptain, said.Voice);
        return said.Text;
    }

    [Fact]
    public void AnUnnamedCaptainIsPaidUnderTheEliteName()
    {
        Assert.Equal("Payroll's gone through, Commander Fixture. Made out to me, Jane Doe.", Docking(new CarrierPayrollCallout()));
    }

    [Theory]
    [InlineData("Doe")]
    [InlineData("  jane ")]
    [InlineData("JANE DOE")]
    public void ASettingMatchingEliteIsPaidAsItShouldBe(string setting)
    {
        var text = Docking(new CarrierPayrollCallout { CaptainName = () => setting });

        Assert.Equal("Payroll's gone through, Commander Fixture, made out to me, as it should be.", text);
    }

    [Fact]
    public void ADifferentSettingMakesTheEliteNameANomDePlume()
    {
        var text = Docking(new CarrierPayrollCallout { CaptainName = () => "Reyes" });

        Assert.Equal("Payroll's gone through. The cheque says Jane Doe, of course. My nom de plume.", text);
    }

    [Fact]
    public void ASecondDockingInTheSameTickWeekIsSilent()
    {
        var callout = new CarrierPayrollCallout();
        Docking(callout);

        Assert.Empty(Say(callout, DockAt(SameWeek.AddHours(3))));
    }

    [Fact]
    public void ADockingAfterTheNextTickIsSpokenAgain()
    {
        var callout = new CarrierPayrollCallout();
        Docking(callout);

        Docking(callout, NextWeek);
    }

    [Fact]
    public void ARestartInTheSameWeekIsSilent()
    {
        string? remembered = null;
        Docking(new CarrierPayrollCallout { LastSaidWeek = () => remembered, RememberSaidWeek = week => remembered = week });

        var restarted = new CarrierPayrollCallout { LastSaidWeek = () => remembered };

        Assert.Empty(Say(restarted, DockAt(SameWeek.AddDays(1))));
    }

    [Fact]
    public void ATickWeekRunsFromThursdaySevenUtc()
    {
        var callout = new CarrierPayrollCallout();
        Docking(callout, new DateTimeOffset(2026, 9, 3, 6, 59, 0, TimeSpan.Zero));

        Docking(callout, new DateTimeOffset(2026, 9, 3, 7, 1, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AJumpRequestIsSilent()
    {
        var request = Event("CarrierJumpRequest", SameWeek, ("CarrierID", 3700000000L), ("SystemName", "Sol"));

        Assert.Empty(Say(new CarrierPayrollCallout(), request));
    }

    [Fact]
    public void ADockingElsewhereIsSilent()
    {
        Assert.Empty(Say(new CarrierPayrollCallout(), DockAt(SameWeek, "OTH-ER1", 3700000001L)));
    }

    [Fact]
    public void PrimingIsSilent()
    {
        Assert.Empty(Say(new CarrierPayrollCallout(), DockAt(SameWeek), priming: true));
    }

    [Fact]
    public void ASquadronCarrierIsSilent()
    {
        Assert.Empty(Say(new CarrierPayrollCallout(), DockAt(SameWeek), type: "SquadronCarrier"));
    }

    [Fact]
    public void NoCaptainOnFileIsSilent()
    {
        Assert.Empty(Say(new CarrierPayrollCallout(), DockAt(SameWeek), captain: null));
    }

    [Fact]
    public void ThePayrollComesAfterTheTowerAndTheCaptainsWelcome()
    {
        var dock = DockAt(SameWeek);
        var context = new CalloutContext(SameWeek, false, State(dock), GameStatus.Unknown, NavRoute.None, [dock]);

        var keys = new CarrierCallout().Examine(context)
            .Concat(new CarrierPayrollCallout().Examine(context))
            .Select(said => said.Key)
            .ToList();

        Assert.True(keys.IndexOf(CarrierCallout.SecuredKey) < keys.IndexOf(CarrierCallout.HomeKey));
        Assert.True(keys.IndexOf(CarrierCallout.HomeKey) < keys.IndexOf(CarrierPayrollCallout.Key));
    }

    [Theory]
    [InlineData(null, "has given you no other")]
    [InlineData("Doe", "which is what the Commander calls you")]
    [InlineData("Reyes", "the Commander calls you Reyes")]
    public void TheCaptainsBriefNamesTheEliteNameAndTheCase(string? setting, string expected)
    {
        var brief = CaptainLine.Brief(State(DockAt(SameWeek)).Carrier, setting);

        Assert.Contains("Jane Doe", brief);
        Assert.Contains(expected, brief);
    }

    [Fact]
    public void TheCaptainsBriefNamesNobodyWithoutACaptainOnFile()
    {
        var brief = CaptainLine.Brief(State(DockAt(SameWeek), captain: null).Carrier, "Reyes");

        Assert.DoesNotContain("crew list", brief);
    }

    [Fact]
    public void TheRewordKeepsTheEliteNameAndTheCase()
    {
        var said = new Announcement(CarrierPayrollCallout.Key, "Payroll's gone through.") { Voice = VoiceRole.CarrierCaptain };
        var brief = FlavourBriefs.For(said, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("nom de plume", brief.Instruction);
    }
}
