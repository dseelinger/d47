using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>
/// <b>The guardrails hold with the persona switched off, and with each of the eleven cores in.</b>
/// <para>
/// Prompt assembly puts the guardrails <em>above</em> the persona so switching personality off
/// cannot strip them, and until this file that ordering was an invariant with no test behind it on
/// a real turn. It is asserted directly here: every scenario runs with each shipped core and with
/// personality off entirely, and the safety assertions must not move.
/// </para>
/// <para>
/// Cheap once the harness exists, which is exactly why it is worth doing rather than arguing about.
/// It also closes the hole a reader of <c>PromptAssembly</c> would ask about first.
/// </para>
/// <para>
/// This is the hermetic half — the ordering is a fact about the bytes d47 sends and is true or
/// false with no model in the room. Whether each core <em>resists</em> equally is the paid half,
/// and it is measured in <see cref="LiveScenarioTests"/> rather than assumed here.
/// </para>
/// </summary>
public class PersonaMatrixTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every shipped core, plus null for personality off. Twelve configurations.</summary>
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
    /// The ordering itself, on every core: the guardrails open the cached system block, and the
    /// persona is strictly below them.
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
                // Personality off truncates a later block and cannot reach this one. Asserted as
                // equality rather than as a prefix, because "the guardrails are in there somewhere"
                // is not the claim — the claim is that nothing else is.
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
    /// <summary>
    /// Personality off, as a matrix entry. A sentinel rather than a null in the theory data,
    /// because xUnit prints the entry and <c>(null)</c> in a failing test name says less than
    /// <c>off</c> does about what configuration broke.
    /// </summary>
    public const string Off = "off";

    public static Persona? Resolve(string core) =>
        string.Equals(core, Off, StringComparison.Ordinal) ? null : PersonaCatalog.Resolve(core);
}
