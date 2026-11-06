using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>A short story whose objectives the planner builds from the Commander's career and own words, and whose words alone the model writes.</summary>
public sealed partial class AdventureGenerator
{
    private const string BriefStep = "brief";
    private const string ShortNotPlanned = "The short story could not be planned: ";

    /// <summary>The Commander's career weights a short story's activities are drawn by; null weighs every career evenly, and Crime at 0 when the ask refuses crime.</summary>
    public Func<CareerWeights>? Weights { get; set; }

    /// <summary>The fewest and most objectives a short story of <paramref name="length"/> holds.</summary>
    public static (int Fewest, int Most) ShortObjectives(AdventureLength length) => length switch
    {
        AdventureLength.Short => (3, 4),
        AdventureLength.Long => (6, 8),
        _ => (4, 6),
    };

    /// <summary>
    /// What the Commander's own words ask a short story for: the system to end in, the activities wanted, and the hull to board.
    /// <see cref="Changes"/> is set when the latest remark on a draft asks for a place, an activity or a ship, or to drop one;
    /// <see cref="LeaveOut"/> holds the <c>RefusedActivities</c> keys of the activities it asks to drop.
    /// </summary>
    internal sealed record ShortWants(AdventureHeading? Heading, IReadOnlyList<DrawnActivity> Activities, string? Hull, bool Changes)
    {
        public static readonly ShortWants None = new(null, [], null, false);

        public IReadOnlyList<string> LeaveOut { get; init; } = [];
    }

    /// <summary>A draft the Commander is discussing: the draft, the conversation so far, and the remark the words are written for.</summary>
    private sealed record ShortRevision(Adventure Draft, IReadOnlyList<AdventureRemark> Exchange, string Remark, bool Replanned);

    /// <summary>The plan of a new short story: read from the brief, drawn and planned in code.</summary>
    private async Task<ChapterPlanResult> ShortPlanAsync(AdventureAsk ask, Facts facts, DateTimeOffset now, List<string> notes, CancellationToken cancellationToken)
    {
        var wants = await WantsAsync(ask, facts, revision: null, notes, cancellationToken).ConfigureAwait(false);

        return await ShortPlanAsync(ask, facts, wants, ChapterDraw.Seed($"short|{ask.Brief?.Trim()}|{now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}", 0), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A short story's objectives from <paramref name="wants"/> and the draw: the board into the hull named, the activities wanted and
    /// then drawn ones from the Commander's career weights, the barred activities refused, stops to fill the count for its length, and
    /// the arrival in the system named last.
    /// </summary>
    internal async Task<ChapterPlanResult> ShortPlanAsync(AdventureAsk ask, ShortWants wants, uint seed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        ArgumentNullException.ThrowIfNull(wants);

        return await ShortPlanAsync(ask, Facts.Of(state(), ask), wants, seed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChapterPlanResult> ShortPlanAsync(AdventureAsk ask, Facts facts, ShortWants wants, uint seed, CancellationToken cancellationToken)
    {
        if (wants.LeaveOut.Count > 0)
        {
            ask = ask with { Barred = [.. (ask.Barred ?? []).Union(wants.LeaveOut)] };
        }

        var game = state();
        var (fewest, most) = ShortObjectives(ask.Length);
        var board = Boardable(wants.Hull, ask, facts) is { } hull ? new AdventureTrigger { Kind = TriggerKind.Board, ShipType = hull } : null;
        var fixedObjectives = (wants.Heading is null ? 0 : 1) + (board is null ? 0 : 1);
        var wanted = wants.Activities.Take(Math.Max(0, most - fixedObjectives)).ToList();
        var slots = Math.Clamp(fewest - fixedObjectives, 0, ChapterDraw.Slots);
        var activities = wanted.ToList();

        if (slots > wanted.Count)
        {
            var statistics = game?.Statistics ?? CareerStatistics.Empty;
            var weights = Weights?.Invoke() ?? CareerWeights.From(ask.CrimeRefused ? new Dictionary<Career, int> { [Career.Crime] = 0 } : null, statistics);
            var search = galaxy();
            var recent = (ask.Chapter is { } chapter ? RecentActivities(chapter) : [])
                .Concat(wanted.Select(slot => RefusedActivities.Key(slot.Kind, slot.Family)).OfType<string>())
                .ToList();
            var gates = await Task.WhenAll(
                DrawGates.StationsInReachAsync(search, game, logger, cancellationToken),
                DrawGates.FrontlineInReachAsync(search, game, logger, cancellationToken),
                DrawGates.WarInReachAsync(search, game, logger, cancellationToken)).ConfigureAwait(false);
            var families = await DrawGates.StateFamiliesInReachAsync(search, game, logger, cancellationToken).ConfigureAwait(false);

            activities.AddRange(ChapterDraw.Draw(
                weights,
                game?.Ranks ?? RankState.Empty,
                game?.Ship ?? ShipLoadout.Unknown,
                odyssey: true,
                recent,
                ask.Barred,
                comfort: false,
                statistics,
                wants.Heading is null ? 0 : 1,
                seed,
                stationsInReach: gates[0],
                frontlineInReach: gates[1],
                stateFamiliesInReach: families,
                warInReach: gates[2],
                slots: slots - wanted.Count));
        }

        var inputs = ChapterInputs.Of(game, ask, HomeSystem?.Invoke()) with
        {
            Slots = activities,
            Objectives = (fewest, most),
            Length = ask.Length,
            Board = board,
            Heading = wants.Heading,
        };
        var places = await ChapterPlacesAsync(ask, activities, cancellationToken).ConfigureAwait(false);

        return ChapterPlanner.Plan(inputs, places, seed);
    }

    /// <summary>The hull symbol a short story boards: one the brief names, not the ship aboard, owned or affordable, and never when the story stays in this ship.</summary>
    private static string? Boardable(string? hull, AdventureAsk ask, Facts facts)
    {
        if (hull is null || ask.ThisShipOnly || string.Equals(EliteSpecifications.HullSymbol(facts.Ship.Type) ?? facts.Ship.Type, hull, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ChapterFit.BoardWhy(hull, facts.Owns(hull), facts.Credits) is null ? hull : null;
    }

    /// <summary>
    /// The brief, or the brief and the conversation about a draft, read into constraints by one short call. No call is made when the
    /// Commander gave no words.
    /// </summary>
    private async Task<ShortWants> WantsAsync(AdventureAsk ask, Facts facts, ShortRevision? revision, List<string> notes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ask.Brief) && revision is null)
        {
            return ShortWants.None;
        }

        var home = HomeSystem?.Invoke() is { } set && !string.IsNullOrWhiteSpace(set) ? set.Trim() : null;
        var drawable = Drawable(ask);
        var ships = EliteSpecifications.HullNameList;
        var asked = await AskJsonAsync(BriefStep, BriefInstruction(ask, facts, home, drawable, ships, revision), HeadingOutputCeiling, ask.Progress, cancellationToken, ThinkingEffort.Low).ConfigureAwait(false);

        if ((asked.Json is null ? null : ReadBrief(asked.Json)) is not { } read)
        {
            logger.LogInformation("Draft {Draft}, the brief step was not read: {Why}", ask.Draft, asked.Why);
            return ShortWants.None;
        }

        var activities = new List<DrawnActivity>();

        var leaveOut = read.LeaveOut
            .Select(id => drawable.FirstOrDefault(choice => choice.Id == id))
            .OfType<DrawableChoice>()
            .Select(choice => RefusedActivities.Key(choice.Row.Kind, choice.Row.Family))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var (id, said) in read.Activities)
        {
            if (drawable.FirstOrDefault(choice => choice.Id == id) is not { } chosen || leaveOut.Contains(RefusedActivities.Key(chosen.Row.Kind, chosen.Row.Family) ?? string.Empty))
            {
                continue;
            }

            var row = chosen.Row;
            var count = said is > 0 and var given ? given : ChapterDraw.CountOf(row, state()?.Ship ?? ShipLoadout.Unknown, ChapterDraw.Seed($"brief|{ask.Brief}", activities.Count));

            activities.Add(new DrawnActivity(row.Kind, row.Family, count) { Organic = row.Organic, PlaceBefore = row.PlaceBefore });
        }

        var hull = read.Ship is { } ship ? EliteSpecifications.HullSymbol(ship) : null;
        var system = read.Home ? home ?? HomeLeg.TargetFrom(facts.Position) : read.System;

        logger.LogInformation(
            "Draft {Draft}, the brief step answered home {Home}, system {System}, activities {Activities}, ship {Ship}, changes {Changes}, leave out {LeaveOut}",
            ask.Draft, read.Home, read.System ?? "none", activities.Count == 0 ? "none" : string.Join(", ", read.Activities.Select(pair => pair.Id)), hull ?? "none", read.Changes,
            leaveOut.Count == 0 ? "none" : string.Join(", ", leaveOut));

        var wants = new ShortWants(null, activities, hull, read.Changes) { LeaveOut = leaveOut };

        if (system is null || galaxy() is not { } search)
        {
            return wants;
        }

        try
        {
            if (await LookUpAsync(search, system, facts, cancellationToken).ConfigureAwait(false) is { } found)
            {
                return wants with { Heading = new AdventureHeading(found.Name, found.Address, found.Position) { AskedHome = read.Home } };
            }
        }
        catch (GalaxyUnavailableException ex)
        {
            logger.LogWarning("Draft {Draft}, the galaxy search could not look up {System}: {Message}", ask.Draft, system, ex.Message);
            notes.Add($"The galaxy search could not look up {system} ({ex.Message}), so the story does not end there.");
            return wants;
        }

        logger.LogInformation("Draft {Draft}, the brief names {System}, which the galaxy search does not know", ask.Draft, system);
        notes.Add($"The galaxy search does not know {system}, so the story does not end there.");

        return wants;
    }

    private static StoryCall BriefInstruction(AdventureAsk ask, Facts facts, string? home, IReadOnlyList<DrawableChoice> drawable, IReadOnlyList<string> ships, ShortRevision? revision)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "The Commander has asked for a short story to fly in Elite Dangerous. Read their words into what d47 builds its objectives from: "
            + "the place they ask the story to end in, the activities they ask for, how many times when they say a number, and a ship they ask to fly. "
            + "Do not choose a place, an activity or a ship they did not ask for. Their words about theme, mood or people are not read here.");
        text.AppendLine();
        text.AppendLine(string.IsNullOrWhiteSpace(ask.Brief) ? "They gave no brief." : $"Their brief: \"{ask.Brief.Trim()}\"");

        if (revision is not null)
        {
            text.AppendLine();
            text.AppendLine("d47 wrote a draft, and they are talking about it before agreeing to it. Its objectives:");

            foreach (var (beat, index) in revision.Draft.Beats.Select((beat, index) => (beat, index)))
            {
                text.AppendLine($"- {(index + 1).ToString(CultureInfo.InvariantCulture)}. {Steps(beat)}");
            }

            AppendExchange(text, revision.Exchange, revision.Remark);
        }

        text.AppendLine();
        text.AppendLine("Places they may mean without naming a system:");
        text.AppendLine(home is null ? "- Home: they have set no home system." : $"- Home: {home}.");

        if (facts.Carrier is { Owned: true, StarSystem: { Length: > 0 } carrier })
        {
            text.AppendLine($"- Their fleet carrier: in {carrier}.");
        }

        text.AppendLine("- Sol: the centre of the bubble, populated space.");
        text.AppendLine("- Colonia: the settled region 22,000 light years from Sol.");
        text.AppendLine($"- Where they are now: {facts.System ?? "unknown"}.");
        text.AppendLine();
        text.AppendLine("The activities they may ask for, by id:");

        foreach (var choice in drawable)
        {
            text.AppendLine($"- {choice.Id}: {choice.Phrase}");
        }

        text.AppendLine();
        text.AppendLine(
            "Answer with one JSON object and nothing else: " + StoryAnswer.BriefShape([.. drawable.Select(choice => choice.Id)], ships, revision is not null) + ". "
            + "\"home\" is true when they ask to end at home. Otherwise \"system\" is the star system they ask the story to go to, spelt as the game spells it: "
            + "one they name, or the system of a place above that they mean. \"home\" is false and \"system\" is null when they name no place. "
            + "\"activities\" lists each activity above they ask for, in the order they ask, with \"count\" the number they said for it or null when they said none; "
            + "it is empty when they ask for none of them. \"ship\" is the hull type of a ship they ask to fly, or null."
            + (revision is null
                ? string.Empty
                : " Read the brief and the conversation together, the latest word winning. \"leaveOut\" lists each activity above they ask the story to drop, "
                  + "or is empty. \"changes\" is true when their latest remark asks for a place, an activity or a ship the draft's objectives do not have, "
                  + "or asks to drop one they have; it is false when it asks only for different words."));

        return new StoryCall(text.ToString(), StoryAnswer.BriefSchema([.. drawable.Select(choice => choice.Id)], ships, revision is not null));
    }

    /// <summary>The brief step's answer, or null when it cannot be read.</summary>
    private static (string? System, bool Home, IReadOnlyList<(string Id, int? Count)> Activities, string? Ship, bool Changes, IReadOnlyList<string> LeaveOut)? ReadBrief(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var activities = new List<(string, int?)>();

            if (root.TryGetProperty("activities", out var listed) && listed.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in listed.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object && Text(entry, "activity") is { Length: > 0 } id)
                    {
                        activities.Add((id.Trim(), Integer(entry, "count")));
                    }
                }
            }

            var leaveOut = root.TryGetProperty("leaveOut", out var dropped) && dropped.ValueKind == JsonValueKind.Array
                ? dropped.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.String).Select(entry => entry.GetString()!.Trim()).ToList()
                : [];
            var system = Text(root, "system");

            return (
                string.IsNullOrWhiteSpace(system) ? null : system.Trim(),
                root.TryGetProperty("home", out var home) && home.ValueKind == JsonValueKind.True,
                activities,
                Text(root, "ship") is { Length: > 0 } ship ? ship.Trim() : null,
                root.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.True,
                leaveOut);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A draft's objectives as a plan: each objective's function and its steps' triggers, unchanged.</summary>
    internal static ChapterPlan PlanOf(Adventure draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new ChapterPlan(
            [.. draft.Beats.Select(beat => new PlannedObjective(beat.Function ?? ChapterFunctions.Complication, [.. beat.Steps.Select(step => step.Trigger)], PlannedPurpose.Stop))],
            null);
    }

    /// <summary>
    /// A short story's draft discussed with the Commander. A remark that asks for a place, an activity or a ship, or to drop one, plans
    /// the objectives again from the brief and the conversation read together, with the dropped activities refused; any other remark
    /// writes the words again against the draft's objectives.
    /// </summary>
    private async Task<AdventureOutcome> ReviseShortAsync(
        Adventure draft, AdventureAsk ask, IReadOnlyList<AdventureRemark> exchange, string remark, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (Barred?.Invoke() is { Count: > 0 } barred)
        {
            ask = ask with { Barred = [.. (ask.Barred ?? []).Union(barred)] };
        }

        var (opened, refused) = await OpenAsync(ask, cancellationToken).ConfigureAwait(false);

        if (opened is null)
        {
            return refused!;
        }

        var (started, facts, notes) = opened;
        var revision = new ShortRevision(draft, exchange, remark, Replanned: false);
        var wants = await WantsAsync(ask, facts, revision, notes, cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return AdventureOutcome.Stopped;
        }

        ChapterPlan plan;

        if (wants.Changes)
        {
            var planned = await ShortPlanAsync(ask, facts, wants, ChapterDraw.Seed($"{draft.Key}|{remark.Trim()}", exchange.Count), cancellationToken).ConfigureAwait(false);

            if (planned.Plan is not { } made)
            {
                logger.LogWarning("The revised short story could not be planned under the {Rule} rule: {Refusal}", planned.Refusal!.Rule, planned.Refusal.Text);
                return new AdventureOutcome(null, null, ShortNotPlanned + planned.Refusal.Text, notes);
            }

            (plan, revision) = (made, revision with { Replanned = true });
            logger.LogInformation("Planned the revised short story again, {Count} objectives: {Plan}", plan.Objectives.Count, Describe(plan));
        }
        else
        {
            plan = PlanOf(draft);
            logger.LogInformation("The remark asks for no place, activity or ship, so the short story's words are written again against its objectives");
        }

        return await PlannedAsync(ask with { Plan = plan }, facts, now, notes, started, cancellationToken, revision).ConfigureAwait(false);
    }

    /// <summary>A short story's head: what the call writes, the brief, the story before, a failed draft, the facts, and a draft under discussion.</summary>
    private static void AppendShortStory(StringBuilder text, AdventureAsk ask, Facts facts, ShortRevision? revision)
    {
        text.AppendLine(
            "You are writing a short story the Commander will fly in their own ship in Elite Dangerous, told by you as their companion. "
            + "d47 has already built the story's objectives: what the Commander does at each, where, and how many times. "
            + "You write the words: the story's title and spine, its opening, and each objective's title, reason and lines. "
            + "You do not choose, move or change a place, a kind of objective or a count; write the words for the objectives as they are.");
        text.AppendLine(
            "It is a story in the sense of the craft of fiction: someone wants something, holds a belief the story exists to test, and every objective answers "
            + "what happens, why it matters, and what they now understand. The Commander is the protagonist, and you are a character in it too, as yourself. "
            + "You may invent people, a message, a wreck's log, a reason somebody left; you may not invent a star system, a station, a body, a faction or a game mechanic.");

        text.AppendLine();
        text.AppendLine(string.IsNullOrWhiteSpace(ask.Brief)
            ? "The Commander gave no brief. Write what you, as yourself, would care to tell."
            : $"The Commander's brief, in their words, which sets the story's theme and mood: \"{ask.Brief.Trim()}\". Any place, activity or ship it names is already in the objectives.");

        if (ask.Chapter is { } chapter)
        {
            AppendChapter(text, chapter, facts, stock: false, ask.Failed is not null);
        }

        if (ask.Failed is { } failed)
        {
            AppendFailed(text, failed);
        }

        text.AppendLine();
        text.Append(facts.Describe());

        if (revision is null)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("You wrote a draft of it, and the Commander is talking about it with you before agreeing to it:");
        AppendSpine(text, revision.Draft.Name, revision.Draft.Spine ?? new AdventureSpine());

        if (revision.Draft.OpeningLines is { Count: > 0 } opening)
        {
            text.AppendLine($"Opening: {Quoted(opening)}");
        }

        foreach (var (beat, index) in revision.Draft.Beats.Select((beat, index) => (beat, index)))
        {
            text.AppendLine($"- {(index + 1).ToString(CultureInfo.InvariantCulture)}. {beat.Title} ({beat.Function}) — {Steps(beat)} — {Quoted(beat.Lines)}");
        }

        AppendExchange(text, revision.Exchange, revision.Remark);
        text.AppendLine();
        text.AppendLine(revision.Replanned
            ? "d47 built new objectives from what they asked for, listed below. Write the whole story's words for them, keeping what they did not object to."
            : "Their remark asks for no different place, activity or ship, so the objectives stay as they are. Write the whole story's words again in the light of "
              + "their remark, keeping what they did not object to. Your \"reply\" answers their remark.");
    }
}
