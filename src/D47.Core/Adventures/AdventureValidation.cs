using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>The career and navy ladders a rank beat may name, and how each is said.</summary>
public static class Careers
{
    /// <summary>The journal's own keys, which are what a <c>Promotion</c> event carries.</summary>
    public static IReadOnlyList<string> Keys { get; } = [.. RankState.Careers, Empire, Federation];

    public const string Empire = "Empire";

    public const string Federation = "Federation";

    /// <summary>Whether the key is the Imperial or the Federal navy rather than a career.</summary>
    public static bool IsNavy(string? career) =>
        string.Equals(career, Empire, StringComparison.OrdinalIgnoreCase)
        || string.Equals(career, Federation, StringComparison.OrdinalIgnoreCase);

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
        ["Imperial"] = Empire,
        ["Federal"] = Federation,
        ["Xenobiologist"] = "Exobiologist",
    };

    /// <summary>The highest rank a rank objective may name: 14 for a navy, Elite V for a career, Elite for CQC.</summary>
    public static int TopRank(string? career) =>
        IsNavy(career) ? RankStanding.NavyTop
        : string.Equals(career, "CQC", StringComparison.OrdinalIgnoreCase) ? RankStanding.Elite
        : RankStanding.EliteTop;

    /// <summary>The name of a rank on a ladder: a navy rank by its title, a career rank as <see cref="RankWord"/>.</summary>
    public static string RankName(string? career, int rank) =>
        string.Equals(career, Empire, StringComparison.OrdinalIgnoreCase) ? NavalRanks.EmpireName(rank) ?? RankWord(rank)
        : string.Equals(career, Federation, StringComparison.OrdinalIgnoreCase) ? NavalRanks.FederationName(rank) ?? RankWord(rank)
        : RankWord(rank);

    /// <summary>A rank in words: "Elite III" from 8 up, otherwise "rank 6".</summary>
    public static string RankWord(int rank) =>
        rank >= RankStanding.Elite ? RankStanding.EliteName(rank) : $"rank {rank.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>How a Commander says the career.</summary>
    public static string Word(string? career) => career switch
    {
        "Explore" => "Exploration",
        "Soldier" => "Mercenary",
        "Exobiologist" => "Exobiology",
        "Empire" => "Imperial Navy",
        "Federation" => "Federal Navy",
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
         "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound", "conflict", "faction", "reach", "boost", "discover", "footfall", "contribute", "claim", "unlock", "pledge", "powerrank", "site",
         "missiontaken", "missiondestination", "missioncomplete", "missionabandon", "choice", "event"];

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

    /// <summary>What stops this being stored or begun, each naming where and why. A story being loaded keeps event values the step event table no longer holds.</summary>
    public static IReadOnlyList<string> Problems(Adventure adventure, bool loading = false)
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

        if (adventure.Beats.Count == 0)
        {
            problems.Add("A story needs at least one beat.");
        }
        else if (adventure.Beats.Count > AdventureLimits.MaxBeats)
        {
            problems.Add($"{adventure.Beats.Count} beats; at most {AdventureLimits.MaxBeats}.");
        }

        if (adventure.Events.Count > AdventureLimits.MaxEvents)
        {
            problems.Add($"{adventure.Events.Count} d47 events; at most {AdventureLimits.MaxEvents}.");
        }

        problems.AddRange(ScansOutOfOrder(adventure.Beats));
        problems.AddRange(MissionHandleProblems(adventure.Beats));
        problems.AddRange(ChoiceProblems(adventure.Beats, adventure.OpeningLines, loading));

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
                    problems.AddRange(CountedProblems(stepWhere, step.Trigger, loading));
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

                        var topRank = Careers.TopRank(Careers.Match(step.Trigger.Career));

                        if (step.Trigger.Rank is not { } stepRank || stepRank < 1 || stepRank > topRank)
                        {
                            problems.Add($"{stepWhere} names rank {step.Trigger.Rank?.ToString(CultureInfo.InvariantCulture) ?? "nothing"}; ranks run 1 to {topRank}.");
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

                    case TriggerKind.MissionTaken:
                        problems.AddRange(MissionFamilyProblems(stepWhere, step.Trigger));
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

        if (trigger.Kind is TriggerKind.Conflict or TriggerKind.Choice)
        {
            yield return $"{where} has a failsOn of the {trigger.Kind.ToString().ToLowerInvariant()} kind, which cannot fail a chapter.";
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
    public static IEnumerable<string> CountedProblems(string where, AdventureTrigger trigger, bool loading = false)
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

        if (trigger.Kind == TriggerKind.Mission)
        {
            foreach (var problem in MissionFamilyProblems(where, trigger))
            {
                yield return problem;
            }
        }

        if (trigger.Kind == TriggerKind.Event)
        {
            foreach (var problem in EventProblems(where, trigger, loading))
            {
                yield return problem;
            }
        }
    }

    /// <summary>What is wrong with an event trigger: an event or field the step event table does not allow, a value the field does not allow, or a task that is empty or too long.</summary>
    public static IEnumerable<string> EventProblems(string where, AdventureTrigger trigger, bool loading = false)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var catalogue = StepEventCatalogue.Default;
        var name = trigger.Event?.Trim();

        if (!catalogue.Exists(name))
        {
            yield return $"{where} waits for the event \"{name ?? string.Empty}\", which is not an event a step may wait on.";
        }
        else
        {
            foreach (var pair in trigger.Where)
            {
                var fields = catalogue.FieldsOf(name);

                if (!fields.Contains(pair.Field, StringComparer.Ordinal))
                {
                    yield return $"{where} tests the field \"{pair.Field}\", which {name} does not have; its fields are {string.Join(", ", fields)}.";
                }
                else if (!loading && !catalogue.AllowsValue(name, pair.Field, pair.Value))
                {
                    yield return $"{where} tests {name} {pair.Field} against \"{pair.Value}\", which that field does not allow.";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(trigger.Task))
        {
            yield return $"{where} waits for an event but gives no task to describe it.";
        }
        else if (trigger.Task.Trim().Length > AdventureTrigger.MaxTaskLength)
        {
            yield return $"{where}'s task is {trigger.Task.Trim().Length} characters; at most {AdventureTrigger.MaxTaskLength}.";
        }
    }

    /// <summary>What is wrong with the mission family a trigger names: one that does not start with <c>Mission_</c>, or one that is set aside.</summary>
    public static IEnumerable<string> MissionFamilyProblems(string where, AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.MissionFamily is { } family && !string.IsNullOrWhiteSpace(family))
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

    /// <summary>What is wrong with the mission handles of a chapter: a step without one, a handle taken twice, one used before its step, or a destination for a family whose accept names none.</summary>
    public static IEnumerable<string> MissionHandleProblems(IReadOnlyList<AdventureBeat> beats)
    {
        var taken = new Dictionary<string, AdventureTrigger>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < beats.Count; index++)
        {
            var where = Where(index, beats[index]);

            foreach (var step in beats[index].Steps)
            {
                // A mission an option takes is the option's alone: no other option, and nothing after the choice, can count on it.
                foreach (var (option, at) in step.Options.Select((option, at) => (option, at)))
                {
                    foreach (var problem in HandleProblems($"{where}, option {(at + 1).ToString(CultureInfo.InvariantCulture)}", option.Steps, new Dictionary<string, AdventureTrigger>(taken, StringComparer.OrdinalIgnoreCase)))
                    {
                        yield return problem;
                    }
                }

                foreach (var problem in HandleProblems(where, [step], taken))
                {
                    yield return problem;
                }
            }
        }
    }

    /// <summary>The handle problems of <paramref name="steps"/> in order, with the missions taken before them in <paramref name="taken"/>, which it adds to.</summary>
    private static IEnumerable<string> HandleProblems(string where, IEnumerable<BeatStep> steps, Dictionary<string, AdventureTrigger> taken)
    {
        foreach (var trigger in steps.Select(step => step.Trigger).Where(trigger => AdventureTrigger.IsMissionHandleKind(trigger.Kind)))
        {
            if (string.IsNullOrWhiteSpace(trigger.Handle))
            {
                yield return $"{where} names no mission handle.";
            }
            else if (trigger.Kind == TriggerKind.MissionTaken)
            {
                if (!taken.TryAdd(trigger.Handle.Trim(), trigger))
                {
                    yield return $"{where} takes the mission \"{trigger.Handle.Trim()}\" again; a handle names one mission.";
                }
            }
            else if (!taken.TryGetValue(trigger.Handle.Trim(), out var binds))
            {
                yield return $"{where} refers to the mission \"{trigger.Handle.Trim()}\" before any objective takes it.";
            }
            else if (trigger.Kind == TriggerKind.MissionDestination && !MissionFamilies.CarriesDestination(binds.MissionFamily))
            {
                yield return $"{where} goes to the destination of the mission \"{trigger.Handle.Trim()}\", but "
                    + (string.IsNullOrWhiteSpace(binds.MissionFamily) ? "it takes a mission of any family" : $"{binds.MissionFamily.Trim()} missions do not name a destination")
                    + $"; name one of {string.Join(", ", MissionFamilies.WithDestination)}.";
            }
        }
    }

    /// <summary>
    /// What is wrong with the choices of a chapter: one that is not the first step of its objective or that the narrator offers, too few or
    /// too many options, a missing or long label, an option step that travels or is a choice, options on a step that is not a choice, or
    /// an ending that is unnamed, unsaid, named twice or named as the failure.
    /// </summary>
    public static IEnumerable<string> ChoiceProblems(IReadOnlyList<AdventureBeat> beats, IReadOnlyList<AdventureLine> opening, bool loading = false)
    {
        var endings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < beats.Count; index++)
        {
            var beat = beats[index];
            var where = Where(index, beat);

            foreach (var (step, stepIndex) in beat.Steps.Select((step, stepIndex) => (step, stepIndex)))
            {
                if (!step.IsChoice)
                {
                    if (step.Options.Count > 0)
                    {
                        yield return $"{where} has options on a step that is not a choice.";
                    }

                    continue;
                }

                if (stepIndex > 0)
                {
                    yield return $"{where} offers a choice at step {(stepIndex + 1).ToString(CultureInfo.InvariantCulture)}; a choice is the first step of its objective, offered by the line before it.";
                }
                else if ((index > 0 ? beats[index - 1].Lines : opening).LastOrDefault()?.Speaker == Stories.StorySpeaker.Narrator)
                {
                    yield return $"{where} begins with a choice that the narrator offers; the narrator never offers anything, so the line before it is the ship's or a character's.";
                }

                if (step.Options.Count is < 2 or > 3)
                {
                    yield return $"{where} offers {step.Options.Count.ToString(CultureInfo.InvariantCulture)} options; a choice offers two or three.";
                }

                foreach (var (option, at) in step.Options.Select((option, at) => (option, at)))
                {
                    var optionWhere = $"{where}, option {(at + 1).ToString(CultureInfo.InvariantCulture)}";

                    if (string.IsNullOrWhiteSpace(option.Label))
                    {
                        yield return $"{optionWhere} has no label.";
                    }
                    else if (option.Label.Trim().Length > BeatOption.MaxLabelLength)
                    {
                        yield return $"{optionWhere}'s label is {option.Label.Trim().Length.ToString(CultureInfo.InvariantCulture)} characters; at most {BeatOption.MaxLabelLength.ToString(CultureInfo.InvariantCulture)}.";
                    }

                    foreach (var optionStep in option.Steps)
                    {
                        if (optionStep.IsChoice || optionStep.Options.Count > 0)
                        {
                            yield return $"{optionWhere} offers a choice of its own; an option's steps cannot.";
                        }
                        else if (IsPlace(optionStep.Trigger.Kind) || optionStep.Trigger is { Kind: TriggerKind.MissionTaken, Station.Length: > 0 })
                        {
                            yield return $"{optionWhere} sends the Commander to a place ({optionStep.Trigger.Describe()}); an option's steps are things to do, not places to go.";
                        }
                        else if (optionStep.Trigger.IsCounted)
                        {
                            foreach (var problem in CountedProblems(optionWhere, optionStep.Trigger, loading))
                            {
                                yield return problem;
                            }
                        }
                        else if (optionStep.Trigger.Kind == TriggerKind.MissionTaken)
                        {
                            foreach (var problem in MissionFamilyProblems(optionWhere, optionStep.Trigger))
                            {
                                yield return problem;
                            }
                        }
                        else if (!AdventureTrigger.IsMissionHandleKind(optionStep.Trigger.Kind) && !AdventureTrigger.IsOnceKind(optionStep.Trigger.Kind))
                        {
                            yield return $"{optionWhere} is a \"{optionStep.Trigger.Kind.ToString().ToLowerInvariant()}\" step; an option's steps are missions, counted or one-event objectives.";
                        }

                        foreach (var problem in optionStep.FailsOn.SelectMany(fail => FailsOnProblems(optionWhere, fail)))
                        {
                            yield return problem;
                        }
                    }

                    if (option.Ending is null)
                    {
                        if (option.EndingLine is not null)
                        {
                            yield return $"{optionWhere} has an ending line but no ending.";
                        }

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(option.Ending) || option.Ending.Trim().Length > AdventureLimits.MaxTitleLength)
                    {
                        yield return $"{optionWhere}'s ending needs a name of at most {AdventureLimits.MaxTitleLength.ToString(CultureInfo.InvariantCulture)} characters.";
                    }
                    else if (string.Equals(option.Ending.Trim(), AdventureEnding.Failed, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return $"{optionWhere}'s ending is named \"{AdventureEnding.Failed}\", which is the failure's.";
                    }
                    else if (!endings.Add(option.Ending.Trim()))
                    {
                        yield return $"{optionWhere}'s ending \"{option.Ending.Trim()}\" is named by another option too.";
                    }

                    if (string.IsNullOrWhiteSpace(option.EndingLine))
                    {
                        yield return $"{optionWhere} ends the chapter but has no line to say when it does.";
                    }
                    else if (option.EndingLine.Trim().Length > AdventureLimits.MaxLineLength)
                    {
                        yield return $"{optionWhere}'s ending line is {option.EndingLine.Trim().Length.ToString(CultureInfo.InvariantCulture)} characters; at most {AdventureLimits.MaxLineLength.ToString(CultureInfo.InvariantCulture)}.";
                    }
                }
            }
        }
    }

    /// <summary>Whether a kind waits on the Commander being at a place.</summary>
    private static bool IsPlace(TriggerKind kind) =>
        kind is TriggerKind.Arrive or TriggerKind.Dock or TriggerKind.Land or TriggerKind.Scan or TriggerKind.Beacon or TriggerKind.Reach or TriggerKind.Site;

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
