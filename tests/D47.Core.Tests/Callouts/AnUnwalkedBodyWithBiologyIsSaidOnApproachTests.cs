using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>First footfall on approach: biology on the body and nobody has walked it (#581).</summary>
public class AnUnwalkedBodyWithBiologyIsSaidOnApproachTests
{
    private const long SystemAddress = 1_000_100;
    private const int BodyId = 4;
    private const string BodyName = "Smojue EB-O d6-37 AB 1 b";

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(bool wasFootfalled) => $$"""
        {"timestamp":"3311-04-02T09:00:00Z","event":"Scan","ScanType":"Detailed","SystemAddress":{{SystemAddress}},
         "BodyID":{{BodyId}},"BodyName":"{{BodyName}}","Landable":true,"WasFootfalled":{{(wasFootfalled ? "true" : "false")}}}
        """;

    private static readonly string Biology = $$"""
        {"timestamp":"3311-04-02T09:01:00Z","event":"FSSBodySignals","SystemAddress":{{SystemAddress}},
         "BodyID":{{BodyId}},"BodyName":"{{BodyName}}",
         "Signals":[{"Type":"$SAA_SignalType_Biological;","Type_Localised":"Biological","Count":3}]}
        """;

    private static readonly string Approach = $$"""
        {"timestamp":"3311-04-02T09:10:00Z","event":"ApproachBody","StarSystem":"Smojue EB-O d6-37",
         "SystemAddress":{{SystemAddress}},"Body":"{{BodyName}}","BodyID":{{BodyId}}}
        """;

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static List<string> Said(ICallout callout, CommanderGameState state, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(
                DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [Parse(Approach)]))
            .Select(announcement => announcement.Text)];

    [Fact]
    public void AnUnwalkedBodyWithBiologyIsSaidOnce()
    {
        var state = Commander(Scan(wasFootfalled: false), Biology, Approach);
        var callout = new FootfallApproachCallout();

        Assert.Equal(
            ["Nobody has set foot on Smojue EB-O d6-37 AB 1 b. First footfall bonus is there."],
            Said(callout, state));

        Assert.Empty(Said(callout, state));
    }

    [Fact]
    public void AWalkedBodyIsNotSaid() =>
        Assert.Empty(Said(new FootfallApproachCallout(), Commander(Scan(wasFootfalled: true), Biology, Approach)));

    [Fact]
    public void ABodyWithoutBiologyIsNotSaid() =>
        Assert.Empty(Said(new FootfallApproachCallout(), Commander(Scan(wasFootfalled: false), Approach)));

    [Fact]
    public void NothingIsSaidFromTheBacklog() =>
        Assert.Empty(Said(new FootfallApproachCallout(), Commander(Scan(wasFootfalled: false), Biology, Approach), priming: true));
}
