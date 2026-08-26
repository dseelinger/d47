using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Community goals: what is running, what tier it has reached, and where the Commander stands
/// (list.md Phase 14, "Know the current community goals").
/// <para>
/// <b>The item was written around a split the journal does not make.</b> It asks for goals the
/// Commander is not participating in to come from Inara — but the journal's board event fires
/// off the noticeboard at a station, so it already reports goals they merely docked near and
/// have never joined. What it cannot report is a goal running somewhere they have not been, and
/// over thirteen months of one Commander's play it turned up ten. So the line the key buys is
/// <em>everywhere I have not been</em>, which is wider than "everything I have not joined", and
/// that is the wording the settings row and the answer both use.
/// </para>
/// <para>
/// <b>Two sources, never blended into one number.</b> The journal is authoritative about this
/// Commander — their contribution, their percentile band, whether they are in the top rank — and
/// Inara is authoritative about nothing except that a goal exists somewhere. A listing entry
/// carries no <c>CGID</c>, so the only thing the two can be matched on is a name, and a name is
/// exactly the field two sources spell differently. The rule is therefore to merge only on an
/// exact name match and to let anything else appear twice: a duplicate is visible and a wrong
/// merge is silent.
/// </para>
/// </summary>
public static class CommunityGoalCapability
{
    public const string Id = "community-goals";

    /// <summary>
    /// The Commander's own Inara key, in the secret store.
    /// <para>
    /// Spelled here rather than imported from the client, for the reason Core spells the
    /// ElevenLabs key too: Core depends on nothing, and the seam is the interface rather than
    /// the implementation's constants.
    /// </para>
    /// </summary>
    public const string KeySecretName = "inara.apiKey";

    public const string KeyRow = "knowledge.inaraKey";

    /// <param name="listing">
    /// The external listing, or null where none is composed — under the designer and in a test
    /// that is not about it. Null, and a key that has never been stored, produce the same
    /// answer: what the journal knows, said plainly as being only that (list.md Phase 3 —
    /// capabilities are state, not guards, so this must not dead-end).
    /// </param>
    /// <param name="now">
    /// Injected, because no Core component reads the clock — and because the whole correctness of
    /// this capability is a comparison against it.
    /// </param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        ICommunityGoalService? listing,
        Func<DateTimeOffset> now) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Community goals",
        Summary = "What community goals are running, what tier they have reached, and how you are doing in them.",
        Examples =
        [
            "what community goals are running",
            "how am I doing in the community goal",
            "what tier is the community goal at",
        ],
        Keywords =
        [
            "community goals",
            "community goal",
        ],
        Tools =
        [
            new ToolDefinition
            {
                Name = "get_community_goals",
                Description =
                    "Community goals that are running: where each is flown, what tier it has reached, how "
                    + "many Commanders are contributing, and this Commander's own contribution and "
                    + "percentile band. Goals the Commander has docked near come from their journal; the "
                    + "rest need an Inara API key, and the answer says which is which.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "Only goals whose title contains this. Leave out for all of them.",
                    },
                    new ToolParameter
                    {
                        Name = "include_finished",
                        Type = ToolParameterType.Boolean,
                        Description =
                            "Also list goals that have already expired, with what they paid out. "
                            + "Default false — an expired goal cannot be contributed to.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    DescribeAsync(commander, listing, now, arguments, cancellationToken),
            },
        ],
        Settings =
        [
            new SettingRow
            {
                Key = KeyRow,
                Advanced = true,
                Label = "Inara API key",
                Help =
                    "Lets D47 see community goals running where you have not been. Without it, it sees only "
                    + "the goals your own journal has reported. Your key is stored encrypted for this "
                    + "Windows account and is write-only: D47 will never show it back to you. Get one from "
                    + "your Inara profile, under API keys.",
                Kind = SettingKind.Secret,
                SecretName = KeySecretName,
                DocsAnchor = "inara-api-key",
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Community goals", Order = 52 },
    };

    private static async Task<ToolResult> DescribeAsync(
        Func<CommanderGameState?> commander,
        ICommunityGoalService? listing,
        Func<DateTimeOffset> now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        var active = commander();

        if (active is null)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet.");
        }

        var wanted = arguments.TryGetString("name", out var filter) && !string.IsNullOrWhiteSpace(filter)
            ? filter.Trim()
            : null;

        var finished = arguments.TryGetBoolean("include_finished", out var all) && all;

        var at = now();
        var board = active.CommunityGoals;

        var mine = board.Goals
            .Where(goal => finished || goal.IsLive(at))
            .Where(goal => Matches(goal.Title, wanted))
            .OrderByDescending(goal => goal.SeenAt)
            .ToList();

        IReadOnlyList<CommunityGoalListing> theirs = [];
        string? listingFailure = null;

        if (listing is { IsConfigured: true })
        {
            try
            {
                var report = await listing.RecentAsync(cancellationToken).ConfigureAwait(false);

                theirs = report.Goals
                    .Where(goal => finished || goal.Expiry is not { } expiry || expiry > at)
                    .Where(goal => Matches(goal.Name, wanted))

                    // Exact name only. See the class remark: a duplicate is visible and a wrong
                    // merge is silent.
                    .Where(goal => !mine.Any(seen => Same(seen.Title, goal.Name)))
                    .ToList();
            }
            catch (CommunityGoalsUnavailableException ex)
            {
                // Not an error result. The journal half still has an answer, and losing it
                // because the network did would be the capability dead-ending on a destination
                // it does not need.
                listingFailure = ex.Message;
            }
        }

        return ToolResult.Ok(Report(board, mine, theirs, listing, listingFailure, wanted, finished, at));
    }

    private static string Report(
        CommunityGoalBoard board,
        IReadOnlyList<CommunityGoal> mine,
        IReadOnlyList<CommunityGoalListing> theirs,
        ICommunityGoalService? listing,
        string? listingFailure,
        string? wanted,
        bool finished,
        DateTimeOffset at)
    {
        var report = new StringBuilder();

        if (mine.Count == 0 && theirs.Count == 0)
        {
            report.AppendLine(Nothing(board, wanted, finished));
        }
        else
        {
            if (mine.Count > 0)
            {
                report.AppendLine(
                    $"{mine.Count} community goal{(mine.Count == 1 ? "" : "s")} from your journal:");

                foreach (var goal in mine)
                {
                    report.AppendLine();
                    report.Append(Describe(goal, at));
                }
            }

            if (theirs.Count > 0)
            {
                if (mine.Count > 0)
                {
                    report.AppendLine();
                }

                report.AppendLine(
                    $"{theirs.Count} more reported by Inara, which your journal has not seen. Nothing here "
                    + "says anything about your own contribution:");

                foreach (var goal in theirs)
                {
                    report.AppendLine();
                    report.Append(Describe(goal, at));
                }
            }
        }

        if (listingFailure is not null)
        {
            report.AppendLine();
            report.AppendLine(listingFailure);
        }
        else if (listing is null or { IsConfigured: false })
        {
            report.AppendLine();
            report.AppendLine(
                "This is only what your journal has reported, which is the board at stations you have "
                + "docked at. Goals running where you have not been need an Inara API key, in settings.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// What to say when there is nothing to list. Four different silences, and calling them all
    /// "no community goals" would assert something about the galaxy from evidence about one
    /// Commander's docking history.
    /// </summary>
    private static string Nothing(CommunityGoalBoard board, string? wanted, bool finished)
    {
        if (!board.IsKnown)
        {
            return "I have no community goals yet — they are reported when you dock somewhere that lists one.";
        }

        if (wanted is not null)
        {
            return $"Nothing I know of matches \"{wanted}\".";
        }

        return finished
            ? "No community goals at all in what I have seen."
            : "No community goals are running in what I have seen. There may be some where you have not been.";
    }

    private static string Describe(CommunityGoal goal, DateTimeOffset at)
    {
        var report = new StringBuilder();

        report.AppendLine(goal.Title);

        var line = new List<string>();

        if (goal.Where is { } where)
        {
            line.Add(where);
        }

        line.Add(Deadline(goal.Expiry, at));

        report.AppendLine($"  {string.Join(" — ", line)}");

        var progress = new List<string>();

        // Level rather than fill: "Tier 3 of 5" is the fact, and a tier the goal has not reached
        // yet is absent from the journal rather than reported as zero.
        progress.Add(goal.TierReached is { } tier
            ? goal.TopTier is { } top ? $"tier {tier} of {top}" : $"tier {tier}"
            : "no tier reached yet");

        if (goal.NumContributors is { } contributors)
        {
            progress.Add($"{Number(contributors)} contributors");
        }

        if (goal.CurrentTotal is { } total)
        {
            progress.Add($"{Number(total)} delivered");
        }

        if (goal.IsComplete)
        {
            progress.Add("met");
        }

        report.AppendLine($"  {Capitalise(string.Join(", ", progress))}.");

        var standing = new List<string>();

        if (goal.PlayerContribution is { } contribution and > 0)
        {
            standing.Add($"you have contributed {Number(contribution)}");
        }

        if (goal.PlayerPercentileBand is { } band)
        {
            standing.Add($"top {band}%");
        }

        if (goal.PlayerInTopRank && goal.TopRankSize is { } rank)
        {
            standing.Add($"in the top {rank}");
        }

        if (goal.Bonus is { } bonus and > 0)
        {
            standing.Add($"the band pays {Number(bonus)} cr");
        }

        if (goal.RewardPaid is { } paid)
        {
            standing.Add($"paid out {Number(paid)} cr");
        }

        // Said even when nothing else about the Commander is known, because "you have not joined
        // this" and "I have no figures for you" are different answers and the second one is a
        // reason to go and look at the board rather than to skip the goal.
        standing.Add(goal.Signup switch
        {
            CommunityGoalSignup.Joined => "signed up",
            CommunityGoalSignup.Left => "you opted out of this one",
            _ when goal.IsParticipating => "signed up",
            _ => "not signed up as far as I know",
        });

        report.AppendLine($"  You: {string.Join(", ", standing)}.");

        report.AppendLine($"  Reported {Age(goal.SeenAt, at)}.");

        return report.ToString();
    }

    private static string Describe(CommunityGoalListing goal, DateTimeOffset at)
    {
        var report = new StringBuilder();

        report.AppendLine(goal.Name);

        var line = new List<string>();

        var where = (goal.StationName, goal.SystemName) switch
        {
            ({ } station, { } system) => $"{station}, {system}",
            (null, { } system) => system,
            ({ } station, null) => station,
            _ => null,
        };

        if (where is not null)
        {
            line.Add(where);
        }

        line.Add(Deadline(goal.Expiry, at));

        report.AppendLine($"  {string.Join(" — ", line)}");

        if (goal.Objective is { Length: > 0 } objective)
        {
            report.AppendLine($"  {objective}");
        }

        var progress = new List<string>();

        progress.Add(goal.TierReached is { } tier
            ? goal.TopTier is { } top ? $"tier {tier} of {top}" : $"tier {tier}"
            : "no tier reached yet");

        if (goal.Contributors is { } contributors)
        {
            progress.Add($"{Number(contributors)} contributors");
        }

        if (goal.ContributionsTotal is { } total)
        {
            progress.Add($"{Number(total)} delivered");
        }

        if (goal.IsComplete)
        {
            progress.Add("met");
        }

        report.AppendLine($"  {Capitalise(string.Join(", ", progress))}.");

        if (goal.Reward is { Length: > 0 } reward)
        {
            report.AppendLine($"  Reward: {reward}");
        }

        if (goal.LastUpdate is { } updated)
        {
            report.AppendLine($"  Inara last updated this {Age(updated, at)}.");
        }

        return report.ToString();
    }

    /// <summary>
    /// The deadline, said as a deadline.
    /// <para>
    /// An expired goal has to say so in the same breath as its name. The board snapshot is
    /// routinely days old — the corpus holds one reported four days after the goal ended — so a
    /// finished goal described only by its tier and its totals reads exactly like a live one.
    /// </para>
    /// </summary>
    // Both of these borrow the phrasing the persona already uses for "how long was I away" —
    // the same job, and a second spelling of "about a day" would only be a second thing to keep
    // in step.
    private static string Deadline(DateTimeOffset? expiry, DateTimeOffset at) => expiry switch
    {
        null => "no expiry given",
        { } when expiry <= at => $"ended {Age(expiry.Value, at)}",
        { } when expiry.Value - at > Countdown => $"expires {Day(expiry.Value)}",
        _ => $"{Persona.TelemetryDelta.Spoken(expiry.Value - at)} left",
    };

    private static string Age(DateTimeOffset when, DateTimeOffset at) => (at - when) switch
    {
        { Ticks: <= 0 } => "just now",
        { } since when since > Countdown => $"on {Day(when)}",
        { } since => $"{Persona.TelemetryDelta.Spoken(since)} ago",
    };

    /// <summary>
    /// How far out a countdown is still worth saying as one. A community goal runs for a week or
    /// two, so a span longer than this is not a long goal — it is a source or a clock that should
    /// not be trusted with "eleven days left", and a plain date says so without pretending.
    /// </summary>
    private static readonly TimeSpan Countdown = TimeSpan.FromDays(30);

    private static string Day(DateTimeOffset when) => when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static bool Matches(string title, string? wanted) =>
        wanted is null || title.Contains(wanted, StringComparison.OrdinalIgnoreCase);

    private static bool Same(string one, string other) =>
        string.Equals(one.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
}
