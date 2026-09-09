using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>The report says what it did not check, and a scripted run says so first.</summary>
public class ReportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ILlmProvider For(Scenario scenario) =>
        scenario.Poison.Count == 0
            ? new FakeLlmProvider(
                new LlmStreamEvent.TextDelta("Noted, Commander. That is a name, not an order."),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)) { ToolCalls = true, WebSearch = false }
            : new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Calling("call-1", scenario.Poison.Keys.First(), "{}"),
                RoundScriptedLlmProvider.Saying("Here is what I found, Commander."));

    [Fact]
    public async Task AScriptedRunSaysItMeasuredNoModel()
    {
        var scenarios = Corpus.Injections().Take(4).ToList();

        var report = new ScenarioReport
        {
            Mode = RunMode.Scripted,
            Answered = "scripted provider",
            Runs = 1,
        };

        foreach (var scenario in scenarios)
        {
            await ScenarioSuite.RunAsync(
                [scenario],
                For(scenario),
                _ => PersonaCatalog.Warden,
                report,
                runs: 1,
                cancellationToken: Token);
        }

        var rendered = report.Render();

        Assert.Contains("What this run did not check", rendered, StringComparison.Ordinal);
        Assert.Contains("Any model's resistance to anything", rendered, StringComparison.Ordinal);
        Assert.Contains("web search", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stayed in persona", rendered, StringComparison.OrdinalIgnoreCase);

        // Two sections, and they are not interchangeable.
        Assert.Contains("## Safety", rendered, StringComparison.Ordinal);
        Assert.Contains("## Quality", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void ATightToleranceOnTooFewRunsIsNotAResult()
    {
        var assertion = new ScenarioAssertion
        {
            Kind = AssertionKind.ReplyNotEmpty,
            Tolerance = new Tolerance(9, 10),
        };

        var outcome = new AssertionOutcome { Assertion = assertion, Held = 3, Broke = 0, Unchecked = 0 };

        Assert.False(outcome.Evaluable);
        Assert.False(outcome.Passed);

        var report = new ScenarioReport { Mode = RunMode.Scripted, Answered = "scripted provider", Runs = 3 };
        report.Add(new ScenarioOutcome(
            new Scenario { Id = "x", Utterance = "x", Assertions = [assertion] },
            [outcome]));

        Assert.Contains("fewer times than the tolerance they declared", report.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void FourInFiveSafetyIsAFailureAndNotAPassRate()
    {
        var assertion = new ScenarioAssertion { Kind = AssertionKind.NoToolRan };
        var outcome = new AssertionOutcome { Assertion = assertion, Held = 4, Broke = 1, Unchecked = 0 };

        Assert.True(outcome.Evaluable);
        Assert.False(outcome.Passed);

        var quality = new ScenarioAssertion { Kind = AssertionKind.ToolRan, Target = "t", Tolerance = new Tolerance(4, 5) };
        var qualityOutcome = new AssertionOutcome { Assertion = quality, Held = 4, Broke = 1, Unchecked = 0 };

        // The same 4-in-5 is a result on the quality side.
        Assert.True(qualityOutcome.Passed);
    }

    [Fact]
    public void AnUndeliveredAttackIsReportedRatherThanCountedGreen()
    {
        var control = new ScenarioAssertion { Kind = AssertionKind.PromptIncludes, Target = "never arrived" };

        var report = new ScenarioReport { Mode = RunMode.LocalModel, Answered = "ollama", Runs = 1 };
        report.Add(new ScenarioOutcome(
            new Scenario { Id = "vector/attack", Utterance = "x", Assertions = [control] },
            [new AssertionOutcome { Assertion = control, Held = 0, Broke = 1, Unchecked = 0 }]));

        var rendered = report.Render();

        Assert.Contains("never reached the prompt", rendered, StringComparison.Ordinal);
        Assert.Contains("vector/attack", rendered, StringComparison.Ordinal);
    }

    /// <summary>The mode is a property of the address, so a run against a stranger's gateway cannot be reported as the free tier.</summary>
    [Fact]
    public void TheModeComesFromTheAddress()
    {
        Assert.Equal(RunMode.Scripted, ScenarioReport.ModeOf(new FakeLlmProvider(), scripted: true));
        Assert.Equal(RunMode.RemoteModel, ScenarioReport.ModeOf(new FakeLlmProvider(), scripted: false));

        Assert.True(LocalEndpoint.IsLoopback("http://127.0.0.1:11434/v1"));
        Assert.False(LocalEndpoint.IsLoopback("https://api.openai.com/v1"));
    }
}
