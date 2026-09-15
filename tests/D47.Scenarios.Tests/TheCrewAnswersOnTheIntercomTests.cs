using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>A turn opened with a hired pilot's name is answered by them, not the ship's AI.</summary>
public class TheCrewAnswersOnTheIntercomTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Journal =
    [
        """{ "timestamp":"2026-09-14T20:00:00Z", "event":"Commander", "FID":"F9000001", "Name":"Scenario" }""",
        """{ "timestamp":"2026-09-14T20:00:01Z", "event":"LoadGame", "FID":"F9000001", "Commander":"Scenario", "Ship":"Asp Explorer", "ShipID":1, "GameMode":"Open", "Credits":100000 }""",
        """{ "timestamp":"2026-09-14T20:00:02Z", "event":"CrewHire", "Name":"Vance Ilo", "CrewID":1, "CombatRank":"Expert" }""",
        """{ "timestamp":"2026-09-14T20:00:03Z", "event":"CrewAssign", "Name":"Vance Ilo", "CrewID":1, "Role":"Active" }""",
    ];

    [Fact]
    public async Task ThePilotByNameIsAnsweredWithNoTools()
    {
        var scenario = new Scenario
        {
            Id = "crew/vance-hired",
            Note = "#188: a hired pilot addressed by name",
            Utterance = "Vance Ilo, how is the fighter",
            Journal = Journal,
            Lines = world =>
            [
                new CrewLine(
                    () => world.GameState.Active?.Crew,
                    () => world.GameState.Active?.Ship.Name,
                    () => PersonaCatalog.Warden.Name),
            ],
        };

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Still flying, Commander. Wing took a knock."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

        var trace = await ScenarioRunner.RunAsync(scenario, provider, PersonaCatalog.Warden, cancellationToken: Token);

        var addressed = Assert.Single(trace.Events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal("Vance Ilo", addressed.Name);

        var prompt = Assert.Single(trace.Prompts);

        Assert.Contains("You are Vance Ilo", prompt.Persona, StringComparison.Ordinal);
        Assert.NotEqual(PersonaCatalog.Warden.RenderBlock(), prompt.Persona);
        Assert.Empty(prompt.Tools);
        Assert.Equal(TurnRoute.Model, trace.Result?.Route);
    }
}
