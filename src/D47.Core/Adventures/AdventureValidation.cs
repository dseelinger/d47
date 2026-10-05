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
         "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound", "conflict", "faction"];

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
            problems.Add("An adventure needs a key.");
        }

        if (string.IsNullOrWhiteSpace(adventure.Name))
        {
            problems.Add("An adventure needs a name.");
        }
        else if (adventure.Name.Trim().Length > AdventureLimits.MaxNameLength)
        {
            problems.Add(
                $"The name is {adventure.Name.Trim().Length} characters; at most {AdventureLimits.MaxNameLength}.");
        }

        if (adventure.Opening is { } opening && opening.Trim().Length > AdventureLimits.MaxLineLength)
        {
            problems.Add($"The opening is {opening.Trim().Length} characters; at most {AdventureLimits.MaxLineLength}.");
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
            problems.Add("An adventure needs at least one beat.");
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

            if (beat.Lines.Count > AdventureLimits.MaxLinesPerBeat)
            {
                problems.Add($"{where} has {beat.Lines.Count} lines; at most {AdventureLimits.MaxLinesPerBeat}.");
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

            if (!Enum.IsDefined(beat.Trigger.Kind))
            {
                problems.Add($"{where} names a trigger that is not one of {string.Join(", ", Kinds)}.");
                continue;
            }

            if (beat.Trigger.IsCounted)
            {
                problems.AddRange(CountedProblems(where, beat.Trigger));
                continue;
            }

            switch (beat.Trigger.Kind)
            {
                case TriggerKind.Rank:
                    if (Careers.Match(beat.Trigger.Career) is null)
                    {
                        problems.Add(
                            $"{where} names a career \"{beat.Trigger.Career ?? string.Empty}\"; the careers are "
                            + string.Join(", ", Careers.Keys.Select(Careers.Word)) + ".");
                    }

                    if (beat.Trigger.Rank is not (>= 1 and <= RankStanding.Elite))
                    {
                        problems.Add($"{where} names rank {beat.Trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; ranks run 1 to {RankStanding.Elite}.");
                    }

                    break;

                case TriggerKind.Board:
                    if (string.IsNullOrWhiteSpace(beat.Trigger.ShipType))
                    {
                        problems.Add($"{where} boards no ship: it names no ship type.");
                    }
                    else if (EliteSpecifications.HullName(beat.Trigger.ShipType) is null)
                    {
                        problems.Add($"{where} names a ship \"{beat.Trigger.ShipType.Trim()}\" that d47 has no name for.");
                    }

                    break;

                case TriggerKind.Arrive:
                    if (beat.Trigger.SystemAddress is null && string.IsNullOrWhiteSpace(beat.Trigger.System))
                    {
                        problems.Add($"{where} arrives nowhere: it names no system.");
                    }

                    break;

                case TriggerKind.Dock:
                    if (beat.Trigger.MarketId is null && string.IsNullOrWhiteSpace(beat.Trigger.Station))
                    {
                        problems.Add($"{where} docks nowhere: it names no station.");
                    }

                    break;

                case TriggerKind.Land:
                case TriggerKind.Scan:
                    if ((beat.Trigger.SystemAddress is null || beat.Trigger.BodyId is null)
                        && string.IsNullOrWhiteSpace(beat.Trigger.Body))
                    {
                        problems.Add($"{where} names no body.");
                    }

                    break;

                case TriggerKind.Beacon:
                    if (beat.Trigger.SystemAddress is not { } beacon || !GuardianCores.Beacons.ContainsKey(beacon))
                    {
                        problems.Add($"{where} waits for a Guardian beacon scan in a system with no Guardian beacon.");
                    }

                    break;

                case TriggerKind.Engineer:
                    problems.AddRange(EngineerProblems(where, beat.Trigger));
                    break;
            }
        }

        return problems;
    }

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

    /// <summary>What is wrong with an engineer trigger: no engineer, or a stage that is not <c>Invited</c> or <c>Unlocked</c>.</summary>
    public static IEnumerable<string> EngineerProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (string.IsNullOrWhiteSpace(trigger.Engineer))
        {
            yield return $"{where} names no engineer.";
        }

        if (EngineerStages.Rank(trigger.Stage) == 0)
        {
            yield return $"{where} names the stage \"{trigger.Stage?.Trim() ?? string.Empty}\"; the stages are {string.Join(", ", EngineerStages.Named)}.";
        }
    }

    /// <summary>One caution sentence naming everything <see cref="Problems"/> found, or null when there is nothing to caution about.</summary>
    public static string? Caution(Adventure adventure)
    {
        var problems = Problems(adventure);

        if (problems.Count == 0)
        {
            return null;
        }

        var needs = new List<string>();

        if (string.IsNullOrWhiteSpace(adventure.Key))
        {
            needs.Add("a key");
        }

        if (string.IsNullOrWhiteSpace(adventure.Name))
        {
            needs.Add("a name");
        }

        if (adventure.Beats.Count == 0)
        {
            needs.Add("at least one beat");
        }

        var named = new HashSet<string>(StringComparer.Ordinal)
        {
            "An adventure needs a key.", "An adventure needs a name.", "An adventure needs at least one beat.",
        };

        var sentences = new List<string>();

        if (needs.Count > 0)
        {
            sentences.Add($"An adventure needs {Listed(needs)} before it can be saved.");
        }

        sentences.AddRange(problems.Where(problem => !named.Contains(problem)));

        return string.Join(" ", sentences);
    }

    /// <summary>Oxford-comma joins two or more items; a single item is returned as is.</summary>
    private static string Listed(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        2 => $"{items[0]} and {items[1]}",
        _ => $"{string.Join(", ", items.Take(items.Count - 1))}, and {items[^1]}",
    };

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

            if (!beat.Trigger.IsResolved)
            {
                reasons.Add($"{Where(index, beat)} — {beat.Trigger.Describe()} — is not yet a real place d47 can recognise.");
            }
        }

        return reasons;
    }

    /// <summary>A scan beat placed after a landing on, or an earlier scan of, the same body.</summary>
    internal static IEnumerable<string> ScansOutOfOrder(IReadOnlyList<AdventureBeat> beats)
    {
        for (var index = 0; index < beats.Count; index++)
        {
            var beat = beats[index];

            if (beat.Trigger.Kind != TriggerKind.Scan)
            {
                continue;
            }

            for (var earlier = 0; earlier < index; earlier++)
            {
                var before = beats[earlier];

                if (before.Trigger.Kind is not (TriggerKind.Land or TriggerKind.Scan) || !SameBody(before.Trigger, beat.Trigger))
                {
                    continue;
                }

                yield return ScanOutOfOrder(Where(index, beat), beat.Trigger, Where(earlier, before), before.Trigger.Kind);
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

    private static string Where(int index, AdventureBeat beat) =>
        string.IsNullOrWhiteSpace(beat.Title)
            ? $"Objective {(index + 1).ToString(CultureInfo.InvariantCulture)}"
            : $"Objective {(index + 1).ToString(CultureInfo.InvariantCulture)} ({beat.Title.Trim()})";
}
