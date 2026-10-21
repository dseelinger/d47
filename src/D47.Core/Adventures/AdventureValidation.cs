using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>The career ladders a rank beat may name, and how each is said.</summary>
public static class Careers
{
    /// <summary>The journal's own keys, which are what a <c>Promotion</c> event carries.</summary>
    public static IReadOnlyList<string> Keys => RankState.Careers;

    /// <summary>The journal's key for a spoken or typed career word, or null if it is not one.</summary>
    public static string? Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var wanted = text.Trim().TrimEnd('.');

        return Keys.FirstOrDefault(key =>
                   string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Word(key), wanted, StringComparison.OrdinalIgnoreCase))
               ?? (Aliases.TryGetValue(wanted, out var alias) ? alias : null);
    }

    /// <summary>
    /// The other words a person or a model uses for a ladder — "Trader" for the Trade career,
    /// "Explorer" for Exploration.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trader"] = "Trade",
        ["Trading"] = "Trade",
        ["Merchant"] = "Trade",
        ["Explorer"] = "Explore",
        ["Exploring"] = "Explore",
        ["Fighter"] = "Combat",
        ["Merc"] = "Soldier",
        ["Mercenaries"] = "Soldier",
        ["Xenobiology"] = "Exobiologist",
        ["Xenobiologist"] = "Exobiologist",
    };

    /// <summary>How a Commander says the career.</summary>
    public static string Word(string? career) => career switch
    {
        "Explore" => "Exploration",
        "Soldier" => "Mercenary",
        "Exobiologist" => "Exobiology",
        null => "an unknown career",
        _ => career,
    };
}

/// <summary>
/// What the file and the form both check, so a hand-edited adventure and a form-built one are refused
/// identically (Phase 47, "The trigger vocabulary is closed and the prose is free").
/// </summary>
public static class AdventureValidation
{
    /// <summary>Every kind, in the words the file uses.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
        ["arrive", "dock", "land", "scan", "rank", "board", "beacon", "bounty", "bond", "mission", "sell", "mine",
         "onfoot", "collect", "organic", "map", "signal", "wreck", "codex", "datasale", "salvage", "uss", "rescue", "engineer", "srv", "crew", "suitmod", "livery",
         "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound", "conflict", "faction", "reach", "boost", "discover", "footfall", "contribute", "claim", "unlock", "pledge", "powerrank", "site"];

    /// <summary>The stars a boost beat may be limited to.</summary>
    public static IReadOnlyList<string> CodexFilters { get; } = ["Biology", "StellarBodies", "Civilisations", "Guardian"];

    public static IReadOnlyList<string> BoostStars { get; } = ["neutron", "whitedwarf"];

    /// <summary>The body classes a discover beat may be limited to; <c>N</c> is a neutron star.</summary>
    public static IReadOnlyList<string> DiscoverBodies { get; } =
        ["Earthlike body", "Water world", "Ammonia world", "Gas giant with water based life", "Gas giant with ammonia based life", "N"];

    /// <summary>The war types a conflict beat may be limited to, as the journal spells them.</summary>
    public static IReadOnlyList<string> WarTypes { get; } = ["war", "civilwar", "election"];

    public static bool TryKind(string? text, out TriggerKind kind)
    {
        kind = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Trim().All(char.IsLetter) && Enum.TryParse(text.Trim(), ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }

    /// <summary>What stops this being stored or begun, each naming where and why.</summary>
    public static IReadOnlyList<string> Problems(Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(adventure.Key))
        {
            problems.Add("A story needs a key.");
        }

        if (string.IsNullOrWhiteSpace(adventure.Name))
        {
            problems.Add("A story needs a name.");
        }
        else if (adventure.Name.Trim().Length > AdventureLimits.MaxNameLength)
        {
            problems.Add(
                $"The name is {adventure.Name.Trim().Length} characters; at most {AdventureLimits.MaxNameLength}.");
        }

        if (Written(adventure.OpeningLines) > AdventureLimits.MaxLinesPerBeat)
        {
            problems.Add($"The opening has {Written(adventure.OpeningLines)} lines; at most {AdventureLimits.MaxLinesPerBeat}.");
        }

        foreach (var opening in adventure.OpeningLines.Where(line => line.Text.Trim().Length > AdventureLimits.MaxLineLength))
        {
            problems.Add($"The opening's line is {opening.Text.Trim().Length} characters; at most {AdventureLimits.MaxLineLength}.");
        }

        if (adventure.Spine is { } spine)
        {
            foreach (var (field, text) in new[]
                     {
                         ("premise", spine.Premise), ("want", spine.Want), ("stake", spine.Stake),
                         ("turn", spine.Turn), ("ending", spine.Ending),
                     })
            {
                if (text is { } value && value.Trim().Length > AdventureLimits.MaxSpineLength)
                {
                    problems.Add($"The {field} is {value.Trim().Length} characters; at most {AdventureLimits.MaxSpineLength}.");
                }
            }
        }

        if (adventure.Beats.Count == 0)
        {
            problems.Add("A story needs at least one beat.");
        }
        else if (adventure.Beats.Count > AdventureLimits.MaxBeats)
        {
            problems.Add($"{adventure.Beats.Count} beats; at most {AdventureLimits.MaxBeats}.");
        }

        problems.AddRange(ScansOutOfOrder(adventure.Beats));

        for (var index = 0; index < adventure.Beats.Count; index++)
        {
            var beat = adventure.Beats[index];
            var where = Where(index, beat);

            if (string.IsNullOrWhiteSpace(beat.Title))
            {
                problems.Add($"{where} has no title.");
            }
            else if (beat.Title.Trim().Length > AdventureLimits.MaxTitleLength)
            {
                problems.Add($"{where}'s title is {beat.Title.Trim().Length} characters; at most {AdventureLimits.MaxTitleLength}.");
            }

            if (Written(beat.Lines) > AdventureLimits.MaxLinesPerBeat)
            {
                problems.Add($"{where} has {Written(beat.Lines)} lines; at most {AdventureLimits.MaxLinesPerBeat}.");
            }

            if (beat.Lines.Count == 0 || beat.Lines.All(line => string.IsNullOrWhiteSpace(line.Text)))
            {
                problems.Add($"{where} has no line.");
            }
            else
            {
                foreach (var line in beat.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Text))
                    {
                        problems.Add($"{where} has an empty line.");
                    }
                    else if (line.Text.Trim().Length > AdventureLimits.MaxLineLength)
                    {
                        problems.Add($"{where}'s line is {line.Text.Trim().Length} characters; at most {AdventureLimits.MaxLineLength}.");
                    }
                }
            }

            if (beat.Steps.Count == 0)
            {
                problems.Add($"{where} has no step.");
            }

            foreach (var (step, stepIndex) in beat.Steps.Select((step, stepIndex) => (step, stepIndex)))
            {
                var stepWhere = beat.Steps.Count > 1 ? $"{where}, step {(stepIndex + 1).ToString(CultureInfo.InvariantCulture)}" : where;

                foreach (var fail in step.FailsOn)
                {
                    problems.AddRange(FailsOnProblems(stepWhere, fail));
                }

                if (!Enum.IsDefined(step.Trigger.Kind))
                {
                    problems.Add($"{stepWhere} names a trigger that is not one of {string.Join(", ", Kinds)}.");
                    continue;
                }

                if (step.Trigger.IsCounted)
                {
                    problems.AddRange(CountedProblems(stepWhere, step.Trigger));
                    continue;
                }

                switch (step.Trigger.Kind)
                {
                    case TriggerKind.Rank:
                        if (Careers.Match(step.Trigger.Career) is null)
                        {
                            problems.Add(
                                $"{stepWhere} names a career \"{step.Trigger.Career ?? string.Empty}\"; the careers are "
                                + string.Join(", ", Careers.Keys.Select(Careers.Word)) + ".");
                        }

                        if (step.Trigger.Rank is not (>= 1 and <= RankStanding.Elite))
                        {
                            problems.Add($"{stepWhere} names rank {step.Trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; ranks run 1 to {RankStanding.Elite}.");
                        }

                        break;

                    case TriggerKind.Board:
                        if (string.IsNullOrWhiteSpace(step.Trigger.ShipType))
                        {
                            problems.Add($"{stepWhere} boards no ship: it names no ship type.");
                        }
                        else if (EliteSpecifications.HullName(step.Trigger.ShipType) is null)
                        {
                            problems.Add($"{stepWhere} names a ship \"{step.Trigger.ShipType.Trim()}\" that d47 has no name for.");
                        }

                        break;

                    case TriggerKind.Arrive:
                        if (step.Trigger.SystemAddress is null && string.IsNullOrWhiteSpace(step.Trigger.System))
                        {
                            problems.Add($"{stepWhere} arrives nowhere: it names no system.");
                        }

                        break;

                    case TriggerKind.Dock:
                        if (step.Trigger.MarketId is null && string.IsNullOrWhiteSpace(step.Trigger.Station))
                        {
                            problems.Add($"{stepWhere} docks nowhere: it names no station.");
                        }

                        break;

                    case TriggerKind.Land:
                    case TriggerKind.Scan:
                        if ((step.Trigger.SystemAddress is null || step.Trigger.BodyId is null)
                            && string.IsNullOrWhiteSpace(step.Trigger.Body))
                        {
                            problems.Add($"{stepWhere} names no body.");
                        }

                        break;

                    case TriggerKind.Beacon:
                        if (step.Trigger.SystemAddress is not { } beacon || !GuardianCores.Beacons.ContainsKey(beacon))
                        {
                            problems.Add($"{stepWhere} waits for a Guardian beacon scan in a system with no Guardian beacon.");
                        }

                        break;

                    case TriggerKind.Engineer:
                        problems.AddRange(EngineerProblems(stepWhere, step.Trigger));
                        break;

                    case TriggerKind.Unlock:
                        problems.AddRange(UnlockProblems(stepWhere, step.Trigger, null));
                        break;

                    case TriggerKind.Pledge:
                        problems.AddRange(PledgeProblems(stepWhere, step.Trigger));
                        break;

                    case TriggerKind.PowerRank:
                        problems.AddRange(PowerRankProblems(stepWhere, step.Trigger));
                        break;

                    case TriggerKind.Reach:
                        if (step.Trigger.JumpRange is not > 0)
                        {
                            problems.Add($"{stepWhere} waits for a ship that can reach the beacon but names no jump range.");
                        }

                        break;
                }
            }
        }

        return problems;
    }

    /// <summary>What is wrong with a <c>failsOn</c> trigger: a count, the conflict kind, or a filter its kind does not take.</summary>
    public static IEnumerable<string> FailsOnProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (!Enum.IsDefined(trigger.Kind))
        {
            yield return $"{where} has a failsOn that is not one of {string.Join(", ", Kinds)}.";
            yield break;
        }

        if (trigger.Count is not null)
        {
            yield return $"{where} has a failsOn with a count; the first matching event fails the chapter.";
        }

        if (trigger.Kind == TriggerKind.Conflict)
        {
            yield return $"{where} has a failsOn of the conflict kind, which cannot fail a chapter.";
            yield break;
        }

        if (trigger.IsCounted)
        {
            foreach (var problem in CountedProblems(where, FailTrigger(trigger)))
            {
                yield return problem;
            }
        }
    }

    /// <summary>A <c>failsOn</c> trigger as the fold evaluates it: a counted kind needs a count to be resolved, and one event is all it takes.</summary>
    public static AdventureTrigger FailTrigger(AdventureTrigger trigger) =>
        trigger.IsCounted ? trigger with { Count = 1 } : trigger;

    /// <summary>What is wrong with a counted trigger: a count under one, or a mission family that is not one or is set aside.</summary>
    public static IEnumerable<string> CountedProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.Count is not >= 1)
        {
            yield return $"{where} counts to {trigger.Count?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; a counted objective needs a count of 1 or more.";
        }

        if (trigger.Kind == TriggerKind.Faction && string.IsNullOrWhiteSpace(trigger.Faction))
        {
            yield return $"{where} works for no faction: it names none.";
        }

        if (trigger.Kind == TriggerKind.Conflict
            && !string.IsNullOrWhiteSpace(trigger.Filter)
            && !WarTypes.Contains(trigger.Filter.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            yield return $"{where} names the war type \"{trigger.Filter.Trim()}\"; the war types are {string.Join(", ", WarTypes)}.";
        }

        if (trigger.Kind == TriggerKind.Boost
            && !string.IsNullOrWhiteSpace(trigger.Filter)
            && !BoostStars.Contains(JournalJson.Symbol(trigger.Filter) ?? string.Empty, StringComparer.Ordinal))
        {
            yield return $"{where} names the star \"{trigger.Filter.Trim()}\"; the stars are {string.Join(", ", BoostStars)}.";
        }

        if (trigger.Kind == TriggerKind.Discover
            && !string.IsNullOrWhiteSpace(trigger.Filter)
            && !DiscoverBodies.Any(body => AdventureWatch.DiscoverKey(body) == AdventureWatch.DiscoverKey(trigger.Filter)))
        {
            yield return $"{where} names the body \"{trigger.Filter.Trim()}\"; the bodies are {string.Join(", ", DiscoverBodies)}.";
        }

        if (trigger.Kind == TriggerKind.Codex
            && !string.IsNullOrWhiteSpace(trigger.Filter)
            && !CodexFilters.Contains((JournalJson.Symbol(trigger.Filter) ?? string.Empty).Split('_')[^1], StringComparer.OrdinalIgnoreCase))
        {
            yield return $"{where} names the codex filter \"{trigger.Filter.Trim()}\"; the filters are {string.Join(", ", CodexFilters)}.";
        }

        if (trigger.Kind == TriggerKind.Mission && trigger.MissionFamily is { } family && !string.IsNullOrWhiteSpace(family))
        {
            if (!family.Trim().StartsWith(MissionFamilies.Prefix, StringComparison.Ordinal))
            {
                yield return $"{where} names a mission family \"{family.Trim()}\"; a family starts with {MissionFamilies.Prefix}, such as Mission_Courier.";
            }
            else if (MissionFamilies.IsSetAside(family.Trim()))
            {
                yield return $"{where} names the mission family {family.Trim()}, which is set aside; no objective uses "
                    + string.Join(", ", MissionFamilies.SetAside.Select(entry => entry.Family)) + ".";
            }
        }
    }

    /// <summary>What is wrong with an engineer trigger: no engineer, an engineer the directory does not know, or a stage that is not <c>Invited</c> or <c>Unlocked</c>.</summary>
    public static IEnumerable<string> EngineerProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (string.IsNullOrWhiteSpace(trigger.Engineer))
        {
            yield return $"{where} names no engineer.";
        }
        else if (EngineerDirectory.ByName(trigger.Engineer) is null)
        {
            yield return $"{where} names the engineer \"{trigger.Engineer.Trim()}\"; the engineers are "
                + string.Join(", ", EngineerDirectory.All.Select(engineer => engineer.Name)) + ".";
        }

        if (EngineerStages.Rank(trigger.Stage) == 0)
        {
            yield return $"{where} names the stage \"{trigger.Stage?.Trim() ?? string.Empty}\"; the stages are {string.Join(", ", EngineerStages.Named)}.";
        }
    }

    /// <summary>What is wrong with a pledge trigger: a Power that is not one of the twelve.</summary>
    public static IEnumerable<string> PledgeProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (!string.IsNullOrWhiteSpace(trigger.Filter) && PowerNamed(trigger.Filter) is null)
        {
            yield return $"{where} names the Power \"{trigger.Filter.Trim()}\", which is not one of the twelve Powers: {string.Join(", ", GalaxyFilters.Powers)}.";
        }
    }

    /// <summary>What is wrong with a power rank trigger: a rank under one.</summary>
    public static IEnumerable<string> PowerRankProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.Rank is not > 0)
        {
            yield return $"{where} waits for a PowerPlay rank of {trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; the rank must be 1 or more.";
        }
    }

    /// <summary>The Power spelled as the galaxy search spells it, or null when <paramref name="name"/> is not one of the twelve.</summary>
    public static string? PowerNamed(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : GalaxyFilters.Powers.FirstOrDefault(power => string.Equals(power, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>What is wrong with an unlock trigger: a name that is not a joined tech broker row, or a row <paramref name="unlocks"/> shows as unlocked.</summary>
    public static IEnumerable<string> UnlockProblems(string where, AdventureTrigger trigger, TechBrokerUnlocks? unlocks)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (string.IsNullOrWhiteSpace(trigger.Filter))
        {
            yield return $"{where} unlocks nothing: it names no tech broker item.";
        }
        else if (TechBrokerCatalogue.ForName(trigger.Filter) is not { } row)
        {
            yield return $"{where} names the tech broker item \"{trigger.Filter.Trim()}\", which is not one a tech broker unlocks that d47 can recognise.";
        }
        else if (unlocks?.IsKnownUnlocked(row) == true)
        {
            yield return $"{where} unlocks the {row.Name}, which the Commander has already unlocked.";
        }
    }

    /// <summary>
    /// Why Begin is shut, once <see cref="Problems"/> is empty: every beat whose place has a name and
    /// no id yet.
    /// </summary>
    public static IReadOnlyList<string> NotReady(Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        var reasons = new List<string>();

        for (var index = 0; index < adventure.Beats.Count; index++)
        {
            var beat = adventure.Beats[index];

            foreach (var step in beat.Steps.Where(step => !step.Trigger.IsResolved))
            {
                reasons.Add($"{Where(index, beat)} — {step.Trigger.Describe()} — is not yet a real place d47 can recognise.");
            }
        }

        return reasons;
    }

    /// <summary>A scan beat placed after a landing on, or an earlier scan of, the same body.</summary>
    internal static IEnumerable<string> ScansOutOfOrder(IReadOnlyList<AdventureBeat> beats)
    {
        var steps = beats.SelectMany((beat, index) => beat.Steps.Select(step => (Beat: beat, Index: index, step.Trigger))).ToList();

        for (var index = 0; index < steps.Count; index++)
        {
            var scan = steps[index];

            if (scan.Trigger.Kind != TriggerKind.Scan)
            {
                continue;
            }

            for (var earlier = 0; earlier < index; earlier++)
            {
                var before = steps[earlier];

                if (before.Trigger.Kind is not (TriggerKind.Land or TriggerKind.Scan) || !SameBody(before.Trigger, scan.Trigger))
                {
                    continue;
                }

                yield return ScanOutOfOrder(Where(scan.Index, scan.Beat), scan.Trigger, Where(before.Index, before.Beat), before.Trigger.Kind);
                break;
            }
        }
    }

    /// <summary>The one sentence for the case, worded for a person and for the model alike.</summary>
    internal static string ScanOutOfOrder(string where, AdventureTrigger scan, string earlier, TriggerKind earlierKind)
    {
        var body = scan.Body ?? "that body";

        return earlierKind == TriggerKind.Land
            ? $"{where} scans {body} after {earlier} lands on it; a body is scanned on the way in, before any landing, so the scan must come before the landing or be of another body."
            : $"{where} scans {body} again after {earlier}; a body is scanned once on the way in, so a second scan would never fire.";
    }

    internal static bool SameBody(AdventureTrigger first, AdventureTrigger second) =>
        first.SystemAddress is { } firstSystem && second.SystemAddress is { } secondSystem
        && first.BodyId is { } firstBody && second.BodyId is { } secondBody
            ? firstSystem == secondSystem && firstBody == secondBody
            : !string.IsNullOrWhiteSpace(first.Body)
              && string.Equals(first.System, second.System, StringComparison.OrdinalIgnoreCase)
              && string.Equals(first.Body, second.Body, StringComparison.OrdinalIgnoreCase);

    /// <summary>A stable key from a name: lower case, dashes, nothing else.</summary>
    public static string Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var key = new StringBuilder();
        var dash = false;

        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                key.Append(character);
                dash = false;
            }
            else if (!dash && key.Length > 0)
            {
                key.Append('-');
                dash = true;
            }
        }

        return key.ToString().TrimEnd('-');
    }

    /// <summary>The lines the writer wrote, leaving out the narrator's introductions.</summary>
    private static int Written(IReadOnlyList<AdventureLine> lines) => lines.Count(line => line.Introduces is null);

    private static string Where(int index, AdventureBeat beat) =>
        string.IsNullOrWhiteSpace(beat.Title)
            ? $"Objective {(index + 1).ToString(CultureInfo.InvariantCulture)}"
            : $"Objective {(index + 1).ToString(CultureInfo.InvariantCulture)} ({beat.Title.Trim()})";
}
