using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

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
        Level = "new",
        Length = "1-year",
        Tone = "Quiet test",
        Core = "archivist",
        Blurb = "A voice in the cockpit knows a song it should not.",
        InYourWords = "I bought a Sidewinder with the last of my credits.",
        Beacon = "The beacon calls.",
    };

    /// <summary>A catalog of the fixture story.</summary>
    public static StoryCatalog Catalog => new([Card], () => [Secret]);

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

    /// <summary>
    /// <see cref="Secret"/> with a cast member, cray, in two versions: Ellie for a Commander who is a man, Ellis for one
    /// who is a woman. The secret and the first clue name cray by token.
    /// </summary>
    public static readonly StorySecret Versioned = Secret with
    {
        Secret = "{name:cray} is the Commander's sister.",
        Clues = [new("{name:cray} hums a song from home.", "cray"), .. Secret.Clues.Skip(1)],
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker
            {
                Id = "cray",
                Who = "A test engineer, brief and warm.",
                Provider = StorySpeaker.Kokoro,
                Versions = new StorySpeakerVersions
                {
                    ForMan = new StorySpeakerVersion { Name = "Ellie", Voice = "af_heart" },
                    ForWoman = new StorySpeakerVersion { Name = "Ellis", Provider = StorySpeaker.Chatterbox, Voice = "ellis-sample" },
                },
            },
        ],
    };

    /// <summary>
    /// A planned chapter's lines answer, named <paramref name="name"/>: words for objectives 1 to <see cref="ChapterPlanner.MostObjectives"/>,
    /// each line spoken by the ship, so it fits any plan; the generator ignores numbers past the plan's.
    /// </summary>
    public static string Lines(string name = "The First Light") =>
        PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives, whole: answer => answer["name"] = name);

    /// <summary>
    /// The career ratings a fixture's Commander has: exploration only, whose drawn activities the fixture galaxy can place, and
    /// Crime at 0. A test that draws other activities sets <see cref="StoryDirector.Ratings"/> itself.
    /// </summary>
    public static readonly D47.Core.Configuration.CareerRatingSettings Explorer = new()
    {
        Combat = 0, Trading = 0, Mining = 0, Exploration = 100, Exobiology = 0, Passengers = 0, SearchAndRescue = 0, Crime = 0,
    };

    /// <summary>
    /// A lines answer whose opening is <paramref name="opening"/>, spoken by <paramref name="openingSpeaker"/> (a plain string with none),
    /// and whose objective N is spoken as <paramref name="lines"/>[N - 1]; a line with no speaker is a plain string. Objectives past
    /// the lines given are spoken by the ship, up to <see cref="ChapterPlanner.MostObjectives"/>.
    /// </summary>
    public static string Written(string opening, string? openingSpeaker, params (string Text, string? Speaker)[] lines) =>
        WrittenInFull(opening, openingSpeaker, [.. lines.Select(line => new[] { line })]);

    /// <summary>As <see cref="Written"/>, with objective N spoken as every line of <paramref name="objectives"/>[N - 1].</summary>
    public static string WrittenInFull(string opening, string? openingSpeaker, params (string Text, string? Speaker)[][] objectives) =>
        PlannedChapterWorld.Lines(
            ChapterPlanner.MostObjectives,
            (number, objective) =>
            {
                if (number <= objectives.Length)
                {
                    objective["lines"] = new System.Text.Json.Nodes.JsonArray([.. objectives[number - 1].Select(line => Line(line.Text, line.Speaker))]);
                }
            },
            answer =>
            {
                answer["name"] = "The First Light";
                answer["opening"] = openingSpeaker is null
                    ? System.Text.Json.Nodes.JsonValue.Create(opening)
                    : new System.Text.Json.Nodes.JsonArray(Line(opening, openingSpeaker));
            });

    /// <summary>A line-repair answer writing objective <paramref name="objective"/> again as one line, and the opening when it is given.</summary>
    public static string Rewritten(int objective, string text, string? speaker, (string Text, string? Speaker)? opening = null)
    {
        var answer = new System.Text.Json.Nodes.JsonObject
        {
            ["objectives"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["objective"] = objective,
                ["title"] = "Part Again",
                ["reason"] = "The broker's ledger points the Commander onward again.",
                ["lines"] = new System.Text.Json.Nodes.JsonArray(Line(text, speaker)),
            }),
        };

        if (opening is { } first)
        {
            answer["opening"] = new System.Text.Json.Nodes.JsonArray(Line(first.Text, first.Speaker));
        }

        return answer.ToJsonString();
    }

    /// <summary>A line-repair answer writing only the opening again, as one line.</summary>
    public static string RewrittenOpening(string text, string? speaker) =>
        new System.Text.Json.Nodes.JsonObject
        {
            ["objectives"] = new System.Text.Json.Nodes.JsonArray(),
            ["opening"] = new System.Text.Json.Nodes.JsonArray(Line(text, speaker)),
        }.ToJsonString();

    private static System.Text.Json.Nodes.JsonNode Line(string text, string? speaker) =>
        speaker is null
            ? System.Text.Json.Nodes.JsonValue.Create(text)
            : new System.Text.Json.Nodes.JsonObject { ["text"] = text, ["speaker"] = speaker };

    /// <summary>The second chapter's lines answer.</summary>
    public static string NextLines => Lines("The Second Light");

    /// <summary>The lines for a third chapter, named apart from the first two.</summary>
    public static string ThirdLines => Lines("The Third Light");

    /// <summary>The fidelity step's answer when no objective states more than its trigger confirms.</summary>
    public const string NothingObjected = """{"objections": []}""";

    private readonly string _folder = Path.Combine(MemoryInstall.FakeRoot, "d47-stories", Guid.NewGuid().ToString("N"));

    public IFileSystem Files { get; } = new MemoryFileSystem();

    /// <summary>With <paramref name="reopening"/>, opens that fixture's files as a restart would, with a new book and director.</summary>
    public StoryFixtures(RoundScriptedLlmProvider provider, StorySecret? secret = null, StoryCard? card = null, Microsoft.Extensions.Logging.ILogger? logger = null, StoryFixtures? reopening = null, IReadOnlyList<D47.Core.Knowledge.StationSummary>? stations = null)
    {
        secret ??= Secret;

        if (reopening is not null)
        {
            _folder = reopening._folder;
            Files = reopening.Files;
        }

        Provider = provider;
        Book = new AdventureBook(
            new AdventureStore(Path.Combine(_folder, "adventures.json"), Files, NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        Stories = StoryStore.Open(Path.Combine(_folder, "story.json"), Files, NullLogger<StoryStore>.Instance);

        Book.Silenced = (commander, id, at) => Stories.Find(commander, id)?.WasOffAt(at) == true;

        Galaxy = new AdventureGeneratorTests.Galaxy { Positioned = true, SystemBodies = [], SystemBiology = [], Extra = stations ?? [] };
        var generator = AdventureGeneratorTests.Generator(provider, Galaxy);

        var catalog = new StoryCatalog([card ?? Card, Other], () => [secret, Secret with { Id = Other.Id }]);

        Director = new StoryDirector(
            Stories,
            Book,
            () => catalog,
            (ask, now, cancellationToken) =>
            {
                Asks.Add(ask);

                if (Throws)
                {
                    throw new HttpRequestException("The provider went away.");
                }

                if (Answers is { } answers)
                {
                    return Written(Task.FromResult(answers(ask)));
                }

                return Written(generator.GenerateAsync(ask, now, cancellationToken));
            },
            () => Here,
            backstory => Backstory = backstory,
            logger ?? NullLogger.Instance)
        {
            Backstory = () => Backstory,
            Archive = StoryChapterArchive.Open(Files, ArchivePath, NullLogger<StoryChapterArchive>.Instance),
            Ratings = () => Explorer,
        };

        Director.WritingAgain += WritingAgain.Add;
    }

    /// <summary>The rounds for a chapter whose every draft fails: the same rounds once for each draft.</summary>
    public static IReadOnlyList<LlmStreamEvent>[] EveryDraft(params string[] rounds) =>
        [.. Enumerable.Range(1, StoryDirector.Drafts).SelectMany(_ => rounds).Select(round => RoundScriptedLlmProvider.Saying(round))];

    /// <summary>What the director said before each draft after the first, oldest first.</summary>
    public List<string> WritingAgain { get; } = [];

    public RoundScriptedLlmProvider Provider { get; }

    /// <summary>The galaxy the generator searches; set <see cref="AdventureGeneratorTests.Galaxy.Positioned"/> for a rewrite to find places to plan.</summary>
    public AdventureGeneratorTests.Galaxy Galaxy { get; }

    public AdventureBook Book { get; }

    public StoryStore Stories { get; }

    public StoryDirector Director { get; }

    public string? Backstory { get; set; }

    /// <summary>Where the Commander is, for the nearest beacon.</summary>
    public StarPosition Here { get; set; } = BeyondTheBeacon(10);

    /// <summary>Every chapter the director asked to have written, oldest first.</summary>
    public List<AdventureAsk> Asks { get; } = [];

    /// <summary>The detail of the last refusal the generator gave, which the Commander is not shown.</summary>
    public string? LastRefusal { get; private set; }

    private async Task<AdventureOutcome> Written(Task<AdventureOutcome> writing)
    {
        var outcome = await writing.ConfigureAwait(false);
        LastRefusal = outcome.Refusal;
        WhileWriting?.Invoke();

        return outcome;
    }

    /// <summary>Run after each write and before the director sees its outcome, as if the game moved on during it.</summary>
    public Action? WhileWriting { get; set; }

    /// <summary>Whether the next write throws rather than reaching the model.</summary>
    public bool Throws { get; set; }

    /// <summary>When set, answers each write in place of the generator.</summary>
    public Func<AdventureAsk, AdventureOutcome>? Answers { get; set; }

    public string StoryPath => Path.Combine(_folder, "story.json");

    public string AdventuresPath => Path.Combine(_folder, "adventures.json");

    public string ArchivePath => Path.Combine(_folder, "story-chapters.jsonl");

    /// <summary>The Guardian cores as the app sees them: held by this Commander's current story.</summary>
    public GuardianCores Cores(string frontierId) => new(() => Stories.Current(frontierId)?.CoreHold ?? CoreHold.None);

    /// <summary>A Commander in a Sidewinder with this jump range, with or without a fuel scoop, a cargo rack (<paramref name="cargo"/> t; null leaves it unreported), a weapon (a pulse laser unless <paramref name="hardpoint"/> names another) and a fleet carrier.</summary>
    public static CommanderGameState Flying(double jumpRange, bool scoop, bool carrier = false, string? hardpoint = "hpt_pulselaser_fixed_small", int? cargo = 4)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test"));
        var modules =
            """[{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size2_class5","On":true}"""
            + (scoop ? """,{"Slot":"Slot01_Size2","Item":"int_fuelscoop_size2_class5","On":true}""" : string.Empty)
            + (hardpoint is null ? string.Empty : $$""",{"Slot":"SmallHardpoint1","Item":"{{hardpoint}}","On":true}""")
            + "]";

        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "MaxJumpRange":{{jumpRange.ToString(System.Globalization.CultureInfo.InvariantCulture)}}{{(cargo is { } capacity ? $", \"CargoCapacity\":{capacity}" : string.Empty)}}, "Modules":{{modules}} }"""));

        if (carrier)
        {
            state.Apply(AdventureFixtures.Event(
                $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"CarrierStats", "CarrierID":1, "Callsign":"X9X-9XX", "Name":"TEST" }"""));
        }

        return state;
    }

    /// <summary>
    /// A Commander in a ship of this type who loaded the game with these credits, after a <c>Statistics</c> event carrying
    /// <paramref name="statistics"/> as its sections, when given.
    /// </summary>
    public static CommanderGameState Loaded(long credits, string ship = "sidewinder", string? statistics = null, string modules = "", int cargo = 0)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test"));

        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"LoadGame", "FID":"F1", "Commander":"Test", "Odyssey":true, "Ship":"{{ship}}", "ShipID":1, "Credits":{{credits}} }"""));
        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Loadout", "Ship":"{{ship}}", "ShipID":1, "MaxJumpRange":30.5, "CargoCapacity":{{cargo}}, "Modules":[{{modules}}] }"""));

        if (statistics is not null)
        {
            state.Apply(AdventureFixtures.Event(
                $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Statistics", {{statistics}} }"""));
        }

        return state;
    }

    /// <summary>A point this many light years from the test beacon, nearer to it than to any other beacon.</summary>
    public static StarPosition BeyondTheBeacon(double lightYears)
    {
        var beacon = GuardianCores.BeaconPositions[BeaconAddress];

        return beacon with { Z = beacon.Z + lightYears };
    }

    public static JournalEvent AncientKey(DateTimeOffset at) =>
        AdventureFixtures.Event($$"""{ "timestamp":"{{AdventureFixtures.Stamp(at)}}", "event":"CollectCargo", "Type":"ancientkey", "Count":1, "Stolen":false }""");

    /// <summary>Jumps to a beacon system and data-links there; returns what the scan woke.</summary>
    public CoreWaking? ScanBeacon(string frontierId, long address, DateTimeOffset at)
    {
        Director.Observe(AdventureFixtures.Jump(address, at), frontierId);

        return Director.Observe(AncientKey(at), frontierId);
    }

    /// <summary>A fixture over a provider that answers each of <paramref name="answers"/> in turn.</summary>
    public static StoryFixtures ScriptedWith(params string[] answers) =>
        new(new RoundScriptedLlmProvider([.. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]));

    /// <summary>Saves a running story picked thirty days before <see cref="Now"/>, its beacon scanned twenty days before, on these chapters.</summary>
    public void SaveRunning(string[] chapters, int cluesGiven = 0, string frontierId = "F1") =>
        Stories.Save(frontierId, new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = chapters,
            CluesGiven = cluesGiven,
        });

    public static readonly AdventureSpine DebtSpine = new() { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed to you.", Ending = "It is forgiven." };

    /// <summary>A generated chapter written at <see cref="Now"/> with the opening "Go.".</summary>
    public static Adventure GeneratedChapter(string key, string name, string? storyId, AdventureSpine? spine, params AdventureBeat[] beats) => new()
    {
        Key = key,
        Name = name,
        Source = AdventureSource.Generated,
        Written = Now,
        Spine = spine,
        Opening = "Go.",
        StoryId = storyId,
        Beats = beats,
    };

    /// <summary>Picks the story for F1 at <paramref name="now"/>, then jumps to the beacon and scans a data point there, stamped as <see cref="BeaconFixture"/> does.</summary>
    public async Task PickAndScanTheBeaconAsync(DateTimeOffset now)
    {
        Assert.Null(await Director.PickAsync("F1", Id, now, CancellationToken.None));

        Director.Observe(BeaconFixture.JumpTo(Beacon, BeaconAddress), "F1");
        Director.Observe(BeaconFixture.AncientKey(), "F1");
    }

    /// <summary>
    /// Flies every beat of a begun chapter so it is done: a place step through its journal event, any other step fired as the bench
    /// fires it. The director sees a beacon scan, as in the app.
    /// </summary>
    public void Finish(string frontierId, string key, DateTimeOffset from)
    {
        var chapter = Book.Store.Find(frontierId, key)!;
        var at = from;

        foreach (var trigger in chapter.Beats.SelectMany(beat => beat.Steps).Select(step => step.Trigger))
        {
            at = at.AddMinutes(1);
            Fire(frontierId, key, trigger, at);
        }
    }

    /// <summary>The journal event that meets <paramref name="trigger"/>, or null for a kind no single event meets.</summary>
    public static JournalEvent? Flown(AdventureTrigger trigger, DateTimeOffset at) => trigger.Kind switch
    {
        TriggerKind.Dock => AdventureFixtures.Docked(trigger.MarketId!.Value, at),
        TriggerKind.Land => AdventureFixtures.Touchdown(trigger.SystemAddress!.Value, trigger.BodyId!.Value, at),
        TriggerKind.Scan => AdventureFixtures.Scan(trigger.SystemAddress!.Value, trigger.BodyId!.Value, at),
        TriggerKind.Arrive or TriggerKind.Beacon => AdventureFixtures.Jump(trigger.SystemAddress!.Value, at),
        _ => null,
    };

    /// <summary>Meets the chapter's current step: through the journal where one event meets it, otherwise as a bench step.</summary>
    public void Fire(string frontierId, string key, AdventureTrigger trigger, DateTimeOffset at)
    {
        if (Flown(trigger, at) is { } flown)
        {
            Book.Observe(flown, frontierId);
        }
        else
        {
            Book.Quiet(frontierId, key);
            Assert.Null(Book.Step(frontierId, key, at));
        }

        if (trigger.Kind == TriggerKind.Beacon)
        {
            Book.Observe(AncientKey(at.AddSeconds(30)), frontierId);
            ScanBeacon(frontierId, trigger.SystemAddress!.Value, at.AddSeconds(30));
        }
    }

    public void Dispose()
    {
    }
}
