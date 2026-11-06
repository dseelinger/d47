using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A Commander docked in Fixture Prime at x = 100 with a courier mission to Fixture Second, and a galaxy around them, for
/// writing a planned chapter through the generator: the plan is drawn from the places gathered, and the model writes its words.
/// </summary>
internal static class PlannedChapterWorld
{
    public static readonly DrawnActivity Bounty = new(TriggerKind.Bounty, null, 5);

    public static readonly DrawnActivity Codex = new(TriggerKind.Codex, null, 4);

    public static readonly DrawnActivity Salvage = new(TriggerKind.Salvage, null, 3);

    public static readonly AdventureStory Story = new(
        "fixture-story", "Fixture Story", "Public", "Hidden", Chapter: 3, DaysRunning: 4, DaysSinceBeacon: 2, Stage: "Fun and Games",
        Slots: [Bounty, Codex, Salvage],
        Cast: [new AdventureSpeaker("mara", "Mara Venn", "A salvage broker.")]);

    public static AdventureAsk Ask(AdventureStory? story = null) => new(AdventureReach.Session, Story: story ?? Story);

    /// <summary>A galaxy around Fixture Prime with enough stations and bodies within a session's reach for a long short story's stops.</summary>
    public static Galaxy Roomy()
    {
        var galaxy = new Galaxy();

        for (var index = 0; index < 6; index++)
        {
            galaxy.MoreStations.Add(new StationSummary
            {
                Name = $"Roomy Port {index}", SystemName = $"Roomy {index}", SystemAddress = 900 + index, MarketId = 9000 + index,
                Position = new StarPosition(130 + (index * 10), 0, 0), Type = "Coriolis Starport", LargePads = 2,
            });
            galaxy.MoreBodies.Add(new BodySummary
            {
                Name = $"Roomy Rock {index} 1", SystemName = $"Roomy Rock {index}", SystemAddress = 950 + index, BodyId = 1,
                Position = new StarPosition(130 + (index * 10), 10, 0), IsLandable = true,
            });
        }

        return galaxy;
    }

    /// <summary>A short story's lines answer for up to eight objectives, each line a plain string.</summary>
    public static string ShortLines() => Lines(
        ChapterPlanner.MostObjectives,
        (number, objective) => objective["lines"] = new JsonArray(JsonValue.Create($"Page {number} of the ledger is open.")),
        answer => answer["opening"] = "A ledger sits in the hold.");

    /// <summary>The Commander's state; without <paramref name="position"/>, the location carries no star position.</summary>
    public static CommanderGameState State(bool position = true)
    {
        var store = new GameStateStore();

        foreach (var line in EmbeddedFixture.Lines("planner.Journal.2026-11-01T100000.01.log").Select(line => position ? line : line.Replace("\"StarPos\":[100.0,0.0,0.0], ", string.Empty, StringComparison.Ordinal)))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
            store.Apply(journalEvent!);
        }

        return store.Active!;
    }

    public static AdventureGenerator Generator(ILlmProvider? provider, Galaxy? galaxy = null, Func<CommanderGameState?>? state = null)
    {
        var commander = State();
        galaxy ??= new Galaxy();

        return new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, state ?? (() => commander), () => galaxy, () => null, null, null, NullLogger.Instance);
    }

    /// <summary>The plan the generator draws for <paramref name="ask"/>.</summary>
    public static async Task<ChapterPlan> PlanAsync(AdventureAsk ask)
    {
        var places = await Generator(null).ChapterPlacesAsync(ask, TestContext.Current.CancellationToken);
        var result = ChapterPlanner.Plan(ChapterInputs.Of(State(), ask), places, AdventureGenerator.PlanSeed(ask));

        return result.Plan ?? throw new InvalidOperationException($"No plan: {result.Refusal?.Rule}: {result.Refusal?.Text}");
    }

    /// <summary>A lines answer for <paramref name="objectives"/> objectives, each changed by <paramref name="change"/> when it is given.</summary>
    public static string Lines(int objectives, Action<int, JsonObject>? change = null, Action<JsonObject>? whole = null)
    {
        var written = new JsonArray();

        for (var number = 1; number <= objectives; number++)
        {
            var objective = new JsonObject
            {
                ["objective"] = number,
                ["title"] = $"Part {Word(number)}",
                ["reason"] = $"The broker's ledger points the Commander onward to part {Word(number)}.",
                ["lines"] = new JsonArray(new JsonObject { ["text"] = $"The ledger has another page, the {Word(number)}.", ["speaker"] = "ship" }),
            };

            change?.Invoke(number, objective);
            written.Add(objective);
        }

        var answer = new JsonObject
        {
            ["name"] = "The Broker's Ledger",
            ["premise"] = "A salvage broker's ledger turns up in the Commander's hold.",
            ["want"] = "To find who wrote the last page.",
            ["stake"] = "Whether the broker can be trusted.",
            ["turn"] = "The last page is in the broker's hand.",
            ["ending"] = "The ledger is closed.",
            ["opening"] = new JsonArray(new JsonObject { ["text"] = "A ledger sits in the hold.", ["speaker"] = "ship" }),
            ["reply"] = "Here is the chapter.",
            ["objectives"] = written,
        };

        whole?.Invoke(answer);

        return answer.ToJsonString();
    }

    /// <summary>A repair answer writing the words of each numbered objective again.</summary>
    public static string Repair(params int[] objectives) =>
        new JsonObject
        {
            ["objectives"] = new JsonArray([.. objectives.Select(number => (JsonNode)new JsonObject
            {
                ["objective"] = number,
                ["title"] = $"Part {Word(number)} Again",
                ["reason"] = $"The broker's ledger points the Commander onward to part {Word(number)} again.",
                ["lines"] = new JsonArray(new JsonObject { ["text"] = $"The ledger's {Word(number)} page is legible now.", ["speaker"] = "ship" }),
            })]),
        }.ToJsonString();

    /// <summary>
    /// A rewrite's lines answer: the words of new objectives 1 to <paramref name="count"/>, titled "New One" and on, each line spoken
    /// by the ship, or a plain string where <paramref name="speakers"/> is false, as for a short story. Numbers past the plan's are ignored.
    /// </summary>
    public static string NewWords(int count = ChapterPlanner.MostObjectives, bool speakers = true) =>
        new JsonObject
        {
            ["objectives"] = new JsonArray([.. Enumerable.Range(1, count).Select(number => (JsonNode)new JsonObject
            {
                ["objective"] = number,
                ["title"] = $"New {char.ToUpperInvariant(Word(number)[0])}{Word(number)[1..]}",
                ["reason"] = $"The broker's ledger sends the Commander on to new part {Word(number)}.",
                ["lines"] = speakers
                    ? new JsonArray(new JsonObject { ["text"] = $"The ledger's new page, the {Word(number)}, is open.", ["speaker"] = "ship" })
                    : new JsonArray(JsonValue.Create($"The ledger's new page, the {Word(number)}, is open.")),
            })]),
        }.ToJsonString();

    public const string NoObjections = """{"objections": []}""";

    private static string Word(int number) => number switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        _ => "eight",
    };

    /// <summary>Every property name anywhere in a JSON schema.</summary>
    public static IEnumerable<string> Properties(string schemaJson)
    {
        var found = new List<string>();

        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject shape:
                    if (shape["properties"] is JsonObject properties)
                    {
                        found.AddRange(properties.Select(property => property.Key));
                    }

                    foreach (var (_, child) in shape)
                    {
                        Walk(child);
                    }

                    break;
                case JsonArray list:
                    foreach (var child in list)
                    {
                        Walk(child);
                    }

                    break;
            }
        }

        Walk(JsonNode.Parse(schemaJson));

        return found;
    }

    /// <summary>The places around Fixture Prime: Fixture Alpha at war, Fixture Beta an Anarchy, and the mission's Fixture Second.</summary>
    internal sealed class Galaxy : StubGalaxyService, IGalaxyService
    {
        private static readonly StationSummary Alpha = new()
        {
            Name = "Alpha Port", SystemName = "Fixture Alpha", SystemAddress = 2, MarketId = 200, Position = new StarPosition(110, 0, 0), Type = "Coriolis Starport", LargePads = 2,
        };

        private static readonly StationSummary Gate = new()
        {
            Name = "Fixture Gate", SystemName = "Fixture Second", SystemAddress = 47002, MarketId = 4700002, Type = "Coriolis Starport", LargePads = 2,
        };

        private static readonly BodySummary Beta = new()
        {
            Name = "Fixture Beta 2", SystemName = "Fixture Beta", SystemAddress = 3, BodyId = 2, Position = new StarPosition(120, 0, 0), IsLandable = true,
        };

        private static readonly Dictionary<string, SystemSummary> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Fixture Alpha"] = new() { Name = "Fixture Alpha", SystemAddress = 2, Government = "Democracy", AtWar = true },
            ["Fixture Beta"] = new() { Name = "Fixture Beta", SystemAddress = 3, Government = "Anarchy" },
            ["Fixture Second"] = new() { Name = "Fixture Second", SystemAddress = 47002, Government = "Corporate" },
        };

        /// <summary>Every galaxy search made.</summary>
        public int Searches { get; private set; }

        /// <summary>Positions of systems the planner may route toward, by name.</summary>
        public Dictionary<string, StarPosition> Positions { get; } = new(StringComparer.OrdinalIgnoreCase) { ["Fixture Second"] = new StarPosition(125, 0, 0) };

        /// <summary>Extra stations every station search returns.</summary>
        public List<StationSummary> MoreStations { get; } = [];

        /// <summary>Extra landable bodies every body search returns.</summary>
        public List<BodySummary> MoreBodies { get; } = [];

        /// <summary>The bodies a search for the Guardian surface signal returns.</summary>
        public List<BodySummary> GuardianBodies { get; } = [];

        /// <summary>Every body search made.</summary>
        public List<BodyQuery> BodyQueries { get; } = [];

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken)
        {
            Searches++;

            var here = new StarPosition(100, 0, 0);

            return Task.FromResult<double?>(Positions.TryGetValue(to, out var there) ? here.DistanceTo(there) : null);
        }

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken)
        {
            Searches++;

            return Task.FromResult<StarPosition?>(Positions.TryGetValue(system, out var at) ? at : null);
        }

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Searches++;

            IReadOnlyList<SystemSummary> found = query.ReferenceSystem switch
            {
                "Fixture Prime" when query.Criteria.Any(criterion => criterion.Choices.Contains("War")) => [Systems["Fixture Alpha"] with { Position = new StarPosition(110, 0, 0) }],
                "Fixture Prime" => [Systems["Fixture Beta"] with { Position = new StarPosition(120, 0, 0) }],
                { } name when Systems.TryGetValue(name, out var system) => [system],
                _ => [],
            };

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Searches++;

            IReadOnlyList<StationSummary> found = query switch
            {
                { StationName: "Fixture Gate" } => [Gate],
                { StationName: null, Services.Count: 0 } => [Alpha, .. MoreStations],
                _ => [],
            };

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, found.Count, found));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Searches++;
            BodyQueries.Add(query);

            return Task.FromResult(query.Signal is null ? new BodySearchResult(query.ReferenceSystem, 1 + MoreBodies.Count, [Beta, .. MoreBodies]) : new BodySearchResult(query.ReferenceSystem, GuardianBodies.Count, GuardianBodies));
        }

        public override Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BodySummary>>(
            [
                .. Enumerable.Range(1, systemAddress == 2 ? 4 : 1).Select(id => new BodySummary { Name = $"Body {id}", SystemName = "Any", BodyId = id, Subtype = "Rocky body" }),
                new BodySummary { Name = "Star", SystemName = "Any", BodyId = 0, Subtype = "G (White-Yellow) Star" },
            ]);

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(new SystemBiology(systemAddress, []));
    }
}
