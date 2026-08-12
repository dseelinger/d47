using D47.Core.Journal;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The names transcription is biased towards. Journal-derived and network-free, which is what
/// lets this be tested with nothing but a handful of journal lines.
/// </summary>
public class ProperNounTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    [Fact]
    public void NothingKnownOffersNoNames()
    {
        Assert.Empty(ProperNouns.From(null));
        Assert.Empty(ProperNouns.From(StateFrom()));
    }

    [Fact]
    public void WhereTheCommanderIsAndWhatTheyFlyAreOffered()
    {
        var names = ProperNouns.From(StateFrom(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Docked","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Loadout","Ship":"Anaconda","ShipName":"Bold Endeavour"}"""));

        // These are exactly the words a general-purpose recogniser has never seen, and where it
        // fails silently — a misheard system name comes back as a plausible English phrase.
        Assert.Contains("Shinrarta Dezhra", names);
        Assert.Contains("Jameson Memorial", names);
        Assert.Contains("Bold Endeavour", names);
        Assert.Contains("Anaconda", names);
    }

    [Fact]
    public void TheRouteAheadIsOfferedSoArrivalsAreNotSurprises()
    {
        var route = new NavRoute
        {
            Hops =
            [
                new RouteHop("Sol", "G"),
                new RouteHop("Wredguia WD-K d8-30", "M"),
                new RouteHop("Praea Euq QI-T d3-3", "N"),
            ],
        };

        var names = ProperNouns.From(
            StateFrom("""{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Sol","JumpDist":1}"""),
            route);

        Assert.Contains("Wredguia WD-K d8-30", names);
        Assert.Contains("Praea Euq QI-T d3-3", names);
    }

    [Fact]
    public void ANameIsOfferedOnceHoweverManyPlacesItAppears()
    {
        var names = ProperNouns.From(StateFrom(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Deciat","JumpDist":1}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"CarrierStats","CarrierID":1,"Callsign":"K7Q-BQL","Name":"Bootstrap"}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"CarrierLocation","CarrierID":1,"Callsign":"K7Q-BQL","StarSystem":"Deciat"}"""));

        // A repeated name spends the bias budget without adding a word.
        Assert.Single(names, name => name == "Deciat");
    }

    [Fact]
    public void TheListIsCappedSoItCannotDisplaceTheModelsOwnContext()
    {
        var many = Enumerable.Range(0, 200)
            .Select(index => new RouteHop($"Synthetic System {index}", "K"))
            .ToArray();

        var names = ProperNouns.From(
            StateFrom("""{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Sol","JumpDist":1}"""),
            new NavRoute { Hops = many });

        // An overlong prompt does not fail loudly — it silently displaces the model's own
        // context and makes transcription worse than no biasing at all.
        Assert.True(names.Count <= ProperNouns.Limit);
    }

    [Fact]
    public void VeryShortNamesAreLeftOut()
    {
        var names = ProperNouns.From(StateFrom(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDJump","StarSystem":"Li","JumpDist":1}"""));

        // Two-letter fragments are already in every recogniser's vocabulary and would spend a
        // slot that a real name needs.
        Assert.DoesNotContain("Li", names);
    }
}
