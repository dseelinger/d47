using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// A flavour line that contradicts what the ship can prove about itself is asked for once more and then
/// not said at all.
/// </summary>
public class AFlavourLineThatContradictsTheShipIsNotSpokenTests
{
    /// <summary>Elite's manifest read, the ship's own, and nothing in it.</summary>
    private static readonly ShipFacts EmptyHold = new() { HoldKnown = true, HoldTonnes = 0 };

    /// <summary>Read, and a hundred tonnes aboard a hold that takes four hundred.</summary>
    private static readonly ShipFacts LoadedHold =
        new() { HoldKnown = true, HoldTonnes = 100, HoldCapacity = 400 };

    [Fact]
    public void AHoldSaidToBeFullWhileItIsEmptyIsAContradiction()
    {
        var found = ContradictedClaims.Find("The hold is full and the route is ours.", EmptyHold);

        Assert.NotNull(found);
        Assert.Equal("cargo aboard", found.Claim);
        Assert.Equal("the hold holds nothing", found.State);
        Assert.Contains("empty", found.Correction, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>No state, no contradiction.</summary>
    [Theory]
    [InlineData("The hold is full and the route is ours.")]
    [InlineData("Nothing in the hold, Commander.")]
    [InlineData("Four hundred tonnes of tritium, and every one of them yours.")]
    public void ALineAboutTheHoldPassesWhileTheHoldIsUnknown(string line)
    {
        Assert.Null(ContradictedClaims.Find(line, ShipFacts.Unknown));
        Assert.Null(ContradictedClaims.Find(line, new ShipFacts { HoldKnown = false, HoldTonnes = 0 }));
    }

    /// <summary>Each claim's words, against the state that disproves them.</summary>
    [Theory]
    [InlineData("cargo aboard", "The hold is full.")]
    [InlineData("cargo aboard", "Forty tonnes of tritium and a long way to carry it.")]
    [InlineData("cargo aboard", "Our cargo is worth more than the ship.")]
    [InlineData("cargo aboard", "We're fully loaded, Commander.")]
    public void AnEmptyHoldDisprovesEveryClaimThatThereIsCargo(string claim, string line)
    {
        Assert.Equal(claim, ContradictedClaims.Find(line, EmptyHold)?.Claim);
    }

    [Theory]
    [InlineData("The hold is empty, for once.")]
    [InlineData("Nothing in the hold and nowhere to be.")]
    [InlineData("No cargo, no cares.")]
    [InlineData("We're running empty.")]
    public void ALoadedHoldDisprovesEveryClaimThatItIsEmpty(string line)
    {
        var found = ContradictedClaims.Find(line, LoadedHold);

        Assert.Equal("hold empty", found?.Claim);
        Assert.Equal("the hold holds 100 t", found?.State);
    }

    /// <summary>
    /// The partial case the empty-hold claim cannot reach: a hold with three hundred tonnes of room in
    /// it, called full.
    /// </summary>
    [Fact]
    public void AHoldWithRoomInItIsNotFull()
    {
        var found = ContradictedClaims.Find("Hold's full. Time to sell.", LoadedHold);

        Assert.Equal("hold full", found?.Claim);
        Assert.Equal("the hold holds 100 of 400 t", found?.State);

        // And at capacity it is simply true, whatever the wording.
        Assert.Null(ContradictedClaims.Find(
            "Hold's full. Time to sell.",
            LoadedHold with { HoldTonnes = 400 }));
    }

    [Theory]
    [InlineData("docked", "We're docked, and the deck is quiet.")]
    [InlineData("docked", "Sitting on the pad with nothing to do.")]
    public void FlyingDisprovesAClaimToBeDocked(string claim, string line)
    {
        var flying = new ShipFacts { Mode = FlightMode.Supercruise, Docked = false };

        Assert.Equal(claim, ContradictedClaims.Find(line, flying)?.Claim);

        // And says nothing at all where the journal has not placed the ship.
        Assert.Null(ContradictedClaims.Find(line, ShipFacts.Unknown));
    }

    /// <summary>The words are narrow, and this is why.</summary>
    [Fact]
    public void AGrantedDockingRequestIsNotAClaimToBeDocked()
    {
        var flying = new ShipFacts { Mode = FlightMode.Normal, Docked = false };

        Assert.Null(ContradictedClaims.Find("Docking request granted, landing pad 04.", flying));
    }

    [Fact]
    public void SupercruiseDisprovesAClaimToBeLanded()
    {
        var cruising = new ShipFacts { Mode = FlightMode.Supercruise };

        Assert.Equal("landed", ContradictedClaims.Find("Now we're on the surface.", cruising)?.Claim);

        // Not from a pad, because a planetary port is on the ground and saying so there is true.
        Assert.Null(ContradictedClaims.Find(
            "Now we're on the surface.",
            new ShipFacts { Mode = FlightMode.Docked, Docked = true }));
    }

    [Fact]
    public void ADockedShipDisprovesAClaimToBeInSupercruise()
    {
        var docked = new ShipFacts { Mode = FlightMode.Docked, Docked = true };

        Assert.Equal("supercruise", ContradictedClaims.Find("Quiet in supercruise tonight.", docked)?.Claim);

        // Normal space is left out: a line about supercruise said there is usually about entering it.
        Assert.Null(ContradictedClaims.Find(
            "Quiet in supercruise tonight.",
            new ShipFacts { Mode = FlightMode.Normal }));
    }

    /// <summary>
    /// The two fitted-module claims from #323 and #324, and the null that means "no evidence" rather
    /// than "not fitted" — the same distinction <see cref="ShipLoadout.Fitted"/> makes.
    /// </summary>
    [Fact]
    public void AMissingScoopDisprovesAClaimToBeScooping()
    {
        Assert.Equal(
            "scoop fitted",
            ContradictedClaims.Find("We'll top up on the fuel scoop.", new ShipFacts { ScoopFitted = false })?.Claim);

        Assert.Null(ContradictedClaims.Find(
            "We'll top up on the fuel scoop.", new ShipFacts { ScoopFitted = true }));

        Assert.Null(ContradictedClaims.Find(
            "We'll top up on the fuel scoop.", new ShipFacts { ScoopFitted = null }));
    }

    [Fact]
    public void AMissingLimpetControllerDisprovesAClaimAboutLimpets()
    {
        Assert.Equal(
            "limpet controller fitted",
            ContradictedClaims.Find(
                "Send a limpet out for it.", new ShipFacts { LimpetControllerFitted = false })?.Claim);

        Assert.Null(ContradictedClaims.Find(
            "Send a limpet out for it.", new ShipFacts { LimpetControllerFitted = null }));
    }

    /// <summary>"tonnes of"/"tons of" is read with its object rather than treated as proof of cargo on its own, and a stated figure is checked against <see cref="ShipFacts.HoldCapacity"/> as its own kind of error.</summary>
    [Fact]
    public void ATonnageNamingAnEmptyHoldAtItsOwnCapacityIsSpoken()
    {
        var atCapacity = new ShipFacts { HoldKnown = true, HoldTonnes = 0, HoldCapacity = 1200 };

        Assert.Null(ContradictedClaims.Find(
            "1200 tonnes of empty cargo hold, crossing a system for free.", atCapacity));
    }

    [Fact]
    public void ATonnageThatExceedsTheHoldsCapacityIsItsOwnContradiction()
    {
        var smallerHold = new ShipFacts { HoldKnown = true, HoldTonnes = 0, HoldCapacity = 400 };

        var found = ContradictedClaims.Find(
            "1200 tonnes of empty cargo hold, crossing a system for free.", smallerHold);

        Assert.Equal("hold capacity", found?.Claim);
        Assert.Contains("400", found?.State, StringComparison.Ordinal);
        Assert.Contains("400", found?.Correction, StringComparison.Ordinal);
    }

    /// <summary>A figure at or under a known capacity is not this claim's business.</summary>
    [Fact]
    public void ATonnageThatFitsTheHoldsCapacityPasses()
    {
        var facts = new ShipFacts { HoldKnown = true, HoldTonnes = 800, HoldCapacity = 1200 };

        Assert.Null(ContradictedClaims.Find("800 tonnes of tritium heading out to Dromi.", facts));
    }

    /// <summary>
    /// Naming a commodity after the tonnage is still caught — the object is not an emptiness word.
    /// </summary>
    [Fact]
    public void ATonnageOfANamedCommodityIsStillACargoClaim()
    {
        var found = ContradictedClaims.Find("Forty tonnes of tritium and a long way to carry it.", EmptyHold);

        Assert.Equal("cargo aboard", found?.Claim);
    }

    /// <summary>"empty cargo hold" now joins the "hold empty" vocabulary, so cargo aboard disproves it.</summary>
    [Fact]
    public void AnEmptyCargoHoldPhraseIsDisprovedByCargoAboard()
    {
        var loaded = new ShipFacts { HoldKnown = true, HoldTonnes = 80 };

        var found = ContradictedClaims.Find("Empty cargo hold, nothing to declare.", loaded);

        Assert.Equal("hold empty", found?.Claim);
    }

    /// <summary>
    /// A line matching both the "cargo aboard" and "hold empty" vocabularies is judged by the hold's
    /// actual state, not by whichever claim the table reaches first.
    /// </summary>
    [Fact]
    public void ALineThatMatchesBothCargoVocabulariesIsJudgedByTheActualHold()
    {
        Assert.Null(ContradictedClaims.Find("Our cargo is gone, empty hold ahead of us.", EmptyHold));

        var found = ContradictedClaims.Find("Our cargo is gone, empty hold ahead of us.", LoadedHold);

        Assert.Equal("hold empty", found?.Claim);
        Assert.Equal("the hold holds 100 t", found?.State);
    }

    /// <summary>Settling the two cargo claims between themselves settles those two and nothing else.</summary>
    [Fact]
    public void ALineThatMatchesBothCargoVocabulariesIsStillHeldToTheOtherClaims()
    {
        var noScoop = new ShipFacts { HoldKnown = true, HoldTonnes = 0, ScoopFitted = false };

        var found = ContradictedClaims.Find(
            "Running empty, and our cargo racks are cold. Time to go scooping.", noScoop);

        Assert.Equal("scoop fitted", found?.Claim);
    }

    /// <summary>
 /// The six warnings logged against the installed build on 2026-09-06, each replayed against
    /// the hold state <c>data\logs\d47-20260906.jsonl</c> and that session's journal recorded for it —
    /// an empty 1200 t hold throughout.
    /// </summary>
    public static IEnumerable<object[]> TheSixLoggedLines()
    {
        yield return ["1200 tonnes of empty cargo hold, crossing a system for free. Unrecoverable margin, every second of it."];
        yield return ["1200 tonnes of empty hold, burning fuel for nothing. Unrecoverable margin, every parsec of it."];
        yield return ["1200 tons of empty cargo hold, crossing a system for free. Unrecoverable margin, every second of it."];
        yield return ["Zero cargo, twelve hundred tons of capacity idle. That's a margin I don't enjoy carrying."];
        yield return ["[flatly] 1200 tons of cargo capacity, zero drawn down. Unrecoverable margin, every hour this hold sits empty."];
        yield return ["1,812,816,000 credits earned this cycle, and 1200 tons of empty hold behind it. Unrecoverable margin, that - capacity idle is capacity wasted."];
    }

    [Theory]
    [MemberData(nameof(TheSixLoggedLines))]
    public void TheSixLoggedLinesAgreeWithTheShipTheyWereLoggedAgainst(string line)
    {
        var facts = new ShipFacts { HoldKnown = true, HoldTonnes = 0, HoldCapacity = 1200 };

        Assert.Null(ContradictedClaims.Find(line, facts));
    }

    [Fact]
    public async Task TheRetryIsAskedWithTheContradictionNamed()
    {
        var asked = new List<string>();

        var said = await ContradictedClaims.SayableAsync(
            "The hold is full.",
            EmptyHold,
            contradiction =>
            {
                asked.Add(contradiction.Correction);
                return Task.FromResult<string?>("Quiet out here tonight.");
            },
            NullLogger.Instance,
            "ambient.Supercruise");

        Assert.Equal("Quiet out here tonight.", said);
        Assert.Contains("empty", Assert.Single(asked), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Exactly one retry, never a loop.</summary>
    [Fact]
    public async Task ARetryThatStillContradictsIsNotSpokenAndIsAskedForOnlyOnce()
    {
        var asks = 0;
        var logger = new RecordingLogger();

        var said = await ContradictedClaims.SayableAsync(
            "The hold is full.",
            EmptyHold,
            _ =>
            {
                asks++;
                return Task.FromResult<string?>("Still a full hold, Commander.");
            },
            logger,
            "ambient.Supercruise");

        Assert.Null(said);
        Assert.Equal(1, asks);

        // Both catches are on the record, with the line, the claim and the state that settled it — the
        // vocabulary is meant to be grown from these, and a miss leaves no trace anywhere.
        var warnings = logger.Entries.Where(entry => entry.Level == LogLevel.Warning).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Contains("cargo aboard", warning.Message, StringComparison.Ordinal));
        Assert.All(warnings, warning => Assert.Contains("the hold holds nothing", warning.Message, StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Message.Contains("Still a full hold", StringComparison.Ordinal));
    }

    /// <summary>A line with nothing to disprove is never asked about a second time.</summary>
    [Fact]
    public async Task ALineThatContradictsNothingIsSpokenWithoutASecondCall()
    {
        var said = await ContradictedClaims.SayableAsync(
            "Quiet out here tonight.",
            EmptyHold,
            _ => throw new InvalidOperationException("A clean line must not be asked for again."),
            NullLogger.Instance,
            "ambient.Supercruise");

        Assert.Equal("Quiet out here tonight.", said);
    }

    /// <summary>The authored fallback is checked too.</summary>
    [Fact]
    public void AnAuthoredLineThatContradictsTheShipIsNotSpokenEither()
    {
        var logger = new RecordingLogger();

        Assert.Null(ContradictedClaims.Sayable(
            "The hold is full and the route is ours.", EmptyHold, logger, "ambient.Supercruise"));

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                     && entry.Message.Contains("the authored line", StringComparison.Ordinal));

        Assert.Equal(
            "There is a queue on the pads outside. There always is.",
            ContradictedClaims.Sayable(
                "There is a queue on the pads outside. There always is.",
                EmptyHold,
                logger,
                "ambient.Docked"));
    }

    /// <summary>The authored line is checked for the contradiction and for nothing else.</summary>
    [Fact]
    public void AnAuthoredLineIsNotJudgedByTheRefusalDenylist()
    {
        const string beat = "She had broken those rules before, and would again.";

        Assert.False(FlavourBriefs.MayBeSpoken(beat));

        Assert.Equal(
            beat,
            ContradictedClaims.Sayable(beat, ShipFacts.Unknown, NullLogger.Instance, "adventure.beat"));

        // Nothing to say is still nothing to say.
        Assert.Null(ContradictedClaims.Sayable("   ", ShipFacts.Unknown, NullLogger.Instance, "ambient.Docked"));
    }

    /// <summary>
    /// A refusal from the model is still a refusal: the two guards compose on the path they are both
 /// meant for, and the one that already existed runs first.
    /// </summary>
    [Fact]
    public async Task AComposedLineThatMayNotBeSpokenAtAllIsStillNotSpoken()
    {
        var said = await ContradictedClaims.SayableAsync(
            "I don't have that capability.",
            ShipFacts.Unknown,
            _ => throw new InvalidOperationException("A refusal is not a contradiction to ask about."),
            NullLogger.Instance,
            "ambient.Docked");

        Assert.Null(said);
    }

    /// <summary>
    /// An exchange is mostly not about the Commander's ship, which is what the issue scoped chatter to.
    /// </summary>
    [Theory]
    [InlineData("Forty tonnes of tritium and nowhere to sell it.", false)]
    [InlineData("We're docked at pad nine until the shift ends.", false)]
    [InlineData("Nice load you're hauling there.", true)]
    [InlineData("Your hold is full, by the look of that ship.", true)]
    [InlineData("Fly safe, Commander.", true)]
    public void ChatterIsCheckedOnlyWhereItAddressesTheCommander(string line, bool theirs) =>
        Assert.Equal(theirs, ContradictedClaims.AboutTheCommandersShip(line));

    /// <summary>Whole words, so a young courier is not the Commander.</summary>
    [Theory]
    [InlineData("The young one on the far pad again.")]
    [InlineData("Yourselves aside, nobody was listening.")]
    public void AWordThatMerelyContainsOneOfThemIsNotAnAddress(string line) =>
        Assert.False(ContradictedClaims.AboutTheCommandersShip(line));

    /// <summary>The one-line question that replaces a single beat of an invented exchange.</summary>
    [Fact]
    public void OneLineOfAnExchangeIsAskedForAgainOnItsOwn()
    {
        var contradiction = ContradictedClaims.Find("The hold is full.", EmptyHold);

        Assert.NotNull(contradiction);

        var rewrite = ContradictedClaims.Rewrite("The hold is full.", contradiction);

        Assert.Contains("The hold is full.", rewrite, StringComparison.Ordinal);
        Assert.Contains(contradiction.Correction, rewrite, StringComparison.Ordinal);
        Assert.Contains("One line only", rewrite, StringComparison.Ordinal);
    }

    /// <summary>
    /// Read from <see cref="CommanderGameState"/> rather than from a formatted string, so the replay
    /// harness can drive a claim directly.
    /// </summary>
    [Fact]
    public void NoCommanderIsNoFacts()
    {
        var facts = ShipFacts.Of(null);

        Assert.False(facts.HoldKnown);
        Assert.Equal(FlightMode.Unknown, facts.Mode);
        Assert.Null(facts.ScoopFitted);
        Assert.Null(facts.LimpetControllerFitted);
    }

    /// <summary>
    /// Where the journal has placed the Commander and what their loadout says, folded the way the rest
    /// of Core folds them.
    /// </summary>
    [Fact]
    public void TheSnapshotReadsTheJournalAndTheFlownLoadout()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Event(
            "Loadout",
            ("Ship", "python"),
            ("ShipID", 7),
            ("CargoCapacity", 256)));

        state.Apply(Event("Docked", ("StationName", "Jameson Memorial"), ("StarSystem", "Shinrarta Dezhra")));

        var docked = ShipFacts.Of(state);

        Assert.True(docked.Docked);
        Assert.Equal(FlightMode.Docked, docked.Mode);
        Assert.Equal(256, docked.HoldCapacity);

        // A loadout with no module list says nothing about what is fitted, which is not the same answer as
        // "nothing is fitted" — so neither module claim can fire.
        Assert.Null(docked.ScoopFitted);
        Assert.Null(docked.LimpetControllerFitted);

        // Undocked first, because that is the event that clears the flag; SupercruiseEntry says where they
        // went and nothing about the pad they left.
        state.Apply(Event("Undocked", ("StationName", "Jameson Memorial")));
        state.Apply(Event("SupercruiseEntry", ("StarSystem", "Shinrarta Dezhra")));

        var cruising = ShipFacts.Of(state);

        Assert.False(cruising.Docked);
        Assert.Equal(FlightMode.Supercruise, cruising.Mode);
    }

    private static JournalEvent Event(string kind, params (string Key, object Value)[] fields)
    {
        var payload = new Dictionary<string, object>
        {
            ["timestamp"] = "2026-09-06T12:00:00Z",
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
