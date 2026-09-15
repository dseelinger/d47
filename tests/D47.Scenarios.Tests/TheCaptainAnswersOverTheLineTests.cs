using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>A turn opened with "Captain" is answered by the carrier's captain, not the ship's AI.</summary>
public class TheCaptainAnswersOverTheLineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Journal =
    [
        """{ "timestamp":"2026-09-14T20:00:00Z", "event":"Commander", "FID":"F9000001", "Name":"Scenario" }""",
        """{ "timestamp":"2026-09-14T20:00:01Z", "event":"LoadGame", "FID":"F9000001", "Commander":"Scenario", "Ship":"Asp Explorer", "ShipID":1, "GameMode":"Open", "Credits":100000 }""",
        """{ "timestamp":"2026-09-14T20:00:02Z", "event":"Location", "Docked":false, "StarSystem":"Sol", "SystemAddress":10477373803, "Body":"Sol", "BodyID":0, "BodyType":"Star" }""",
        """{ "timestamp":"2026-09-14T20:00:03Z", "event":"CarrierStats", "CarrierID":3700000001, "Callsign":"BNH-T2F", "Name":"Sacred Fire", "DockingAccess":"all", "AllowNotorious":false, "FuelLevel":792, "JumpRangeCurr":500.0, "JumpRangeMax":500.0, "PendingDecommission":false, "SpaceUsage":{ "TotalCapacity":25000, "Crew":0, "Cargo":540, "CargoSpaceReserved":0, "ShipPacks":0, "ModulePacks":0, "FreeSpace":24460 }, "Finance":{ "CarrierBalance":750352669 }, "Crew":[] }""",
        """{ "timestamp":"2026-09-14T20:00:04Z", "event":"CarrierLocation", "CarrierType":"FleetCarrier", "CarrierID":3700000001, "StarSystem":"Wolf 359", "SystemAddress":10477373804, "BodyID":0 }""",
    ];

    [Fact]
    public async Task CaptainByNameIsAnsweredWithTheCaptainsBrief()
    {
        var scenario = new Scenario
        {
            Id = "carrier-captain/fuel-at-100-ly",
            Note = "#185: the captain addressed by name, carrier owned and 100 Ly away",
            Utterance = "Captain, how much fuel have we got",
            Journal = Journal,
            Lines = world =>
            [
                new CaptainLine(
                    () => world.GameState.Active?.Carrier ?? CarrierState.None,
                    () => world.GameState.Active?.Location.StarSystem,
                    () => PersonaCatalog.Warden.Name,
                    (_, _, _) => Task.FromResult<double?>(100)),
            ],
        };

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Seven hundred ninety-two tonnes of tritium, Commander."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

        var trace = await ScenarioRunner.RunAsync(scenario, provider, PersonaCatalog.Warden, cancellationToken: Token);

        var addressed = Assert.Single(trace.Events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.CarrierCaptain, addressed.Role);

        List<TurnEvent> events = [.. trace.Events];
        Assert.IsType<TurnEvent.Routed>(events[events.IndexOf(addressed) + 1]);

        var prompt = Assert.Single(trace.Prompts);

        Assert.Contains("captain of the Commander's fleet carrier, Sacred Fire", prompt.Persona, StringComparison.Ordinal);
        Assert.NotEqual(PersonaCatalog.Warden.RenderBlock(), prompt.Persona);
        Assert.Equal(TurnRoute.Model, trace.Result?.Route);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(426)]
    public async Task AFarCarrierLosesWordsOnTheWay(double lightYears)
    {
        const string answer =
            "Seven hundred ninety-two tonnes of tritium in the depot, Commander, and the market is buying at a "
            + "good price this week, so we can sell some if you like.";

        var scenario = new Scenario
        {
            Id = $"carrier-captain/fuel-at-{lightYears}-ly",
            Note = "#187: the captain's words thin with the carrier's distance",
            Utterance = "Captain, how much fuel have we got",
            Journal = Journal,
            Lines = world =>
            [
                new CaptainLine(
                    () => world.GameState.Active?.Carrier ?? CarrierState.None,
                    () => world.GameState.Active?.Location.StarSystem,
                    () => PersonaCatalog.Warden.Name,
                    (_, _, _) => Task.FromResult<double?>(lightYears)),
            ],
        };

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta(answer),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

        var trace = await ScenarioRunner.RunAsync(scenario, provider, PersonaCatalog.Warden, cancellationToken: Token);

        var signal = Assert.Single(trace.Events.OfType<TurnEvent.Addressed>()).Signal;
        var heard = string.Concat(trace.Events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text));

        if (signal == 1)
        {
            Assert.Equal(answer, heard);
        }
        else
        {
            Assert.InRange(signal, 0.15, 0.25);
            Assert.True(heard.Length < answer.Length, $"\"{heard}\" is no shorter than the answer");
            Assert.Contains(LinkSignal.Lost, heard, StringComparison.Ordinal);
        }
    }
}
