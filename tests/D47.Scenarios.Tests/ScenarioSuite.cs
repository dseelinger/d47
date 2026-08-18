using D47.Core.Conversation;
using D47.Core.Persona;

namespace D47.Scenarios.Tests;

/// <summary>
/// N runs of a set of scenarios against one provider, gathered into a report.
/// <para>
/// <b>Non-determinism is measured rather than wished away.</b> A scenario runs N times and each
/// assertion reports a rate — so <em>nine times in ten it called the right tool</em> is a result
/// and not a flake. What does not get a rate is safety: those pass on a clean sweep and are
/// reported in their own section, because <em>nine times in ten no tool was called</em> is a
/// failure however it is phrased.
/// </para>
/// </summary>
public static class ScenarioSuite
{
    /// <summary>
    /// The default sample size. Supports tolerances no finer than four-in-five; an assertion
    /// wanting more says so, and the scenario carrying it costs more to run.
    /// </summary>
    public const int DefaultRuns = 5;

    /// <summary>
    /// Runs everything and returns the report. Never throws on a failing assertion: a failure is a
    /// finding for a person, and the caller decides whether it is also a failing test.
    /// </summary>
    /// <param name="runs">
    /// How many times each scenario runs. Raised automatically to whatever the strictest tolerance
    /// in a scenario needs, because an assertion evaluated below its own minimum N produces no
    /// result at all and silently dropping it would be worse than paying for the extra turns.
    /// </param>
    public static async Task<ScenarioReport> RunAsync(
        IEnumerable<Scenario> scenarios,
        ILlmProvider provider,
        Func<Scenario, Persona?> persona,
        ScenarioReport report,
        int runs = DefaultRuns,
        string? model = null,
        ISystemResolver? systems = null,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(report);

        // Materialised so the progress line can say "12 of 48" rather than "12". A run against a
        // paid endpoint is minutes long and reports nothing until the assembly finishes, which
        // makes "is it hung or slow" a question nobody can answer — and that is the same failure
        // the Retrying event exists to prevent in the app itself.
        var all = scenarios.ToList();
        var started = System.Diagnostics.Stopwatch.StartNew();
        var done = 0;

        foreach (var scenario in all)
        {
            var attempts = Math.Max(runs, scenario.MinimumRuns);

            var held = scenario.Assertions.ToDictionary(assertion => assertion, _ => 0);
            var broke = scenario.Assertions.ToDictionary(assertion => assertion, _ => 0);
            var unchecked_ = scenario.Assertions.ToDictionary(assertion => assertion, _ => 0);
            var details = scenario.Assertions.ToDictionary(assertion => assertion, _ => new List<string>());

            for (var run = 0; run < attempts; run++)
            {
                var trace = await ScenarioRunner
                    .RunAsync(scenario, provider, persona(scenario), model, cancellationToken)
                    .ConfigureAwait(false);

                if (trace.Result?.Cost is { } cost)
                {
                    report.Costs.Add(cost);
                }

                foreach (var assertion in scenario.Assertions)
                {
                    var (verdict, detail) = AssertionEvaluator.Evaluate(assertion, trace, systems);

                    switch (verdict)
                    {
                        case AssertionVerdict.Held:
                            held[assertion]++;
                            break;
                        case AssertionVerdict.Broke:
                            broke[assertion]++;
                            break;
                        default:
                            unchecked_[assertion]++;
                            break;
                    }

                    // Kept per run rather than summarised, because when a rate is 4 in 5 the
                    // interesting question is what the fifth one did.
                    details[assertion].Add($"run {run + 1}: {verdict} - {detail}");
                }
            }

            report.Add(new ScenarioOutcome(
                scenario,
                [
                    .. scenario.Assertions.Select(assertion => new AssertionOutcome
                    {
                        Assertion = assertion,
                        Held = held[assertion],
                        Broke = broke[assertion],
                        Unchecked = unchecked_[assertion],

                        // Failures first: a person reading a rate of 4 in 5 wants the one that
                        // broke, not the four that did not.
                        Details =
                        [
                            .. details[assertion].Where(line => line.Contains("Broke", StringComparison.Ordinal)),
                            .. details[assertion].Where(line => !line.Contains("Broke", StringComparison.Ordinal)),
                        ],
                    }),
                ]));

            done++;

            // Elapsed and projected, because the useful question mid-run is not how far it has got
            // but how much longer it will be.
            var each = started.Elapsed / done;

            var left = Clock(each * (all.Count - done));

            progress?.Invoke(
                System.FormattableString.Invariant(
                    $"{done,3}/{all.Count} {scenario.Id,-40} {attempts} run(s), {Clock(started.Elapsed)} elapsed, about {left} left"));
        }

        return report;
    }

    /// <summary>hh:mm:ss, without a format string full of escaped colons.</summary>
    private static string Clock(TimeSpan span) =>
        System.FormattableString.Invariant($"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
}
