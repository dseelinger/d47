using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Engineers: where they are, what they grade, and how far along the Commander is with each
/// (list.md Phase 14, "Engineers").
/// <para>
/// Two halves that only mean something together. The <em>directory</em> is a shipped table —
/// where each engineer works and what they modify — and the <em>standing</em> is folded from the
/// Commander's own journal. "Who grades frame shift drives" has one answer for everybody; "who
/// can grade mine" has one answer for this Commander, and it is the second question people are
/// actually asking.
/// </para>
/// <para>
/// <b>The chain of unlocks is not asserted here, only observed.</b> That was once because no
/// permissive source for the referral graph had been found; two have been since, and the table has
/// not caught up yet (see <see cref="EngineerDirectory"/>). What this capability says meanwhile is
/// what the journal already knows: an engineer who has invited the Commander is a referral that has
/// happened, and one they have never heard of is a step they have not reached.
/// </para>
/// </summary>
public static class EngineerCapability
{
    public const string Id = "engineers";

    public static CapabilityDescriptor Create(Func<CommanderGameState?> commander) => new()
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
        ],
        Keywords =
        [
            "which engineers",
            "my engineers",
            "engineer progress",
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
                    "Look an engineer up by name, or find who grades a kind of module. Says where they "
                    + "work, what they modify and to what grade, what their invitation asks for where that "
                    + "is a delivery, and how far along the Commander is with them.",
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
                ],
                Handler = (arguments, _) => Task.FromResult(ToolResult.Ok(Find(commander, arguments))),
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
            // Written on entering the game, so silence before that is missing evidence rather
            // than a Commander who has unlocked nobody.
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

            // Highest grade first: the grade is what decides who is worth flying to, and an
            // alphabetical list makes the Commander find that out for themselves.
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

        // The third state, and it needs its own line or the numbers do not add up. An engineer
        // the game has told the Commander about but who has not invited them is neither invited
        // nor unmet, and leaving them out of all three lists makes one person silently vanish
        // from a report whose whole job is to account for everybody.
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

        if (!string.IsNullOrWhiteSpace(name))
        {
            return EngineerDirectory.ByName(name) is { } engineer
                ? Describe(engineer, commander())
                : Catalogue.Unknown("engineer", name.Trim(), EngineerDirectory.Near(name));
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            return "Name an engineer, or say what kind of module needs grading.";
        }

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
            report.Append($"  {engineer.Name} — to grade {speciality.MaxGrade}, at {engineer.Where}");

            // The Commander's own standing beside each one, because "who grades this" is nearly
            // always asked as "who can grade this for me", and the two answers can differ
            // completely.
            if (progress?.For(engineer.Id) is { } standing)
            {
                report.Append($"; {standing.Describe()}");
            }
            else if (progress?.IsKnown == true)
            {
                report.Append("; not met");
            }

            report.AppendLine();
        }

        return report.ToString().TrimEnd();
    }

    private static string Describe(Engineer engineer, CommanderGameState? active)
    {
        var report = new StringBuilder();

        report.AppendLine($"{engineer.Name} works out of {engineer.Where}.");

        if (engineer.Specialities.Count > 0)
        {
            report.AppendLine("Grades: " + string.Join(
                ", ",
                engineer.Specialities.Select(speciality => $"{speciality.Kind} to {speciality.MaxGrade}")) + ".");
        }
        else
        {
            // Real people d47 has a location for and no blueprint data on. Saying so beats
            // implying they do nothing.
            report.AppendLine("I have no record of what they modify.");
        }

        if (engineer.UnlockCost is { } cost)
        {
            report.AppendLine($"Their invitation asks for {cost}.");
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
}
