using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Stories;

/// <summary>A detour proposer on a scripted model and <see cref="DetourGalaxy"/>, with the Commander in Oppi.</summary>
internal static class DetourFixtures
{
    public const long Anlave = 7_200_000_001;

    public const long Oppi = 3_382_387_380_970;

    public const long Colonia = 3_238_296_097_059;

    public const string GoToAnlave = """{"answer": "step", "reply": "Anlave first, then the story.", "title": "Go to Anlave", "kind": "arrive", "system": "Anlave", "station": null, "count": null, "faction": null, "mission": null, "commodity": null, "power": null, "options": []}""";

    public const string HuntBounties = """{"answer": "step", "reply": "Two bounties, then back to it.", "title": "Hunt two bounties", "kind": "bounty", "system": null, "station": null, "count": 2, "faction": null, "mission": null, "commodity": null, "power": null, "options": []}""";

    public const string WhichPlace = """{"answer": "question", "reply": "Which place counts as closer to home?", "title": null, "kind": null, "system": null, "station": null, "count": null, "faction": null, "mission": null, "commodity": null, "power": null, "options": ["Oppi", "Nowhere Real", "Colonia", "Anlave", "Sol"]}""";

    public const string WhichPlaceAgain = """{"answer": "question", "reply": "Closer to which place?", "title": null, "kind": null, "system": null, "station": null, "count": null, "faction": null, "mission": null, "commodity": null, "power": null, "options": []}""";

    /// <summary>Sets a proposer answering <paramref name="answers"/> in turn on the fixtures' director, and returns its model.</summary>
    public static RoundScriptedLlmProvider Give(StoryFixtures fixtures, ILogger? logger = null, Action? asked = null, params string[] answers)
    {
        var model = new RoundScriptedLlmProvider([.. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);
        var game = new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Location = new JournalLocation("Oppi", null, false, null) };

        fixtures.Director.Detours = new DetourProposer(
            () => model,
            () => null,
            () => null,
            () =>
            {
                asked?.Invoke();
                return game;
            },
            () => new DetourGalaxy(),
            null,
            null,
            logger ?? NullLogger.Instance);

        return model;
    }

    /// <summary>Knows Anlave, Oppi, Colonia and Sol; a faction or Power search answers three systems, farthest first.</summary>
    internal sealed class DetourGalaxy : StubGalaxyService
    {
        private static readonly Dictionary<string, long> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Anlave"] = Anlave,
            ["Oppi"] = Oppi,
            ["Colonia"] = Colonia,
            ["Sol"] = 10477373803,
            ["Far Holding"] = 7_200_000_002,
            ["Near Holding"] = 7_200_000_003,
            ["Middle Holding"] = 7_200_000_004,
            ["Last Holding"] = 7_200_000_005,
        };

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            if (query.Criteria.Any(criterion => criterion.Filter.Name is "faction" or "power"))
            {
                return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, 4,
                [
                    new SystemSummary { Name = "Far Holding", SystemAddress = Systems["Far Holding"], Distance = 40 },
                    new SystemSummary { Name = "Near Holding", SystemAddress = Systems["Near Holding"], Distance = 5 },
                    new SystemSummary { Name = "Last Holding", SystemAddress = Systems["Last Holding"], Distance = 90 },
                    new SystemSummary { Name = "Middle Holding", SystemAddress = Systems["Middle Holding"], Distance = 20 },
                ]));
            }

            var reference = query.ReferenceSystem ?? string.Empty;
            var name = Systems.Keys.FirstOrDefault(known => string.Equals(known, reference, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(name is null
                ? new GalaxySearchResult(reference, 0, [])
                : new GalaxySearchResult(name, 1, [new SystemSummary { Name = name, SystemAddress = Systems[name], Distance = 0 }]));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);
    }
}
