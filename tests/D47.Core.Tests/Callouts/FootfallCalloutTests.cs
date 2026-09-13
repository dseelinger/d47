using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The first footfall on a body, marked by "Keep each body's scan in game state" (#202).</summary>
public class FootfallCalloutTests
{
    private const long SystemAddress = 1_000_100;
    private const int BodyId = 4;
    private const string BodyName = "Smojue EB-O d6-37 AB 1 b";

    private static readonly DateTimeOffset ScannedAt = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DisembarkedAt = ScannedAt.AddMinutes(5);

    private static CalloutContext Context(CommanderGameState state, bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: state,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]);

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(bool? wasFootfalled, DateTimeOffset? at = null)
    {
        var timestamp = at ?? ScannedAt;
        var footfallField = wasFootfalled is { } value ? $"\"WasFootfalled\":{(value ? "true" : "false")}," : "";

        return $$"""
            {
                "timestamp": "{{timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}",
                "event": "Scan",
                "SystemAddress": {{SystemAddress}},
                "BodyID": {{BodyId}},
                "BodyName": "{{BodyName}}",
                "Landable": true,
                {{footfallField}}
                "ScanType": "Detailed"
            }
            """;
    }

    private static string Disembark(bool srv = false, DateTimeOffset? at = null)
    {
        var timestamp = at ?? DisembarkedAt;

        return $$"""
            {
                "timestamp": "{{timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}",
                "event": "Disembark",
                "SRV": {{(srv ? "true" : "false")}},
                "OnPlanet": true,
                "OnStation": false,
                "SystemAddress": {{SystemAddress}},
                "Body": "{{BodyName}}",
                "BodyID": {{BodyId}}
            }
            """;
    }

    [Fact]
    public void AFirstDisembarkOnAnUnfootfalledBodyIsAnnounced()
    {
        var disembark = Disembark();
        var state = Commander(Scan(wasFootfalled: false), disembark);

        var said = Assert.Single(new FootfallCallout().Examine(Context(state, false, disembark)));

        Assert.Contains(BodyName, said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ASecondDisembarkOnThatBodySaysNothing()
    {
        var second = Disembark(at: DisembarkedAt.AddMinutes(1));
        var state = Commander(Scan(wasFootfalled: false), Disembark(), second);

        Assert.Empty(new FootfallCallout().Examine(Context(state, false, second)));
    }

    [Fact]
    public void PrimingTheFirstDisembarkThenASecondSaysNothing()
    {
        var first = Disembark();
        var second = Disembark(at: DisembarkedAt.AddMinutes(1));
        var state = Commander(Scan(wasFootfalled: false), first);

        Assert.Empty(new FootfallCallout().Examine(Context(state, true, first)));

        state = Commander(Scan(wasFootfalled: false), first, second);

        Assert.Empty(new FootfallCallout().Examine(Context(state, false, second)));
    }

    [Fact]
    public void ADisembarkThatSetNoFootfallTakenAtSaysNothing()
    {
        var disembark = Disembark();
        var state = Commander(Scan(wasFootfalled: true), disembark);

        Assert.Empty(new FootfallCallout().Examine(Context(state, false, disembark)));
    }

    [Fact]
    public void PrimingSaysNothing()
    {
        var disembark = Disembark();
        var state = Commander(Scan(wasFootfalled: false), disembark);

        Assert.Empty(new FootfallCallout().Examine(Context(state, true, disembark)));
    }
}
