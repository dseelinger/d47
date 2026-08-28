using System.Globalization;
using System.Text;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Engineers: where they are, what they grade, and how far along the Commander is with each
/// (Phase 14, "Engineers").
/// <para>
/// Two halves that only mean something together. The <em>directory</em> is a shipped table —
/// where each engineer works and what they modify — and the <em>standing</em> is folded from the
/// Commander's own journal. "Who grades frame shift drives" has one answer for everybody; "who
/// can grade mine" has one answer for this Commander, and it is the second question people are
/// actually asking.
/// </para>
/// <para>
/// <b>The chain of unlocks is asserted as well as observed.</b> The table names who recommends
/// each engineer and the grade that referral needs (see <see cref="EngineerDirectory"/>), so "how
/// do I get to this person" is answerable before the Commander has taken a step towards them. The
/// observed half still matters and still comes first where the two meet: an engineer who has
/// invited the Commander is a referral that has already happened, and no table can be more right
/// about that than their own journal.
/// </para>
/// <para>
/// <b>Phase 28 adds a solver, and adds it as two Protected tools rather than as advertised
/// ones.</b> The advertised surface is re-billed on every turn and the largest profile sat at
/// 39,639 bytes against a 40,000 ceiling when this was written, so a route worth several hundred
/// bytes of schema is a phrase and a press instead. Nothing is lost: "who should I unlock next"
/// is a fixed question with no free-text argument in it, which is exactly the shape the keyword
/// router handles without a round trip.
/// </para>
/// </summary>
public static class EngineerCapability
{
    public const string Id = "engineers";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <param name="unlocks">
    /// The solver, or null under the designer and in tests that are not about it — the capability
    /// still registers, so its documentation page still exists, and the two routing tools answer
    /// that there is nothing to rank rather than throwing.
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
        Keywords =
        [
            "which engineers",
            "my engineers",
            "engineer progress",
            "who should I unlock next",
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
                    + "work, what they modify and to what grade, who has to recommend them and at what "
                    + "grade, what earns their invitation and what it asks for, and how far along the "
                    + "Commander is both with them and with whoever refers them.",
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
            report.Append(speciality.IsGraded
                ? $"  {engineer.Name} — to grade {speciality.MaxGrade}, at {engineer.Where}"
                : $"  {engineer.Name} — at {engineer.Where}");

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

                // The next step rather than a dead end. An engineer the Commander has never heard
                // of is reachable through somebody, and naming them here turns a list of people
                // they cannot use into a list of paths they can start.
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
            // A speciality with no grade is named without one. Odyssey suit and weapon blueprints
            // are ungraded in the game, and "Suit to 0" reads as a defect rather than as a fact.
            report.AppendLine("Grades: " + string.Join(
                ", ",
                engineer.Specialities.Select(speciality => speciality.IsGraded
                    ? $"{speciality.Kind} to {speciality.MaxGrade}"
                    : speciality.Kind)) + ".");
        }
        else
        {
            // Real people d47 has a location for and no blueprint data on. Saying so beats
            // implying they do nothing.
            report.AppendLine("I have no record of what they modify.");
        }

        report.Append(Chain(engineer, active?.Engineers));

        if (engineer.Meeting is { } meeting)
        {
            report.AppendLine($"Earning the invitation: {meeting}");
        }

        // The prose and the material list are the same fact for the 26 engineers whose invitation
        // is a delivery, and only the prose exists for the rest — so the list goes out where there
        // is one and the sentence carries the others rather than leaving them blank.
        if (engineer.UnlockCost is { } cost)
        {
            report.AppendLine($"Their invitation asks for {cost}.");
        }
        else if (engineer.Unlock is { } unlock)
        {
            report.AppendLine($"Their invitation asks for: {unlock}");
        }

        if (engineer.Reputation is { } reputation)
        {
            report.AppendLine($"Reputation with them rises fastest by: {reputation}");
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

    /// <summary>
    /// Who has to recommend this engineer, and where the Commander stands on that path.
    /// <para>
    /// <b>Several referrers mean any of them will do</b>, not all — Yi Shen is reached through
    /// three people, and reading that as three requirements would describe a wall where there is a
    /// door. So the best standing among them is the one that decides the answer.
    /// </para>
    /// <para>
    /// <b>A referral with no grade is a referral with no grade.</b> The whole Odyssey chain states
    /// one nowhere, because those engineers unlock on a count of modifications instead; filling in
    /// <see cref="EngineeringRules.ReferralGrade"/> there would be d47 inventing a requirement the
    /// game does not have.
    /// </para>
    /// </summary>
    private static string Chain(Engineer engineer, EngineerProgressState? progress)
    {
        var report = new StringBuilder();

        if (!engineer.NeedsReferral)
        {
            // Said out loud rather than left silent, because "nobody has to introduce you" is the
            // useful half of the answer for the eleven who need nobody.
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

            // Named rather than "any of them", because the next sentence is about this engineer
            // and two unattributed "has not met" lines in a row read as one repeated.
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
            // The gap is the answer. It used to be priced in credits and that price was a wiki's
            // claim rather than a measurement (#26); what is stated now is the mechanism, from
            // Frontier's own roll table.
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
