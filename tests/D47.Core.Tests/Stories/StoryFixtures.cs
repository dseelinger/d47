using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Stories;

/// <summary>An invented story, its hidden layer, and a director over a book in a temporary folder.</summary>
internal sealed class StoryFixtures : IDisposable
{
    public const string Id = "the-test-story";

    public const string Beacon = "IC 2391 Sector MX-T b3-6";

    public const long BeaconAddress = 13872878396833;

    public const long SecondBeaconAddress = 4208161886922;

    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static readonly StoryCard Card = new()
    {
        Id = Id,
        Number = 1,
        Title = "The Test Story",
        Genre = "Whydunit",
        Tone = "Quiet test",
        Core = "archivist",
        Blurb = "A voice in the cockpit knows a song it should not.",
        InYourWords = "I bought a Sidewinder with the last of my credits.",
        Beacon = "The beacon calls.",
    };

    public static readonly StoryCard Other = Card with { Id = "the-other-story", Number = 2, Title = "The Other Story", Core = "kex", InYourWords = "I was somebody else." };

    /// <summary>A hidden layer that keeps every rule of the format.</summary>
    public static readonly StorySecret Secret = new()
    {
        Id = Id,
        Secret = "The ship's voice is the Commander's sister.",
        End = "Keep her or let her go.",
        Beats = new StoryBeats
        {
            OpeningImage = "A ship that hums.",
            ThemeStated = "Who is the voice?",
            SetUp = "A new pilot.",
            Catalyst = "The song.",
            Debate = "Ignore it?",
            BreakIntoTwo = "Ask the voice.",
            BStory = "A dock worker.",
            FunAndGames = "Old ports.",
            Midpoint = "The address.",
            BadGuysCloseIn = "A buyer for the ship.",
            AllIsLost = "The voice goes quiet.",
            DarkNightOfTheSoul = "Alone in the black.",
            BreakIntoThree = "The beacon.",
            Finale = "The name.",
            FinalImage = "Two voices hum.",
        },
        Clues =
        [
            new("She hums a song from home.", StorySpeaker.Ship),
            new("She knows the old address.", StorySpeaker.Narrator),
            new("She says the name.", "dock-hand"),
            .. Enumerable.Range(4, 11).Select(at => new StoryLine($"Clue {at}.", StorySpeaker.Ship)),
        ],
        Finale = [.. Enumerable.Range(1, 4).Select(at => new StoryLine($"Finale {at}.", StorySpeaker.Narrator))],
        Options = [new StoryOption { Id = "keep", Label = "Keep her", After = "She stays.", Add = ["warden"] }],
        Cast = [new StorySpeaker { Id = "dock-hand", Name = "Ren", Who = "A tired dock hand, short words.", Provider = StorySpeaker.Kokoro, Voice = "bm_george" }],
    };

    public const string Spine = """
        {"name": "The First Light", "premise": "A stock voice asks for something.", "want": "To see a beacon.",
         "stake": "Whether a tool can want.", "turn": "It asked before.", "ending": "It is answered."}
        """;

    public const string BeatsToTheBeacon = """
        {"opening": "Factory settings, holding.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    /// <summary>Chapter one with a beacon beat before its last, which also ends at the beacon.</summary>
    public const string BeatsWithAnEarlyBeacon = """
        {"opening": "Factory settings, holding.", "reply": "Here it is.", "beats": [
          {"title": "Too Soon", "function": "setup", "kind": "beacon", "line": "Not yet."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "line": "Scan it."}
        ]}
        """;

    /// <summary>A beacon written by the model in the old form, as an arrival.</summary>
    public const string BeatsArrivingAtTheBeacon = """
        {"opening": "Factory settings, holding.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Beacon", "function": "resolution", "kind": "arrive", "system": "IC 2391 Sector MX-T b3-6", "line": "Scan it."}
        ]}
        """;

    public const string BeatsElsewhere = """
        {"opening": "Factory settings, holding.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "resolution", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."}
        ]}
        """;

    public const string NextSpine = """
        {"name": "The Second Light", "premise": "The voice asks again.", "want": "To answer.",
         "stake": "Whether it was a question.", "turn": "It was a name.", "ending": "It is kept."}
        """;

    public const string NextBeatsWithABeacon = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern Again", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Beacon Again", "function": "resolution", "kind": "beacon", "line": "Again."}
        ]}
        """;

    public const string NextBeats = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern Again", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Anchorage Again", "function": "resolution", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Home."}
        ]}
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-stories", Guid.NewGuid().ToString("N"));

    public StoryFixtures(RoundScriptedLlmProvider provider)
    {
        Directory.CreateDirectory(_folder);

        Provider = provider;
        Book = new AdventureBook(
            new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        Stories = StoryStore.Open(Path.Combine(_folder, "story.json"), NullLogger<StoryStore>.Instance);

        Book.Silenced = (commander, id, at) => Stories.Find(commander, id)?.WasOffAt(at) == true;

        var generator = AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy());

        Director = new StoryDirector(
            Stories,
            Book,
            new StoryCatalog([Card, Other], () => [Secret, Secret with { Id = Other.Id }]),
            (ask, now, cancellationToken) => Throws
                ? throw new HttpRequestException("The provider went away.")
                : generator.GenerateAsync(ask, now, cancellationToken),
            () => StarPosition.Origin,
            backstory => Backstory = backstory,
            NullLogger.Instance);
    }

    public RoundScriptedLlmProvider Provider { get; }

    public AdventureBook Book { get; }

    public StoryStore Stories { get; }

    public StoryDirector Director { get; }

    public string? Backstory { get; private set; }

    /// <summary>Whether the next write throws rather than reaching the model.</summary>
    public bool Throws { get; set; }

    public string StoryPath => Path.Combine(_folder, "story.json");

    /// <summary>The Guardian cores as the app sees them: held by this Commander's current story.</summary>
    public GuardianCores Cores(string frontierId) => new(() => Stories.Current(frontierId)?.CoreHold ?? CoreHold.None);

    public static JournalEvent DataScanned(DateTimeOffset at) =>
        AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(at)}}", "event":"DataScanned", "Type":"$Datascan_AncientBeacon;" }""");

    /// <summary>Jumps to a beacon system and data-links there; returns what the scan woke.</summary>
    public CoreWaking? ScanBeacon(string frontierId, long address, DateTimeOffset at)
    {
        Director.Observe(AdventureFixtures.Jump(address, at), frontierId);

        return Director.Observe(DataScanned(at), frontierId);
    }

    /// <summary>Flies every beat of a begun chapter so it is done.</summary>
    public void Finish(string frontierId, string key, DateTimeOffset from)
    {
        var chapter = Book.Store.Find(frontierId, key)!;
        var at = from;

        foreach (var beat in chapter.Beats)
        {
            at = at.AddMinutes(1);

            Book.Observe(
                beat.Trigger.Kind == TriggerKind.Dock
                    ? AdventureFixtures.Docked(beat.Trigger.MarketId!.Value, at)
                    : AdventureFixtures.Jump(beat.Trigger.SystemAddress!.Value, at),
                frontierId);

            if (beat.Trigger.Kind == TriggerKind.Beacon)
            {
                Book.Observe(DataScanned(at.AddSeconds(30)), frontierId);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
