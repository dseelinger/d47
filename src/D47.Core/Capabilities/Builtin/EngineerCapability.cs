using System.Globalization;
using System.Text;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Engineers: where they are, what they grade, and how far along the Commander is with each (Phase 14,
/// "Engineers").
/// </summary>
public static class EngineerCapability
{
    public const string Id = "engineers";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// <param name="unlocks"> The solver, or null under the designer and in tests that are not about it
    /// — the capability still registers, so its documentation page still exists, and the two routing
    /// tools answer that there is nothing to rank rather than throwing.
    /// </summary>
    /// <param name="unlocks">
    /// The solver, or null under the designer and in tests that are not about it — the capability still
    /// registers, so its documentation page still exists, and the two routing tools answer that there
    /// is nothing to rank rather than throwing.
    /// </param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        EngineerPlanService? unlocks = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Engineers",
        Summary = "Where each engineer is, what they grade, and how far along the Commander is with them.",
        Examples =
        [
            "which engineers have I unlocked",
            "who grades frame shift drives",
            "where is Felicity Farseer",
            "who should I unlock next",
            "what is left for Liz Ryder",
        ],
        // Each names its tool (#161): four tools here take no required argument.
        Keywords =
        [
            new("which engineers", "get_engineer_progress"),
            new("my engineers", "get_engineer_progress"),
            new("engineer progress", "get_engineer_progress"),
            new("who should I unlock next", "get_engineer_route"),
            new("engineer in this system", "find_engineer"),
            new("who's the engineer here", "find_engineer"),
            new("which engineer is here", "find_engineer"),
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_engineer_progress",
                Description =
                    "How far along the Commander is with every engineer: which are unlocked and at what "
                    + "grade, which have invited them, and which they have not met. Read from the journal, "
                    + "so it is about this Commander rather than about the game.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeProgress(commander))),
            },
            new ToolDefinition
            {
                Name = "find_engineer",
                Description =
                    "Look an engineer up by name, find who grades a kind of module, or find who is based "
                    + "in a system. Says where they work, what they modify and to what grade, who has to "
                    + "recommend them and at what grade, what earns their invitation and what it asks "
                    + "for, and how far along the Commander is both with them and with whoever refers "
                    + "them.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "engineer",
                        Type = ToolParameterType.String,
                        Description = "An engineer by name — for example \"Farseer\" or \"Hera Tani\".",
                    },
                    new ToolParameter
                    {
                        Name = "grades",
                        Type = ToolParameterType.String,
                        Description =
                            "A kind of module to find engineers for — for example \"Frame Shift Drive\", "
                            + "\"Thrusters\" or \"Shield Generator\".",
                    },
                    new ToolParameter
                    {
                        Name = "system",
                        Type = ToolParameterType.String,
                        Description =
                            "A system to find the engineer based there, by name — the Commander's "
                            + "current system when left out.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Find(commander, arguments))),
            },

            new ToolDefinition
            {
                Name = "get_engineer_prerequisites",
                Description =
                    "What is still standing between the Commander and one engineer: the prerequisites from "
                    + "their unlock chain that are not yet met, each with d47's reading of it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "engineer",
                        Type = ToolParameterType.String,
                        Description = "An engineer by name — for example \"Farseer\" or \"Hera Tani\".",
                        Required = true,
                    },
                ],
                Commands = [.. EngineerDirectory.All.SelectMany(PrerequisitePhrases)],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Prerequisites(commander, arguments))),
            },

            new ToolDefinition
            {
                Name = "get_engineer_unlock_requirements",
                Description =
                    "Every engineer's unlock chain in one call, with the Commander's standing against "
                    + "each step: who refers them, what earns the invitation, what it asks for, and "
                    + "whether each of those is met. Covers all engineers at once, so it is the tool for "
                    + "a question across them — which engineers want a given material, commodity or "
                    + "on-foot item, which ask for a rank or reputation, or which need nothing but a "
                    + "trip. get_engineer_prerequisites answers for one engineer; this answers for all "
                    + "of them.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeUnlockRequirements(commander))),
            },

            // Protected, and cost is the reason rather than safety: it reads and writes nothing.
            new ToolDefinition
            {
                Protected = true,
                Name = "get_engineer_route",
                Description =
                    "Which engineer to unlock next, ranked by the trip it takes and how much of what the "
                    + "Commander has planned it covers. Shows its working: the stops, the distance in "
                    + "jumps of the ship being flown, what each invitation asks for, and what cannot be "
                    + "counted in jumps at all.",
                Commands =
                [
                    new ToolCommandPhrase("who should I unlock next", Nothing),
                    new ToolCommandPhrase("what is the fastest way in", Nothing),
                    new ToolCommandPhrase("which engineer next", Nothing),
                ],
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(
                    unlocks?.Describe() ?? "No engineer plans are loaded.")),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Engineers", Order = 50 },
    };

    private static string DescribeProgress(Func<CommanderGameState?> commander)
    {
        var active = commander();

        if (active is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var progress = active.Engineers;

        if (!progress.IsKnown)
        {
            // Written on entering the game, so silence before that is missing evidence rather than a
            // Commander who has unlocked nobody.
            return "I have no engineer progress yet — it is written when you enter the game.";
        }

        var report = new StringBuilder();

        var unlocked = progress.Unlocked;
        var invited = progress.Invited;

        report.AppendLine(
            $"{unlocked.Count} engineer{(unlocked.Count == 1 ? "" : "s")} unlocked of "
            + $"{EngineerDirectory.All.Count} that exist.");

        if (unlocked.Count > 0)
        {
            report.AppendLine();

            // Highest grade first: the grade is what decides who is worth flying to, and an alphabetical list
            // makes the Commander find that out for themselves.
            foreach (var standing in unlocked.OrderByDescending(s => s.Rank ?? 0).ThenBy(s => s.Name, StringComparer.Ordinal))
            {
                report.Append($"  {standing.Name} — grade {standing.Rank?.ToString() ?? "unknown"}");

                if (EngineerDirectory.ById(standing.Id) is { } engineer)
                {
                    report.Append($", at {engineer.Where}");
                }

                report.AppendLine();
            }
        }

        if (invited.Count > 0)
        {
            // The observed half of the chain: an invitation is a referral that has happened.
            report.AppendLine();
            report.AppendLine($"Invited and not yet unlocked: {Names(invited)}.");
        }

        // The third state, and it needs its own line or the numbers do not add up.
        var known = progress.Standings
            .Where(standing => !standing.IsUnlocked && !standing.IsInvited)
            .ToArray();

        if (known.Length > 0)
        {
            report.AppendLine();
            report.AppendLine($"Heard of, no invitation yet: {Names(known)}.");
        }

        var met = progress.Standings.Select(standing => standing.Id).ToHashSet();
        var unmet = EngineerDirectory.All.Where(engineer => !met.Contains(engineer.Id)).ToArray();

        if (unmet.Length > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"Not met at all: {string.Join(", ", unmet.Select(engineer => engineer.Name))}.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Every engineer's unlock chain and the Commander's standing against each step, one block per
    /// engineer (#272). With no journal, every criterion reads undetermined rather than refusing.
    /// </summary>
    private static string DescribeUnlockRequirements(Func<CommanderGameState?> commander)
    {
        var active = commander();
        var progress = active?.Engineers;
        var evidence = Engineers.UnlockEvidence.From(active);

        var report = new StringBuilder();
        report.AppendLine($"{EngineerDirectory.All.Count} engineers.");

        foreach (var engineer in EngineerDirectory.All.OrderBy(engineer => engineer.Name, StringComparer.Ordinal))
        {
            var criteria = EngineerAccess.CriteriaFor(engineer, evidence);
            var index = 0;

            report.AppendLine();
            report.Append(engineer.Name);

            if (StatusFor(progress, engineer.Id) is { } status)
            {
                report.Append(" — ").Append(status);
            }

            report.AppendLine();

            if (engineer.NeedsReferral)
            {
                var referrals = criteria.Take(engineer.ReferredBy.Count).ToArray();
                index += referrals.Length;

                report.AppendLine(
                    $"  Referred by: {Join(engineer.ReferredBy)}. — {ReferralVerdict(referrals)}");
            }
            else
            {
                report.AppendLine("  Referred by: nobody.");
            }

            if (engineer.Meeting is { } meeting)
            {
                report.AppendLine($"  Meeting: {meeting} — {Verdict(criteria[index])}");
                index++;
            }

            if (engineer.Unlock is { } unlock)
            {
                report.AppendLine($"  Unlock: {unlock} — {Verdict(criteria[index])}");
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>The Commander's standing with one engineer, or null where there is no journal to read it from.</summary>
    private static string? StatusFor(EngineerProgressState? progress, int id)
    {
        if (progress is not { IsKnown: true })
        {
            return null;
        }

        var standing = progress.For(id);

        if (standing is null)
        {
            return "not in the journal";
        }

        if (standing.IsUnlocked)
        {
            return $"unlocked at grade {standing.Rank?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";
        }

        return standing.IsInvited ? "invited" : "known";
    }

    /// <summary>One word for a criterion, with d47's own reading of it where there is one.</summary>
    private static string Verdict(UnlockCriterion criterion)
    {
        var word = criterion.Met switch
        {
            true => "met",
            false => "not met",
            null => "undetermined",
        };

        return criterion.Reading is { Length: > 0 } reading ? $"{word} ({reading})" : word;
    }

    /// <summary>Any one of several referrals is enough, so the group is met if any single one is.</summary>
    private static string ReferralVerdict(IReadOnlyList<UnlockCriterion> referrals)
    {
        if (referrals.Any(criterion => criterion.Met == true))
        {
            return "met";
        }

        return referrals.Any(criterion => criterion.Met is null) ? "undetermined" : "not met";
    }

    /// <summary>
    /// "What is left for X" as a fixed phrase, one set per engineer, since a command phrase's
    /// arguments cannot be read out of the utterance (#266).
    /// </summary>
    private static IEnumerable<ToolCommandPhrase> PrerequisitePhrases(Engineer engineer)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["engineer"] = engineer.Name };

        yield return new ToolCommandPhrase($"what is left for {engineer.Name}", arguments);
        yield return new ToolCommandPhrase($"what's left for {engineer.Name}", arguments);
        yield return new ToolCommandPhrase($"what does {engineer.Name} still need", arguments);
    }

    private static string Prerequisites(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        arguments.TryGetString("engineer", out var name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name an engineer.";
        }

        if (EngineerDirectory.ByName(name) is not { } engineer)
        {
            return Catalogue.Unknown("engineer", name.Trim(), EngineerDirectory.Near(name));
        }

        var evidence = Engineers.UnlockEvidence.From(commander());

        if (evidence.Progress?.For(engineer.Id)?.IsUnlocked == true)
        {
            return $"{engineer.Name} is unlocked. Nothing is left.";
        }

        var unmet = EngineerAccess.CriteriaFor(engineer, evidence).Where(criterion => criterion.Met != true).ToArray();

        if (unmet.Length == 0)
        {
            return $"Every prerequisite for {engineer.Name} is met, but they have not unlocked yet.";
        }

        var report = new StringBuilder();
        report.AppendLine($"Still needed for {engineer.Name}:");

        foreach (var criterion in unmet)
        {
            report.AppendLine(criterion.Reading is { Length: > 0 } reading
                ? $"  {criterion.Text.TrimEnd('.')}: {reading}."
                : $"  {criterion.Text}");
        }

        return report.ToString().TrimEnd();
    }

    private static string Names(IReadOnlyList<EngineerStanding> standings) =>
        string.Join(", ", standings.Select(standing => standing.Name));

    private static string Find(Func<CommanderGameState?> commander, ToolArguments arguments)
    {
        arguments.TryGetString("engineer", out var name);
        arguments.TryGetString("grades", out var kind);
        arguments.TryGetString("system", out var system);

        if (!string.IsNullOrWhiteSpace(name))
        {
            return EngineerDirectory.ByName(name) is { } engineer
                ? Describe(engineer, commander())
                : Catalogue.Unknown("engineer", name.Trim(), EngineerDirectory.Near(name));
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            return ByKind(commander, kind);
        }

        // A system, named or the Commander's own, is asked for by leaving both of the above out.
        var resolved = string.IsNullOrWhiteSpace(system) ? commander()?.Location.StarSystem : system.Trim();

        return string.IsNullOrWhiteSpace(resolved)
            ? "Name an engineer, or say what kind of module needs grading."
            : BySystem(commander, resolved);
    }

    private static string BySystem(Func<CommanderGameState?> commander, string system)
    {
        var here = EngineerDirectory.InSystem(system);

        return here.Count switch
        {
            0 => $"No engineer of mine is based in {system}.",
            1 => Describe(here[0], commander()),
            _ => string.Join("\n\n", here.Select(engineer => Describe(engineer, commander()))),
        };
    }

    private static string ByKind(Func<CommanderGameState?> commander, string kind)
    {
        var grading = EngineerDirectory.Grading(kind);

        if (grading.Count == 0)
        {
            return Catalogue.Unknown("modification", kind.Trim(), EngineerDirectory.NearKinds(kind));
        }

        var progress = commander()?.Engineers;
        var report = new StringBuilder();

        report.AppendLine(
            $"{grading.Count} engineer{(grading.Count == 1 ? "" : "s")} grade {grading[0].Speciality.Kind}, "
            + "best first:");

        foreach (var (engineer, speciality) in grading)
        {
            report.Append(speciality.IsGraded
                ? $"  {engineer.Name} — to grade {speciality.MaxGrade}, at {engineer.Where}"
                : $"  {engineer.Name} — at {engineer.Where}");

            // The Commander's own standing beside each one, because "who grades this" is nearly always asked
            // as "who can grade this for me", and the two answers can differ completely.
            if (progress?.For(engineer.Id) is { } standing)
            {
                report.Append($"; {standing.Describe()}");
            }
            else if (progress?.IsKnown == true)
            {
                report.Append("; not met");

                // The next step rather than a dead end.
                if (engineer.NeedsReferral)
                {
                    report.Append($", reached through {Join(engineer.ReferredBy)}");
                }
            }

            report.AppendLine();
        }

        return report.ToString().TrimEnd();
    }

    private static string Describe(Engineer engineer, CommanderGameState? active)
    {
        var report = new StringBuilder();

        report.AppendLine(
            $"{engineer.Name} works out of {engineer.Where}"
            + (engineer.Body is { } body ? $", on {body}." : "."));

        if (engineer.Specialities.Count > 0)
        {
            // A speciality with no grade is named without one.
            report.AppendLine("Grades: " + string.Join(
                ", ",
                engineer.Specialities.Select(speciality => speciality.IsGraded
                    ? $"{speciality.Kind} to {speciality.MaxGrade}"
                    : speciality.Kind)) + ".");
        }
        else
        {
            // Real people d47 has a location for and no blueprint data on.
            report.AppendLine("I have no record of what they modify.");
        }

        report.Append(Chain(engineer, active?.Engineers));

        if (engineer.Meeting is { } meeting)
        {
            report.AppendLine($"Earning the invitation: {meeting}");
        }

        // The prose and the material list are the same fact for the 26 engineers whose invitation is a
        // delivery, and only the prose exists for the rest — so the list goes out where there is one and the
        // sentence carries the others rather than leaving them blank.
        if (engineer.UnlockCost is { } cost)
        {
            report.AppendLine($"Their invitation asks for {cost}.");
        }
        else if (engineer.Unlock is { } unlock)
        {
            report.AppendLine($"Their invitation asks for: {unlock}");
        }

        var standing = active?.Engineers.For(engineer.Id);

        if (standing is not null)
        {
            report.AppendLine($"The Commander has them {standing.Describe()}.");
        }
        else if (active?.Engineers.IsKnown == true)
        {
            report.AppendLine("The Commander has not met them.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Who has to recommend this engineer, and where the Commander stands on that path.</summary>
    private static string Chain(Engineer engineer, EngineerProgressState? progress)
    {
        var report = new StringBuilder();

        if (!engineer.NeedsReferral)
        {
            // Said out loud rather than left silent, because "nobody has to introduce you" is the useful half
            // of the answer for the eleven who need nobody.
            report.AppendLine(engineer.Discovery is { } discovery
                ? $"Nobody has to recommend them — {LowerFirst(discovery)}"
                : "Nobody has to recommend them.");

            return report.ToString();
        }

        var through = engineer.ReferredBy;

        var at = engineer.ReferralGrade is { } grade
            ? $" at grade {grade}"
            : string.Empty;

        report.AppendLine(through.Count == 1
            ? $"Reached through {through[0]}{at}."
            : $"Reached through any of {Join(through)}{at}.");

        if (engineer.ReferralGrade is null)
        {
            report.AppendLine(
                "No grade is stated for that referral — the on-foot engineers unlock on a count of "
                + "modifications rather than on a grade.");
        }

        if (progress is not { IsKnown: true })
        {
            return report.ToString();
        }

        // Where the Commander actually stands on the path, which is the half no table can supply.
        var standings = through
            .Select(name => (Name: name, Standing: EngineerDirectory.ByName(name) is { } referrer
                ? progress.For(referrer.Id)
                : null))
            .ToArray();

        var best = standings
            .Where(pair => pair.Standing is { IsUnlocked: true })
            .OrderByDescending(pair => pair.Standing!.Rank ?? 0)
            .FirstOrDefault();

        if (best.Standing is null)
        {
            var met = standings.Where(pair => pair.Standing is not null).Select(pair => pair.Name).ToArray();

            // Named rather than "any of them", because the next sentence is about this engineer and two
            // unattributed "has not met" lines in a row read as one repeated.
            report.AppendLine(met.Length > 0
                ? $"The Commander has heard of {Join(met)} but unlocked nobody on that path."
                : $"The Commander has not met {Join(through)}.");

            return report.ToString();
        }

        var rank = best.Standing.Rank ?? 0;

        if (engineer.ReferralGrade is not { } needed)
        {
            report.AppendLine($"The Commander is grade {rank} with {best.Name}.");
        }
        else if (rank >= needed)
        {
            report.AppendLine(
                $"The Commander is grade {rank} with {best.Name}, so that referral is earned.");
        }
        else
        {
            // The gap is the answer.
            report.AppendLine(
                $"The Commander is grade {rank} with {best.Name}, and the referral needs grade {needed}"
                + $", plus roughly half the bar to the grade after it. {EngineeringRules.RankRises}");
        }

        return report.ToString();
    }

    private static string Join(IReadOnlyList<string> names) =>
        names.Count switch
        {
            0 => "nobody",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " or " + names[^1],
        };

    private static string LowerFirst(string text) =>
        text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
}
