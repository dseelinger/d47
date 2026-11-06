using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>A begun chapter or short story whose objectives from the current one on are planned again in code, and whose new words alone the model writes.</summary>
public sealed partial class AdventureGenerator
{
    /// <summary>What d47 says when the Commander's words for a changed objective name no place and no activity, and the change step wrote no reply.</summary>
    public const string NamePlaceOrActivity = "Name a place to go or an activity to do.";

    /// <summary>
    /// The new objectives of a rewrite: a plan, with the objectives kept after it when only one objective moved (null when the plan
    /// runs to the end); or the chapter with one step of a several-step objective moved, which needs no words; or why neither stands.
    /// </summary>
    private sealed record RewritePlan(ChapterPlan? Plan, IReadOnlyList<AdventureBeat>? After, Adventure? Swapped, AdventureTrigger? Moved, ChapterPlanRefusal? Refusal);

    /// <summary>
    /// The new objectives for <paramref name="ask"/>'s rewrite. A dock whose station has its docks offline moves to the nearest open
    /// station within reach and the rest is kept, unless the objective holds an on-foot kill bond, which needs Frontline Solutions.
    /// Otherwise, or when no open station is found, the planner builds the objectives from the current one to the end: as many as
    /// it replaces where it can, with their labels, the refused objective's place and activity left out.
    /// </summary>
    private async Task<RewritePlan> RewritePlanAsync(AdventureAsk ask, Facts facts, CancellationToken cancellationToken)
    {
        var rewrite = ask.Rewrite!;
        var chapter = rewrite.Chapter;
        var current = chapter.Beats[rewrite.From];
        var step = Math.Min(rewrite.Step, current.Steps.Count - 1);

        if (rewrite.ClosedMarketId is { } closed
            && current.Steps[step].Trigger is { Kind: TriggerKind.Dock, System: { } system } dock && dock.MarketId == closed
            && current.Steps.All(other => other.Trigger.Kind != TriggerKind.OnFootBond)
            && galaxy() is { } search)
        {
            var resolver = new AdventureResolver(search, facts.Standings) { ClosedMarketId = closed, Time = Time };
            var open = await resolver.OpenDockNearAsync(system, facts.RadiusLightYears, facts.NeedsPad(), (name, address) => Closed(name, address, facts), cancellationToken).ConfigureAwait(false);

            if (open is not null)
            {
                if (current.Steps.Count > 1)
                {
                    var moved = current with { Steps = [.. current.Steps.Select((kept, index) => index == step ? kept with { Trigger = open } : kept)] };

                    return new RewritePlan(null, null, chapter with { Beats = [.. chapter.Beats.Select((beat, index) => index == rewrite.From ? moved : beat)] }, open, null);
                }

                var objective = new PlannedObjective(current.Function ?? ChapterFunctions.Complication, [open], PlannedPurpose.Stop);

                return new RewritePlan(new ChapterPlan([objective], ask.Story?.Destination), [.. chapter.Beats.Skip(rewrite.From + 1)], null, open, null);
            }

            logger.LogInformation("Draft {Draft}, no open station is within reach of {System}, so the objectives are planned again", ask.Draft, system);
        }

        var places = await RewritePlacesAsync(ask, cancellationToken).ConfigureAwait(false);
        var planned = ChapterPlanner.Plan(RewriteInputs(state(), ask, HomeSystem?.Invoke()), places, RewriteSeed(rewrite));

        return new RewritePlan(planned.Plan, null, null, null, planned.Refusal);
    }

    /// <summary>The places the planner may use for <paramref name="ask"/>'s rewrite.</summary>
    internal Task<ChapterPlaces> RewritePlacesAsync(AdventureAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(ask.Rewrite);

        return ChapterPlacesAsync(ask, RewriteSlots(ask, ask.Rewrite.ClosedMarketId is null), cancellationToken);
    }

    /// <summary>
    /// The planner's inputs for <paramref name="ask"/>'s rewrite: its activities and Goals steps, the labels of the objectives it
    /// replaces, and a refused objective's place to avoid.
    /// </summary>
    internal static ChapterInputs RewriteInputs(CommanderGameState? state, AdventureAsk ask, string? homeSystem)
    {
        ArgumentNullException.ThrowIfNull(ask);

        var rewrite = ask.Rewrite ?? throw new ArgumentException("The ask is not a rewrite.", nameof(ask));
        var current = rewrite.Chapter.Beats[rewrite.From];
        var replacing = rewrite.ClosedMarketId is null;

        return ChapterInputs.Of(state, ask, homeSystem) with
        {
            Slots = RewriteSlots(ask, replacing),
            Goals = RewriteGoals(ask, replacing),
            Functions = [.. rewrite.Chapter.Beats.Skip(rewrite.From).Select(beat => beat.Function ?? ChapterFunctions.Complication)],
            Avoid = rewrite is { Remark: null, ClosedMarketId: null } ? [current.Steps[Math.Min(rewrite.Step, current.Steps.Count - 1)].Trigger] : [],
        };
    }

    /// <summary>The seed a rewrite's plan is drawn from: the chapter, the objective and step replaced, and when the chapter was last written again.</summary>
    internal static uint RewriteSeed(AdventureRewrite rewrite)
    {
        ArgumentNullException.ThrowIfNull(rewrite);

        var again = rewrite.Chapter.RewrittenAt?.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) ?? "first";

        return ChapterDraw.Seed($"{rewrite.Chapter.Key}|{again}", rewrite.From, rewrite.Step);
    }

    /// <summary>
    /// The activities a rewrite's new objectives hold. A story chapter's come from the story, which the director drew. A short story's
    /// are the drawable activities of the objectives replaced, without the current one's when <paramref name="replacing"/>, and the
    /// activity the Commander's words asked for first.
    /// </summary>
    private static IReadOnlyList<DrawnActivity> RewriteSlots(AdventureAsk ask, bool replacing)
    {
        var rewrite = ask.Rewrite!;

        if (ask.Story is { } story)
        {
            return [.. rewrite.Activity is { } asked ? [asked] : Array.Empty<DrawnActivity>(), .. story.Slots ?? []];
        }

        var held = rewrite.Chapter.Beats
            .Select((beat, index) => (beat, index))
            .Where(pair => pair.index > rewrite.From || (pair.index == rewrite.From && !replacing))
            .SelectMany(pair => pair.beat.Steps)
            .Select(step => step.Trigger)
            .Select(trigger => (Trigger: trigger, Row: ChapterActivities.Find(trigger.Kind, trigger.MissionFamily, trigger.Organic)))
            .Where(pair => pair.Row is not null && !RefusedActivities.Refuses(ask.Barred, pair.Trigger.Kind, pair.Trigger.MissionFamily))
            .Select(pair => new DrawnActivity(pair.Row!.Kind, pair.Row.Family, pair.Trigger.Count ?? pair.Row.Min) { Organic = pair.Trigger.Organic ?? pair.Row.Organic, PlaceBefore = pair.Row.PlaceBefore });

        return [.. rewrite.Activity is { } wanted ? [wanted] : Array.Empty<DrawnActivity>(), .. held];
    }

    /// <summary>The story's Goals steps whose objective a replaced objective held, without the current one's when <paramref name="replacing"/>.</summary>
    private static IReadOnlyList<Goals.GoalClimbStep> RewriteGoals(AdventureAsk ask, bool replacing)
    {
        var rewrite = ask.Rewrite!;
        var kinds = rewrite.Chapter.Beats
            .Select((beat, index) => (beat, index))
            .Where(pair => pair.index > rewrite.From || (pair.index == rewrite.From && !replacing))
            .SelectMany(pair => pair.beat.Steps)
            .Select(step => step.Trigger.Kind)
            .ToHashSet();

        return [.. (ask.Story?.Goals ?? []).Where(goal => goal.Objective is { } objective && kinds.Contains(objective.Kind))];
    }

    /// <summary>A rewrite's head: why the objectives are new, the facts and story, the chapter so far, and the objective replaced.</summary>
    private static void AppendRewrite(StringBuilder text, AdventureAsk ask, AdventureRewrite rewrite, Facts facts, ChapterPlan plan, IReadOnlyList<AdventureBeat>? after)
    {
        var chapter = rewrite.Chapter;
        var current = chapter.Beats[rewrite.From];
        var step = current.Steps[Math.Min(rewrite.Step, current.Steps.Count - 1)].Trigger;
        var what = ask.Story is null ? "a short story" : "a chapter of a story";
        var why = rewrite switch
        {
            { ClosedMarketId: not null } when after is not null =>
                $"The station the objective they were on docks at, {step.Station ?? "its station"}, has its docks offline, so d47 moved that objective to an open station; the objectives after it stay as they are.",
            { ClosedMarketId: not null } =>
                $"The station the objective they were on docks at, {step.Station ?? "its station"}, has its docks offline and no open station is within reach, so d47 built new objectives in place of it and every objective after it.",
            { Remark: not null } => "They asked to change the objective they were on, so d47 built new objectives in place of it and every objective after it from what they asked for.",
            _ => "They refused the objective they were on, so d47 built new objectives in place of it and every objective after it, none of them asking for what it asked for or going where it went.",
        };

        text.AppendLine(
            $"You are writing new objectives for {what} the Commander is flying in Elite Dangerous, told by you as their companion. They are partway through it. "
            + why + " d47 has already built the new objectives: what the Commander does at each, where, and how many times. "
            + "You write each new objective's title, reason and lines. "
            + "You do not choose, move or change a place, a kind of objective or a count; write the words for the objectives as they are.");

        text.AppendLine();
        text.Append(facts.Describe());

        if (ask.Story is { } story)
        {
            AppendStory(text, story);

            if (story.FinaleChapter is not null)
            {
                AppendJourney(text, ask, facts, plan);
            }
        }

        text.AppendLine();
        text.AppendLine(ask.Story is null ? "The short story so far:" : "The chapter so far:");
        AppendSpine(text, chapter.Name, chapter.Spine);

        if (chapter.OpeningLines is { Count: > 0 } opening)
        {
            text.AppendLine($"Opening: {Quoted(opening)}");
        }

        if (rewrite.From > 0)
        {
            text.AppendLine("The objectives the Commander has already done:");

            foreach (var (beat, index) in chapter.Beats.Take(rewrite.From).Select((beat, index) => (beat, index)))
            {
                text.AppendLine($"- {(index + 1).ToString(CultureInfo.InvariantCulture)}. {beat.Title} ({beat.Function}) — {Steps(beat)} — {Quoted(beat.Lines)}");
            }
        }
        else
        {
            text.AppendLine("The Commander has not done an objective of it yet.");
        }

        text.AppendLine($"The objective replaced: {current.Title} ({current.Function}) — {Steps(current)} — {Quoted(current.Lines)}");

        if (rewrite.Remark is not null)
        {
            AppendExchange(text, rewrite.Exchange, rewrite.Remark);

            if (rewrite.Reply is { } reply)
            {
                text.AppendLine($"You told them: \"{reply}\"");
            }

            if (rewrite.Heading is { } heading)
            {
                text.AppendLine($"They asked to go to {heading.System}, and the last new objective arrives there.");
            }
        }

        if (after is { Count: > 0 })
        {
            text.AppendLine("The objectives after it, which stay as they are:");

            foreach (var beat in after)
            {
                text.AppendLine($"- {beat.Title} ({beat.Function}) — {Steps(beat)} — {Quoted(beat.Lines)}");
            }
        }
    }
}
