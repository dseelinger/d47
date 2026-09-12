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

            new ToolDefinition
            {
                Protected = true,
                Name = "promote_engineer_route",
                Description =
                    "Offer the way in to an engineer to the checklist, as a chain rather than a line — "
                    + "one item per stop, each carrying the grade that stop needs. A proposal: the "
                    + "Commander accepts it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "engineer",
                        Type = ToolParameterType.String,
                        Description = "Which engineer, by name. Omit for the best next unlock.",
                    },
                ],
                Commands =
                [
                    new ToolCommandPhrase("put that route on my checklist", Nothing),
                    new ToolCommandPhrase("promote this unlock", Nothing),
                ],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("engineer", out var named);

                    return Task.FromResult(ToolResult.Ok(
                        unlocks?.Promote(named) ?? "No engineer plans are loaded."));
                },
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
