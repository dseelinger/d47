using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

public sealed class IllegalMissionsAreSteeredToAnarchyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Quiet Job", "premise": "A job is owed.", "want": "To finish it.",
         "stake": "Whether it can be paid for.", "turn": "The payer is the target.", "ending": "It is done."}
        """;

    [Theory]
    [InlineData("Mission_OnFoot_AssassinationIllegal_Covert_MB")]
    [InlineData("Mission_OnFoot_Heist_Covert_MB")]
    [InlineData("Mission_OnFoot_Sabotage_Power_MB")]
    [InlineData("mission_onfoot_salvageillegal")]
    [InlineData("Mission_OnFoot_Heist")]
    public void ACrimeAgainstItsTargetIsIllegal(string name) => Assert.True(MissionFamilies.IsIllegal(name));

    [Theory]
    [InlineData("Mission_OnFoot_Assassination_MB")]
    [InlineData("Mission_OnFoot_Salvage_MB")]
    [InlineData("Mission_OnFoot")]
    [InlineData("Mission_Courier")]
    [InlineData(null)]
    public void AnyOtherMissionIsNot(string? name) => Assert.False(MissionFamilies.IsIllegal(name));

    [Fact]
    public async Task AnIllegalMissionAfterAnArriveInDemocracyIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Ossen's Lantern")), Saying(Beats("Ossen's Lantern")));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Objective 2 (The Job) is an illegal mission", outcome.Refusal);
        Assert.Contains("Ossen's Lantern is not an Anarchy system", outcome.Refusal);
    }

    [Fact]
    public async Task AnIllegalMissionAfterAnArriveInAnarchyPasses()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Dyson's Hollow")));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AnIllegalMissionThatDoesNotFollowAPlaceIsRefused()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats(null)), Saying(Beats(null)));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("The objective before it is not one", outcome.Refusal);
    }

    [Fact]
    public async Task TheWriterIsHandedTheAnarchySystemsAndTheRule()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Dyson's Hollow")));

        await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("Anarchy systems within reach, from the galaxy search, nearest first:", brief);
        Assert.Contains("- Dyson's Hollow (5 ly)", brief);
        Assert.DoesNotContain("- Ossen's Lantern (5 ly)", brief);
        Assert.Contains("directly after an \"arrive\" or \"dock\" objective in one of these systems", brief);
        Assert.Contains("an Anarchy system can hold settlements owned by lawful factions", brief);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithNoAnarchySystemKnownTheWriterIsToldToUseNoIllegalMissionAndNothingIsRefused(bool searchFails)
    {
        var galaxy = new Galaxy { AnarchyInReach = false, AnarchySearchFails = searchFails };
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Ossen's Lantern")));

        var outcome = await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("No Anarchy system is known within reach, so write no illegal mission objective", brief);
        Assert.DoesNotContain("Anarchy systems within reach, from the galaxy search", brief);

        // The rule is not checked, so the draft stands.
        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AChapterWithoutAnIllegalMissionPassesWhereverItArrives()
    {
        var provider = new RoundScriptedLlmProvider(Saying(Spine), Saying(Beats("Ossen's Lantern", "Mission_OnFoot_Assassination")));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    /// <summary>Arrive in <paramref name="system"/> (or open with the mission when null), then a mission, then a rank.</summary>
    private static string Beats(string? system, string family = "Mission_OnFoot_Heist_Covert")
    {
        var arrive = system is null
            ? string.Empty
            : $$"""{"title": "The Arrival", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "{{system}}", "station": null, "body": null, "career": null, "rank": null, "line": "Here."},""";

        return $$"""
            {"opening": "A job.", "reply": "Here it is.", "beats": [
              {{arrive}}
              {"title": "The Job", "function": "turn", "kind": "mission", "system": null, "station": null, "body": null, "career": null, "rank": null, "count": 1, "faction": null, "mission": "{{family}}", "line": "Take the job."},
              {"title": "The Quiet Job Ends", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "It is done."}
            ]}
            """;
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     """{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        var state = store.Active;

        return new AdventureGenerator(
            () => provider,
            () => null,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance);
    }

    /// <summary>Oppi and Ossen's Lantern run by Democracy, Dyson's Hollow by Anarchy.</summary>
    private sealed class Galaxy : IGalaxyService
    {
        private static readonly (string Name, long Address, string Government)[] Systems =
        [
            ("Oppi", 3382387380970, "Democracy"),
            ("Ossen's Lantern", AdventureFixtures.Lantern, "Democracy"),
            ("Dyson's Hollow", AdventureFixtures.Hollow, "Anarchy"),
        ];

        public bool AnarchyInReach { get; init; } = true;

        public bool AnarchySearchFails { get; init; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            if (query.Criteria.Any(criterion => criterion.Filter.Name == "government"))
            {
                if (AnarchySearchFails)
                {
                    throw new GalaxyUnavailableException("the service is down");
                }

                var found = AnarchyInReach
                    ? Systems.Where(system => system.Government == "Anarchy").Select(Summary).ToList()
                    : [];

                return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, found.Count, found));
            }

            var named = Systems.Where(system => string.Equals(system.Name, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)).Select(Summary).ToList();

            return Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, named.Count, named));
        }

        private static SystemSummary Summary((string Name, long Address, string Government) system) =>
            new() { Name = system.Name, SystemAddress = system.Address, Government = system.Government, Distance = 5 };

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
