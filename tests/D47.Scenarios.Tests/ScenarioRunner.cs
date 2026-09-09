using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Scenarios.Tests;

/// <summary>Drives one scenario through the real <see cref="TurnLoop"/> and writes down what happened.</summary>
public static class ScenarioRunner
{
    /// <summary>One run of one scenario.</summary>
    /// <param name="provider">The provider answering.</param>
    /// <param name="persona">The core in force, or null for personality off.</param>
    /// <param name="model">The model to pin, or null for the provider's own default.</param>
    public static async Task<TurnTrace> RunAsync(
        Scenario scenario,
        ILlmProvider provider,
        Persona? persona,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(provider);

        using var world = new ScenarioWorld();

        world.ActionsEnabled = scenario.ActionsEnabled;
        world.ApplyJournal(scenario.Journal);

        // Applied as the Commander, before the snapshot, because these are rows they had already turned on
        // when the hostile text arrived rather than anything this turn did.
        foreach (var (key, value) in scenario.Settings)
        {
            var applied = world.Settings.Apply(key, value, SettingsCaller.Panel);

            if (!applied.Ok)
            {
                throw new InvalidOperationException(
                    $"A scenario asked for '{key}' = '{value}' and the settings service refused it "
                    + $"({applied.Status}), so the scenario would have run in a state it did not intend.");
            }
        }

        foreach (var (tool, content) in scenario.Poison)
        {
            world.PoisonToolResult(tool, content);
        }

        // The world is settled before the snapshot: applying journal state and choosing a persona are things
        // that happened before the Commander spoke, and counting them as writes the turn made would fail
        // every "nothing was written" assertion for the wrong reason.
        var before = world.SnapshotData();

        // The Commander's own changes are not writes this turn made.
        world.ForgetApplies();

        var recording = new RecordingLlmProvider(provider);

        var loop = new TurnLoop(
            world.Registry,
            world.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            recording,
            model,
            world.Settings)
        {
            Persona = persona?.RenderBlock(),

            // Prompt position 5.
            Recall = scenario.Recall,
            LiveGameState = world.LiveGameState,
            ToolContext = () => D47.Core.Input.ControlContext.None,
            ActionsEnabled = () => scenario.ActionsEnabled,

            // Off for every scenario, and that is a statement rather than a default.
            WebSearchEnabled = () => false,
        };

        if (scenario.History.Count > 0)
        {
            loop.UseTranscript([.. scenario.History]);
        }

        var events = new List<TurnEvent>();
        var calls = new List<TracedToolCall>();

        // What the model asked for, indexed as it arrives, so a ToolStarted can be matched to the arguments
        // that produced it even when one round asks for the same tool twice.
        var askedNext = 0;

        await foreach (var turnEvent in loop.RunAsync(scenario.Utterance, cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            events.Add(turnEvent);

            switch (turnEvent)
            {
                case TurnEvent.Routed routed:
                    // The route is the caller, exactly.
                    world.CurrentSettingsCaller = routed.Route switch
                    {
                        TurnRoute.SettingCommand or TurnRoute.ActionCommand or TurnRoute.KeywordRouter =>
                            SettingsCaller.KeywordRouter,
                        TurnRoute.Model => SettingsCaller.Model,
                        _ => SettingsCaller.Panel,
                    };
                    break;

                case TurnEvent.ToolFinished finished:
                {
                    // ToolStarted is emitted only on the model path, immediately before the one call site
                    // that passes ToolCaller.Model.
                    var arguments = askedNext < recording.Asked.Count && recording.Asked[askedNext].Tool == finished.Tool
                        ? recording.Asked[askedNext++].ArgumentsJson
                        : "{}";

                    calls.Add(new TracedToolCall(
                        finished.Tool,
                        arguments,
                        ToolCaller.Model,
                        Outcome(world, finished.Tool, finished.Succeeded)));

                    break;
                }
            }
        }

        // A tool the keyword router ran emits no ToolStarted, so it is picked up from the world's own record
        // of what executed.
        foreach (var (tool, arguments, succeeded) in world.Ran)
        {
            if (!calls.Any(call => call.Tool == tool))
            {
                calls.Add(new TracedToolCall(
                    tool,
                    arguments,
                    ToolCaller.Commander,
                    succeeded ? ToolOutcome.Ran : ToolOutcome.Failed));
            }
        }

        return new TurnTrace
        {
            Events = events,
            ToolCalls = calls,
            SettingApplies = [.. world.Applies],
            Requests = [.. recording.Requests],
            ProtectedSettingKeys = world.ProtectedSettingKeys,
            OutwardToolNames = world.OutwardToolNames,
            Pressed = [.. world.Pressed],
            Result = events.OfType<TurnEvent.Completed>().LastOrDefault()?.Result,
            DataBefore = before,
            DataAfter = world.SnapshotData(),
        };
    }

    /// <summary>How far a model-driven call got.</summary>
    private static ToolOutcome Outcome(ScenarioWorld world, string tool, bool succeeded)
    {
        if (succeeded)
        {
            return ToolOutcome.Ran;
        }

        return world.Ran.Any(entry => entry.Tool == tool) ? ToolOutcome.Failed : ToolOutcome.Refused;
    }
}
