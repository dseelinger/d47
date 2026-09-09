using System.Globalization;
using System.Text;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Community goals: what is running, what tier it has reached, and where the Commander stands (Phase
/// 14, "Know the current community goals").
/// </summary>
public static class CommunityGoalCapability
{
    public const string Id = "community-goals";

    /// <summary>The Commander's own Inara key, in the secret store.</summary>
    public const string KeySecretName = "inara.apiKey";

    public const string KeyRow = "knowledge.inaraKey";

    /// <summary>
    /// <param name="listing"> The external listing, or null where none is composed — under the designer
    /// and in a test that is not about it.
    /// </summary>
    /// <param name="listing">
    /// The external listing, or null where none is composed — under the designer and in a test that is
    /// not about it.
    /// </param>
    /// <param name="now">
    /// Injected, because no Core component reads the clock — and because the whole correctness of this
    /// capability is a comparison against it.
    /// </param>
    /// <param name="ledger">
    /// What the Community Goal commodity has made or lost, folded from the journals (#296).
    /// </param>
    /// <param name="search">The saved search, for which commodity the ledger is asked about.</param>
    /// <param name="settings">Where the Elite week boundary is read from (#332).</param>
    public static CapabilityDescriptor Create(
        Func<CommanderGameState?> commander,
        ICommunityGoalService? listing,
        Func<DateTimeOffset> now,
        CommodityLedger? ledger = null,
        CommunityGoalSearch? search = null,
        Configuration.SettingsService? settings = null) => new()
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
        // Named, because the capability has two answerable tools now (#296): the board is what "community
        // goal" means on its own; the ledger is reached by its own three phrases.
        Keywords =
        [
            new CapabilityKeyword("community goals", "get_community_goals"),
            new CapabilityKeyword("community goal", "get_community_goals"),
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
            new ToolDefinition
            {
                Name = "get_community_goal_earnings",
                Description =
                    "What the Community Goal commodity has made or lost, net of what the cargo cost: "
                    + "this session, today, or this week (the Elite week, turning at the weekly server "
                    + "maintenance).",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "range",
                        Type = ToolParameterType.String,
                        Description = "Which stretch. Default session.",
                        AllowedValues = ["session", "today", "week"],
                    },
                ],

                // The three questions the issue names, matched whole so none swallows a longer sentence, and
                // costing no surface bytes: a command is not part of the schema.
                Commands =
                [
                    Phrase("how have i done today", "today"),
                    Phrase("how have i done this week", "week"),
                    Phrase("how have i done this session", "session"),
                    Phrase("how have i done on the community goal", "week"),
                    Phrase("how am i doing on the community goal", "week"),
                    Phrase("how have i done since the last maintenance window", "week"),
                    Phrase("how have i done since maintenance", "week"),
                    Phrase("how have i done since the last maintenance", "week"),
                    Phrase("how have i done since the tick", "week"),
                ],
                Handler = (arguments, _) =>
                    Task.FromResult(Earnings(commander, ledger, search, settings, now, arguments)),
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
            new SettingRow
            {
                Key = "callouts.weekBoundaryDay",
                Advanced = true,
                Label = "The week turns on",
                Help =
                    "Which day, UTC, \"this week\" turns over on — the Powerplay cycle, the BGS tick "
                    + "and the weekly server maintenance all sit at the same moment. Thursday by "
                    + "default; change it only if Frontier moves theirs.",
                Kind = SettingKind.Choice,
                Choices = Enum.GetNames<DayOfWeek>(),
                DocsAnchor = "week-boundary",
                Binding = new SettingBinding
                {
                    Read = s => s.Callouts.WeekBoundaryDay.ToString(),
                    Write = (s, v) => s with
                    {
                        Callouts = s.Callouts with
                        {
                            WeekBoundaryDay = Enum.TryParse<DayOfWeek>(v, out var day)
                                ? day
                                : s.Callouts.WeekBoundaryDay,
                        },
                    },
                },
            },
            new SettingRow
            {
                Key = "callouts.weekBoundaryHourUtc",
                Advanced = true,
                Label = "…at this hour, UTC",
                Help = "The hour, UTC, the day above turns at. 7 means 07:00 UTC.",
                Kind = SettingKind.Number,
                Minimum = 0,
                Maximum = 23,
                DefaultDisplay = "7",
                DocsAnchor = "week-boundary",
                Binding = new SettingBinding
                {
                    Read = s => s.Callouts.WeekBoundaryHourUtc.ToString(CultureInfo.InvariantCulture),
                    Write = (s, v) => s with
                    {
                        Callouts = s.Callouts with
                        {
                            WeekBoundaryHourUtc = int.TryParse(v, out var hour) && hour is >= 0 and <= 23
                                ? hour
                                : s.Callouts.WeekBoundaryHourUtc,
                        },
                    },
                },
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Community goals", Order = 53 },
    };

    private static ToolCommandPhrase Phrase(string phrase, string range) =>
        new(phrase, new Dictionary<string, string>(StringComparer.Ordinal) { ["range"] = range });

    /// <summary>
    /// The ledger's answer for one stretch (#296): the net figure first, in the words the sale callout
    /// uses, then the sales, tonnes and the two gross sides it is the difference of.
    /// </summary>
    private static ToolResult Earnings(
        Func<CommanderGameState?> commander,
        CommodityLedger? ledger,
        CommunityGoalSearch? search,
        Configuration.SettingsService? settings,
        Func<DateTimeOffset> now,
        ToolArguments arguments)
    {
        if (ledger is null || search is null)
        {
            return ToolResult.Error(
                "Nothing here is keeping a commodity ledger, so I cannot say how the goal has gone.");
        }

        if (commander() is not { } active)
        {
            return ToolResult.Error("No Elite Dangerous journal has been detected yet.");
        }

        var who = active.Identity.FrontierId;
        var commodity = search.Commodity;
        var at = now();

        arguments.TryGetString("range", out var range);

        LedgerTotal total;
        string label;

        // Named once, in the week's own answer, so "this week" says what it means without a settings page —
        // and tracks the configured boundary rather than assuming Thursday (#342).
        string? boundary = null;

        switch (range?.ToLowerInvariant())
        {
            case "today":
                total = ledger.Between(who, commodity, CommodityLedger.Today(at));
                label = "today";
                break;

            case "week":
                var weekBoundaryDay = settings?.Current.Callouts.WeekBoundaryDay ?? DayOfWeek.Thursday;
                var week = CommodityLedger.Week(
                    at,
                    weekBoundaryDay,
                    settings?.Current.Callouts.WeekBoundaryHourUtc ?? 7);
                total = ledger.Between(who, commodity, week);
                label = "this week";
                boundary = $", since {weekBoundaryDay}'s maintenance";
                break;

            default:
                total = ledger.Session(who, commodity);
                label = "this session";
                break;
        }

        if (total.Sales == 0)
        {
            return ToolResult.Ok($"No {commodity} sold {label}.");
        }

        var tonnes = total.Tonnes == 1 ? "1 tonne" : $"{total.Tonnes:N0} tonnes";
        var sales = total.Sales == 1 ? "1 sale" : $"{total.Sales} sales";

        return ToolResult.Ok(
            $"{commodity}: {total.Said} {label}{boundary} — {sales}, {tonnes}, {total.Revenue:N0} cr in "
            + $"against {total.Cost:N0} cr the cargo cost.");
    }

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

                    // Exact name only.
                    .Where(goal => !mine.Any(seen => Same(seen.Title, goal.Name)))
                    .ToList();
            }
            catch (CommunityGoalsUnavailableException ex)
            {
                // Not an error result.
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

    /// <summary>What to say when there is nothing to list.</summary>
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

        // Level rather than fill: "Tier 3 of 5" is the fact, and a tier the goal has not reached yet is
        // absent from the journal rather than reported as zero.
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

        // Said even when nothing else about the Commander is known, because "you have not joined this" and "I
        // have no figures for you" are different answers and the second one is a reason to go and look at the
        // board rather than to skip the goal.
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

    /// <summary>The deadline, said as a deadline.</summary>
    // Both of these borrow the phrasing the persona already uses for "how long was I away" — the same job,
    // and a second spelling of "about a day" would only be a second thing to keep in step.
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

    /// <summary>How far out a countdown is still worth saying as one.</summary>
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
