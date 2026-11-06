using System.Text.Json.Nodes;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Tests;

/// <summary>
/// A Commander docked at Jameson Memorial with a known position, and a galaxy of positioned places around Shinrarta Dezhra, so a
/// short story asked for on the Short Story page can be planned; and the lines answer that names it "The Missing Freight".
/// </summary>
internal static class ShortStoryWorld
{
    private static readonly StarPosition Here = new(55.72, 17.59, 27.16);

    public static CommanderGameState State() =>
        JournalScript.State(
            """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            """{ "timestamp":"2026-08-22T19:01:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "StarPos":[55.72,17.59,27.16], "Docked":true, "StationName":"Jameson Memorial", "StationType":"Coriolis", "MarketID":128666762, "Body":"Shinrarta Dezhra A", "BodyID":1 }""");

    /// <summary>The lines answer for a short story of up to eight objectives: "The Missing Freight", replying "Here it is."</summary>
    public static readonly string Lines = new JsonObject
    {
        ["name"] = "The Missing Freight",
        ["premise"] = "Freight went missing on the way to Maren Anchorage.",
        ["want"] = "To find the freight.",
        ["stake"] = "Whether a debt can be owed to nobody.",
        ["turn"] = "It was never loaded.",
        ["ending"] = "The freight is found and delivered.",
        ["opening"] = "Somebody is paying.",
        ["reply"] = "Here it is.",
        ["objectives"] = new JsonArray([.. Enumerable.Range(1, 8).Select(number => (JsonNode)new JsonObject
        {
            ["objective"] = number,
            ["title"] = $"Leg {number}",
            ["reason"] = "The freight's manifest names the next place the Commander goes.",
            ["lines"] = new JsonArray(JsonValue.Create($"The manifest's page {number} is stamped.")),
        })]),
    }.ToJsonString();

    /// <summary>Stations, landable bodies and an Anarchy system at war, a few light years from Shinrarta Dezhra.</summary>
    internal sealed class Galaxy : StubGalaxyService
    {
        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new GalaxySearchResult(query.ReferenceSystem, 1,
            [
                new SystemSummary { Name = "Hollow Reach", SystemAddress = 300, Position = new StarPosition(Here.X, Here.Y, Here.Z + 6), Government = "Anarchy", AtWar = true },
            ]));

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);

        public override Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BodySummary>>([new BodySummary { Name = "Rock 1", SystemName = "Any", BodyId = 1, Subtype = "Rocky body" }]);

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult(new SystemBiology(systemAddress, []));

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> stations = query.Services.Count > 0
                ? []
                : [.. Enumerable.Range(1, 4).Select(index => new StationSummary
                {
                    Name = $"Maren Anchorage {index}", SystemName = $"Dyson's Hollow {index}", SystemAddress = 100 + index, MarketId = 1000 + index,
                    Position = new StarPosition(Here.X + (index * 4), Here.Y, Here.Z), HasLargePad = true, LargePads = 2, Type = "Coriolis Starport",
                })];

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Count, stations));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<BodySummary> bodies = query.Signal is not null
                ? []
                : [.. Enumerable.Range(1, 4).Select(index => new BodySummary
                {
                    Name = $"Ossen's Lantern {index} a", SystemName = $"Ossen's Lantern {index}", SystemAddress = 200 + index, BodyId = 1,
                    Position = new StarPosition(Here.X, Here.Y + (index * 4), Here.Z), IsLandable = true,
                })];

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));
        }
    }
}
