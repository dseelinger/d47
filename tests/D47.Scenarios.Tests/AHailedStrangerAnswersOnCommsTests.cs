using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Listening;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>A stranger hails the Commander, the selector moves to them, and the comms key reaches them (#1605).</summary>
[Trait("Category", "Integration")]
public class AHailedStrangerAnswersOnCommsTests
{
    private const string Hailer = "Odalys Fenn";

    private const string HailText = "Commander, that was some fine flying back there.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Journal =
    [
        """{ "timestamp":"2026-09-14T20:00:00Z", "event":"Commander", "FID":"F9000001", "Name":"Scenario" }""",
        """{ "timestamp":"2026-09-14T20:00:01Z", "event":"LoadGame", "FID":"F9000001", "Commander":"Scenario", "Ship":"Asp Explorer", "ShipID":1, "GameMode":"Open", "Credits":100000 }""",
        """{ "timestamp":"2026-09-14T20:00:02Z", "event":"Location", "Docked":false, "StarSystem":"Sol", "SystemAddress":10477373803, "Body":"Sol", "BodyID":0, "BodyType":"Star" }""",
    ];

    [Fact]
    public async Task TheHailerAnswersTheCommsKey()
    {
        var roster = new NpcsHeardHere();
        var channels = new CommsChannels();
        var hail = new NpcChatterLine(Hailer, HailText);

        var scenario = new Scenario
        {
            Id = "comms/hailed-then-comms-key",
            Note = "#1605: a hail selects the hailer, and the comms key reaches them with no name said",
            Utterance = "Thanks, just passing through",
            Journal = Journal,
            Target = PttTarget.Comms,
            Comms = () => channels.Selected,
            Lines = world =>
            {
                var line = new ChatterLine(() => world.GameState.Active?.Location.StarSystem, roster: () => roster);

                // As the Announcer and the comms feed handle a spoken hail.
                roster.Heard(
                    new Announcement(NpcChatter.LineKey, HailText)
                    {
                        Voice = VoiceRole.Comms,
                        Speaker = Hailer,
                        CommsChannel = "npc",
                        Invented = new NpcChatterHeard(hail, 7, Hail: true),
                    },
                    null,
                    DateTimeOffset.UnixEpoch);
                line.Heard(hail, 7);
                channels.Update(new CommsInputs { Heard = roster.Present });
                Assert.True(channels.Hailed(Hailer));

                return [line];
            },
        };

        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Safe travels, Commander."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

        var trace = await ScenarioRunner.RunAsync(scenario, provider, PersonaCatalog.Warden, cancellationToken: Token);

        var addressed = Assert.Single(trace.Events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Comms, addressed.Role);
        Assert.Equal(Hailer, addressed.Name);

        var prompt = Assert.Single(trace.Prompts);

        Assert.Contains($"You are {Hailer}", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"{Hailer}: {HailText}", prompt.Persona, StringComparison.Ordinal);
        Assert.Empty(prompt.Tools);
        Assert.Equal("Safe travels, Commander.", trace.Result?.Text);
    }
}
