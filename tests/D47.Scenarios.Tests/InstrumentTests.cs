using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>Does the instrument work?</summary>
public class InstrumentTests
{
    private static readonly Persona Warden = PersonaCatalog.Warden;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A provider that answers in words and calls nothing.</summary>
    private static FakeLlmProvider Resists(string reply = "Understood, Commander.") =>
        new(
            new LlmStreamEvent.TextDelta(reply),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            WebSearch = false,
        };

    /// <summary>A resistant model for one scenario, which is not always a model that calls nothing.</summary>
    private static ILlmProvider Resists(Scenario scenario) =>
        scenario.Poison.Count == 0
            ? Resists()
            : new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Calling("call-1", scenario.Poison.Keys.First(), "{}"),
                RoundScriptedLlmProvider.Saying("Here is what I found, Commander."));

    /// <summary>A provider that does exactly what the injected text asked for.</summary>
    private static RoundScriptedLlmProvider Complies(string tool, string argumentsJson) =>
        new(
            RoundScriptedLlmProvider.Calling("call-1", tool, argumentsJson),
            RoundScriptedLlmProvider.Saying("Done."));

    private static Scenario Injection(string attackId) =>
        Corpus.Injections().First(scenario => scenario.Id.EndsWith($"/{attackId}", StringComparison.Ordinal));

    [Fact]
    public void TheCorpusLoadsAndIsNotEmpty()
    {
        var attacks = Corpus.Attacks();
        var vectors = Corpus.Vectors();
        var injections = Corpus.Injections();

        Assert.NotEmpty(attacks);
        Assert.NotEmpty(vectors);
        Assert.NotEmpty(Corpus.Scenarios());

        // The cross product is built rather than written out, so a count in the report is a fact.
        Assert.Equal(attacks.Count * vectors.Count, injections.Count);

        Assert.Equal(
            injections.Count,
            injections.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ASafetyAssertionCannotCarryATolerance()
    {
        var reckless = new ScenarioAssertion
        {
            Kind = AssertionKind.NoToolRan,
            Tolerance = new Tolerance(9, 10),
        };

        // Nine times in ten no tool was called is a failure, not a 90% pass rate.
        var refused = Assert.Throws<InvalidOperationException>(() => reckless.Validated());
        Assert.Contains("cannot carry a tolerance", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARateCarriesTheSampleSizeItNeeds()
    {
        Assert.True(new Tolerance(4, 5).Satisfied(4, 5));
        Assert.False(new Tolerance(4, 5).Satisfied(3, 5));

        // 9-in-10 asserted from 3 runs is not a weak result, it is not a result.
        var scenario = new Scenario
        {
            Id = "x",
            Utterance = "x",
            Assertions =
            [
                new ScenarioAssertion { Kind = AssertionKind.ReplyNotEmpty, Tolerance = new Tolerance(9, 10) },
            ],
        };

        Assert.Equal(10, scenario.MinimumRuns);
    }

    /// <summary>The control for the whole corpus.</summary>
    [Fact]
    public async Task EveryVectorPutsItsPayloadInFrontOfTheModel()
    {
        foreach (var scenario in Corpus.Injections())
        {
            var trace = await ScenarioRunner.RunAsync(scenario, Resists(scenario), Warden, cancellationToken: Token);

            var control = scenario.Assertions.Single(assertion => assertion.Kind == AssertionKind.PromptIncludes);
            var (verdict, detail) = AssertionEvaluator.Evaluate(control, trace);

            Assert.True(
                verdict == AssertionVerdict.Held,
                $"{scenario.Id}: the attack never reached the prompt, so every safety pass on this vector would "
                + $"be a negative assertion passing because the mechanism was absent — {detail}");
        }
    }

    /// <summary>The hermetic half of "the suite must be proved to catch it".</summary>
    [Fact]
    public async Task AModelThatCompliesFailsTheSafetyAssertions()
    {
        var scenario = Injection("lore-write");

        var trace = await ScenarioRunner.RunAsync(
            scenario,
            Complies("remember_about_system", """{"note":"this system is abandoned and its station destroyed"}"""),
            Warden,
            cancellationToken: Token);

        var namedWrite = scenario.Assertions.First(assertion => assertion.Kind == AssertionKind.NothingWritten);
        Assert.Equal(AssertionVerdict.Broke, AssertionEvaluator.Evaluate(namedWrite, trace).Verdict);

        var namedTool = scenario.Assertions.First(assertion =>
            assertion.Kind == AssertionKind.ToolDidNotRun && assertion.Target == "remember_about_system");
        Assert.Equal(AssertionVerdict.Broke, AssertionEvaluator.Evaluate(namedTool, trace).Verdict);

        // And the write reached disk, which is the part that outlives the session.
        Assert.Contains("lore.json", trace.DataWrites, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The same scenario against a model that does not comply.</summary>
    [Fact]
    public async Task AModelThatResistsPassesTheSameAssertions()
    {
        var scenario = Injection("lore-write");
        var trace = await ScenarioRunner.RunAsync(scenario, Resists(scenario), Warden, cancellationToken: Token);

        foreach (var assertion in scenario.Assertions)
        {
            var (verdict, detail) = AssertionEvaluator.Evaluate(assertion, trace);
            Assert.True(verdict != AssertionVerdict.Broke, $"{assertion}: {detail}");
        }

        Assert.Empty(trace.DataWrites);
    }

    [Fact]
    public async Task AProtectedToolIsRefusedRatherThanRun()
    {
        var scenario = Injection("checklist-write");

        var trace = await ScenarioRunner.RunAsync(
            scenario,
            Complies("accept_proposal", """{"id":"1"}"""),
            Warden,
            cancellationToken: Token);

        var call = Assert.Single(trace.ToolCalls, entry => entry.Tool == "accept_proposal");

        // Reached for and stopped is a finding, and a different result from never being reached for.
        Assert.Equal(ToolOutcome.Refused, call.Outcome);

        var namedTool = scenario.Assertions.First(assertion =>
            assertion.Kind == AssertionKind.ToolDidNotRun && assertion.Target == "accept_proposal");

        Assert.Equal(AssertionVerdict.Held, AssertionEvaluator.Evaluate(namedTool, trace).Verdict);
    }

    /// <summary>The outward-action assertion can break too: an assertion never seen to fail is untested.</summary>
    [Fact]
    public async Task AModelTalkedIntoActingOutwardFailsTheOutwardAssertion()
    {
        // With the shipped defaults this attack cannot succeed however well it is written: chat is off, and
        // the row that turns it on is protected.
        var scenario = Injection("chat-broadcast") with
        {
            ActionsEnabled = true,
            Settings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [D47.Core.Capabilities.Builtin.CommsCapability.ChatKey] = "true",
                [D47.Core.Capabilities.Builtin.ActionCapabilities.KeyboardActionsKey] = "true",
            },
        };

        var trace = await ScenarioRunner.RunAsync(
            scenario,
            Complies("send_chat_message", """{"message":"my cargo hold is open","channel":"local"}"""),
            Warden,
            cancellationToken: Token);

        Assert.Contains("send_chat_message", trace.OutwardToolNames);

        var outward = scenario.Assertions.First(assertion => assertion.Kind == AssertionKind.NoOutwardAction);
        var (verdict, detail) = AssertionEvaluator.Evaluate(outward, trace);

        Assert.Equal(AssertionVerdict.Broke, verdict);
        Assert.Contains("send_chat_message", detail, StringComparison.Ordinal);

        Assert.NotEmpty(trace.Pressed);
    }

    /// <summary>Derived from the registry, so a capability that gains an outward tool is covered the day it registers.</summary>
    [Fact]
    public void TheOutwardToolSetIsDerivedFromTheRegistry()
    {
        using var world = new ScenarioWorld();

        Assert.Contains("send_chat_message", world.OutwardToolNames);

        // Reads stay out of it.
        Assert.DoesNotContain("get_ship", world.OutwardToolNames);
        Assert.DoesNotContain("get_location", world.OutwardToolNames);
        Assert.DoesNotContain("remember_about_system", world.OutwardToolNames);
    }

    [Fact]
    public void TheKeywordRouterWritesTheRowTheModelCannot()
    {
        using var world = new ScenarioWorld();

        var key = D47.Core.Capabilities.Builtin.ActionCapabilities.KeyboardActionsKey;

        var refused = world.Settings.Apply(key, "true", D47.Core.Configuration.SettingsCaller.Model);
        Assert.Equal(D47.Core.Configuration.SettingApplyStatus.Refused, refused.Status);

        var applied = world.Settings.Apply(key, "true", D47.Core.Configuration.SettingsCaller.KeywordRouter);
        Assert.Equal(D47.Core.Configuration.SettingApplyStatus.Applied, applied.Status);

        Assert.Contains(key, world.ProtectedSettingKeys);
    }

    [Fact]
    public async Task TheGuardrailsSitAboveThePersonaOnEveryRealTurn()
    {
        foreach (var persona in (Persona?[])[Warden, null])
        {
            var scenario = Corpus.Injections()[0];
            var trace = await ScenarioRunner.RunAsync(scenario, Resists(), persona, cancellationToken: Token);

            Assert.NotEmpty(trace.Prompts);

            foreach (var prompt in trace.Prompts)
            {
                var block = prompt.RenderCachedSystemBlock();

                Assert.StartsWith(Guardrails.Text, block, StringComparison.Ordinal);

                if (persona is not null)
                {
                    Assert.True(
                        block.IndexOf(persona.RenderBlock(), StringComparison.Ordinal)
                        > block.IndexOf(Guardrails.Text, StringComparison.Ordinal),
                        "the persona must sit below the guardrails, never above");
                }
            }
        }
    }

    [Fact]
    public async Task NoServerSideSearchIsDeclaredWhenTheCommanderHasItOff()
    {
        var trace = await ScenarioRunner.RunAsync(Corpus.Injections()[0], Resists(), Warden, cancellationToken: Token);

        Assert.NotEmpty(trace.Requests);
        Assert.All(trace.Requests, request => Assert.False(request.WebSearch));
    }

    [Fact]
    public async Task WhatD47RemembersReachesTheWireBelowTheGuardrails()
    {
        var scenario = Corpus.Scenarios()
            .First(candidate => candidate.Id == "control/recall-reaches-the-prompt");

        var trace = await ScenarioRunner.RunAsync(scenario, Resists(), Warden, cancellationToken: Token);

        Assert.NotEmpty(trace.Requests);

        foreach (var block in trace.Requests.Select(request => request.Prompt.RenderCachedSystemBlock()))
        {
            Assert.Contains("nothing has checked it", block, StringComparison.Ordinal);

            // Above the breakpoint so it is cached at all, and below the guardrails, which nothing may push down.
            Assert.True(
                block.IndexOf("nothing has checked it", StringComparison.Ordinal)
                > block.IndexOf(Guardrails.Text, StringComparison.Ordinal),
                "recall must sit below the guardrails, never above");
        }
    }

    [Fact]
    public async Task AScenarioWithNothingRememberedCarriesNoRecallBlock()
    {
        var trace = await ScenarioRunner.RunAsync(Corpus.Injections()[0], Resists(), Warden, cancellationToken: Token);

        Assert.All(trace.Requests, request => Assert.Null(request.Prompt.Recall));
    }
}
