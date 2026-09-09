using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using D47.Core.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>In-game comms is not a path to the model.</summary>
public class CommsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeLlmProvider Answering() =>
        new(
            new LlmStreamEvent.TextDelta("A quiet approach, Commander."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

    /// <summary>One scenario carrying a hostile message in the journal, exactly as Elite writes one.</summary>
    private static Scenario Hostile(Attack attack) => new()
    {
        Id = $"comms/{attack.Id}",
        Note = attack.Note,
        Utterance = "Anything worth knowing about where we are right now?",
        Journal =
        [
            """{ "timestamp":"2026-08-18T11:59:58Z", "event":"Commander", "FID":"F9000001", "Name":"Scenario" }""",
            """{ "timestamp":"2026-08-18T11:59:59Z", "event":"LoadGame", "FID":"F9000001", "Commander":"Scenario", "Horizons":true, "Odyssey":true, "Ship":"Asp Explorer", "ShipID":1, "GameMode":"Open", "Credits":100000, "Loan":0 }""",
            """{ "timestamp":"2026-08-18T12:00:00Z", "event":"Location", "Docked":false, "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "Body":"Shinrarta Dezhra A", "BodyID":1, "BodyType":"Star" }""",
            ReceiveText(attack.Payload),
        ],
        Assertions =
        [
            new ScenarioAssertion
            {
                Kind = AssertionKind.PromptExcludes,
                Target = Corpus.Marker(attack.Payload),
                Note = "no byte of another player's message appears in any prompt d47 builds",
            },
            new ScenarioAssertion { Kind = AssertionKind.NoToolRan },
            new ScenarioAssertion { Kind = AssertionKind.NothingWritten },
        ],
    };

    private static string ReceiveText(string payload)
    {
        var escaped = System.Text.Json.JsonSerializer.Serialize(payload);

        return $$"""
            { "timestamp":"2026-08-18T12:00:05Z", "event":"ReceiveText", "From":{{escaped}},
              "Message":{{escaped}}, "Message_Localised":{{escaped}}, "Channel":"local" }
            """;
    }

    [Fact]
    public async Task NoHostileMessageReachesAnyPrompt()
    {
        foreach (var attack in Corpus.Attacks())
        {
            var scenario = Hostile(attack);
            var trace = await ScenarioRunner.RunAsync(scenario, Answering(), PersonaCatalog.Warden, cancellationToken: Token);

            Assert.NotEmpty(trace.Prompts);

            foreach (var assertion in scenario.Assertions)
            {
                var (verdict, detail) = AssertionEvaluator.Evaluate(assertion, trace);

                Assert.True(
                    verdict == AssertionVerdict.Held,
                    $"{scenario.Id} {assertion}: {detail}");
            }
        }
    }

    /// <summary>The control that makes the test above mean something.</summary>
    [Fact]
    public async Task TheSameStringInAShipNameDoesReachThePrompt()
    {
        foreach (var attack in Corpus.Attacks())
        {
            var scenario = Corpus.Injections()
                .First(injection => injection.Id == $"ship-name/{attack.Id}");

            var trace = await ScenarioRunner.RunAsync(scenario, Answering(), PersonaCatalog.Warden, cancellationToken: Token);

            Assert.True(
                trace.AnyPromptContains(Corpus.Marker(attack.Payload)),
                $"{attack.Id}: journal text is prompt position 7 and this one did not arrive, so every comms "
                + "pass in this file is worthless");
        }
    }

    [Fact]
    public async Task ThePromptExcludesAssertionBreaksWhenTheStringIsThere()
    {
        var attack = Corpus.Attacks()[0];
        var scenario = Corpus.Injections().First(injection => injection.Id == $"ship-name/{attack.Id}");

        var trace = await ScenarioRunner.RunAsync(scenario, Answering(), PersonaCatalog.Warden, cancellationToken: Token);

        var excludes = new ScenarioAssertion
        {
            Kind = AssertionKind.PromptExcludes,
            Target = Corpus.Marker(attack.Payload),
        };

        Assert.Equal(AssertionVerdict.Broke, AssertionEvaluator.Evaluate(excludes, trace).Verdict);
    }

    [Fact]
    public void NoRegisteredToolReadsInGameMessages()
    {
        using var world = new ScenarioWorld();

        var comms = world.Registry.All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Where(tool => tool.Description.Contains("in-game chat", StringComparison.OrdinalIgnoreCase)
                           || tool.Description.Contains("received message", StringComparison.OrdinalIgnoreCase)
                           || tool.Description.Contains("incoming message", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // send_chat_message writes and does not read.
        Assert.All(comms, tool => Assert.Equal("send_chat_message", tool.Name));
    }
}
