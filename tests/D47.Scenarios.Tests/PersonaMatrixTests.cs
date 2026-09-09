using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>The guardrails hold with the persona switched off, and with each of the eleven cores in.</summary>
public class PersonaMatrixTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every shipped core, plus null for personality off.</summary>
    public static TheoryData<string> Cores()
    {
        var data = new TheoryData<string>();

        foreach (var persona in PersonaCatalog.All)
        {
            data.Add(persona.Id);
        }

        data.Add(PersonaMatrix.Off);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cores))]
    public async Task TheSafetyAssertionsDoNotMoveWithThePersona(string core)
    {
        var persona = PersonaMatrix.Resolve(core);

        foreach (var scenario in Corpus.Injections())
        {
            var provider = scenario.Poison.Count == 0
                ? (ILlmProvider)new FakeLlmProvider(
                    new LlmStreamEvent.TextDelta("I read that as information about the world, Commander."),
                    new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)) { ToolCalls = true, WebSearch = false }
                : new RoundScriptedLlmProvider(
                    RoundScriptedLlmProvider.Calling("call-1", scenario.Poison.Keys.First(), "{}"),
                    RoundScriptedLlmProvider.Saying("Here is what I found, Commander."));

            var trace = await ScenarioRunner.RunAsync(scenario, provider, persona, cancellationToken: Token);

            foreach (var assertion in scenario.Assertions.Where(a => a.Class == AssertionClass.Safety))
            {
                var (verdict, detail) = AssertionEvaluator.Evaluate(assertion, trace);

                Assert.True(
                    verdict == AssertionVerdict.Held,
                    $"{core} / {scenario.Id} / {assertion}: {detail}");
            }
        }
    }

    /// <summary>
    /// The ordering itself, on every core: the guardrails open the cached system block, and the persona
    /// is strictly below them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cores))]
    public async Task TheGuardrailsOpenTheBlockWhicheverCoreIsIn(string core)
    {
        var persona = PersonaMatrix.Resolve(core);

        var trace = await ScenarioRunner.RunAsync(
            Corpus.Injections()[0],
            new FakeLlmProvider(
                new LlmStreamEvent.TextDelta("Understood."),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)) { ToolCalls = true, WebSearch = false },
            persona,
            cancellationToken: Token);

        Assert.NotEmpty(trace.Prompts);

        foreach (var prompt in trace.Prompts)
        {
            var block = prompt.RenderCachedSystemBlock();

            Assert.StartsWith(Guardrails.Text, block, StringComparison.Ordinal);

            if (persona is null)
            {
                // Personality off truncates a later block and cannot reach this one.
                Assert.Equal(Guardrails.Text, block);
            }
            else
            {
                Assert.Contains(persona.RenderBlock(), block, StringComparison.Ordinal);
            }
        }
    }
}

/// <summary>Turning a core id — or the word for none — into a persona.</summary>
public static class PersonaMatrix
{
    /// <summary>Personality off, as a matrix entry.</summary>
    public const string Off = "off";

    public static Persona? Resolve(string core) =>
        string.Equals(core, Off, StringComparison.Ordinal) ? null : PersonaCatalog.Resolve(core);
}
