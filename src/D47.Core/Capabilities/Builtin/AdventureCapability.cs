using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Adventures — stories the Commander flies, told by the ship's AI (Phase 47).</summary>
public static class AdventureCapability
{
    public const string Id = "adventures";

    public const string PauseTool = "pause_story";

    public const string ResumeTool = "resume_story";

    public const string AnswerEndingTool = "answer_story_ending";

    public const string RefuseBeatTool = "refuse_story_beat";

    public const string ChangeObjectiveTool = "change_story_objective";

    public const string ChangeDraftTool = "change_adventure";

    public const string AcceptDraftTool = "accept_adventure";

    public const string RejectDraftTool = "reject_adventure";

    public const string AskTool = "ask_for_adventure";

    public const string RecapTool = "recap_story";

    public const string StoryDownloadsKey = "adventures.storyDownloads";

    public const string StoryRatingsKey = "adventures.storyRatings";

    public const string CastReactionsKey = "adventures.castReactions";

    public const string RatingKeyPrefix = "adventures.rating.";

    private const string RatingsGroup = "What you like doing";

    private const string NoStory = "No story is running.";

    private const string NoDraft = "There is no draft short story.";

    /// <summary>Switches the Commander's story on or off, returning a refusal or null. The app sets it once the story exists.</summary>
    public sealed class StorySwitch
    {
        public Func<bool, string?> Set { get; set; } = _ => NoStory;
    }

    /// <summary>Answers a finished story's ending by the option's position, one from the first, or null when there is one option.</summary>
    public sealed class EndingAnswer
    {
        public Func<int?, StoryAnswer> Answer { get; set; } = _ => StoryAnswer.Refused("No ending is waiting for an answer.");
    }

    /// <summary>Replaces the beat the story is waiting on, returning a refusal or null. The app sets it once the story exists.</summary>
    public sealed class BeatRefusal
    {
        public Func<CancellationToken, Task<string?>> Refuse { get; set; } = _ => Task.FromResult<string?>(NoStory);
    }

    /// <summary>Asks for an adventure, acts on the draft in front of the Commander and opens the entry for a different objective, each returning a refusal or null. The app sets it once the Adventures page exists.</summary>
    public sealed class AdventureDesk
    {
        public Func<bool> HasDraft { get; set; } = () => false;

        public Func<string?> Change { get; set; } = () => NoDraft;

        public Func<string?> Accept { get; set; } = () => NoDraft;

        public Func<string?> Reject { get; set; } = () => NoDraft;

        /// <summary>Opens the Short Story page and the brief entry whose commit presses Go.</summary>
        public Func<string?> Ask { get; set; } = () => "The Short Story page is not available.";

        /// <summary>Opens the entry that takes the Commander's words for a different objective.</summary>
        public Func<string?> ChangeObjective { get; set; } = () => NoStory;
    }

    /// <summary>Writes the Narrator's recap of the story under way, or null when no story is under way. The app sets it once the story exists.</summary>
    public sealed class StoryRecapper
    {
        public Func<CancellationToken, Task<string?>> Write { get; set; } = _ => Task.FromResult<string?>(null);
    }

    private sealed record Rating(
        string Key,
        string Label,
        string Spoken,
        string Anchor,
        Func<CareerRatingSettings, int> Read,
        Func<CareerRatingSettings, int, CareerRatingSettings> Write,
        string? Note = null);

    private static readonly Rating[] Ratings =
    [
        new("combat", "Combat", "combat", "rating-combat", r => r.Combat, (r, v) => r with { Combat = v }),
        new("trading", "Trading", "trading", "rating-trading", r => r.Trading, (r, v) => r with { Trading = v }),
        new("mining", "Mining", "mining", "rating-mining", r => r.Mining, (r, v) => r with { Mining = v }),
        new("exploration", "Exploration", "exploration", "rating-exploration", r => r.Exploration, (r, v) => r with { Exploration = v }),
        new("exobiology", "Exobiology", "exobiology", "rating-exobiology", r => r.Exobiology, (r, v) => r with { Exobiology = v }),
        new("passengers", "Passengers", "passengers", "rating-passengers", r => r.Passengers, (r, v) => r with { Passengers = v }),
        new("searchAndRescue", "Search and rescue", "search and rescue", "rating-search-and-rescue", r => r.SearchAndRescue, (r, v) => r with { SearchAndRescue = v }),
        new(
            "crime",
            "Crime",
            "crime",
            "rating-crime",
            r => r.Crime,
            (r, v) => r with { Crime = v },
            "This rating has no effect until Frontier's Law and Disorder update."),
    ];

    private static SettingRow CareerRatingRow(Rating rating) => new()
    {
        Key = RatingKeyPrefix + rating.Key,
        Group = RatingsGroup,
        GroupHelp = "How much you like each kind of play, from 1 to 100. Story chapters ask for the activities you rate higher more often.",
        Label = rating.Label,
        Help = $"How much you like {rating.Spoken}, from 1 to 100. 50 is neutral. Say \"set my {rating.Spoken} rating to 80\" or \"rate {rating.Spoken} 30\". "
               + (rating.Note ?? "A higher rating makes story chapters ask for it more often."),
        Kind = SettingKind.Number,
        DefaultDisplay = "50",
        Note = _ => $"How much you like {rating.Spoken}, 1 to 100. Story chapters ask for what you rate higher more often."
                    + (rating.Note is null ? string.Empty : " " + rating.Note),
        Minimum = 1,
        Maximum = 100,
        DocsAnchor = rating.Anchor,
        Scope = SettingScope.Commander,

        // A rating steers what the story asks of the Commander; a model may not set it.
        Protected = true,
        ValuesBySound = said => RatingHeard(said, rating.Spoken),
        Refuses = (_, value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 100
            ? null
            : $"A {rating.Spoken} rating is from 1 to 100.",
        Binding = new SettingBinding
        {
            Read = s => rating.Read(s.CareerRatings).ToString(CultureInfo.InvariantCulture),
            Write = (s, v) => s with
            {
                CareerRatings = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 100
                    ? rating.Write(s.CareerRatings, number)
                    : rating.Write(s.CareerRatings, CareerRatingSettings.Default),
            },
        },
    };

    /// <summary>The rating named in "set my mining rating to 80" or "rate mining 80", or none.</summary>
    internal static ValuesHeard RatingHeard(string said, string spoken)
    {
        var name = Regex.Escape(spoken);

        var match = Regex.Match(
            said.Trim(),
            $@"^(?:set (?:my |the )?{name} rating (?:to |at )?|rate {name} (?:to |at )?)(?<n>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success && NumberIn(match.Groups["n"].Value) is { } number
            ? new ValuesHeard([number.ToString(CultureInfo.InvariantCulture)], Certain: true)
            : ValuesHeard.None;
    }

    private static readonly string[] Units =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
            "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];

    private static readonly string[] Tens =
        ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>A whole number from 0 to 999 in digits or words, or null.</summary>
    private static int? NumberIn(string text)
    {
        var words = text.Trim().ToLowerInvariant().Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 1 && int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out var digits))
        {
            return digits;
        }

        var total = 0;
        var seen = false;

        foreach (var word in words)
        {
            if (Array.IndexOf(Units, word) is var unit and >= 0)
            {
                total += unit;
            }
            else if (Array.IndexOf(Tens, word) is var ten and >= 2)
            {
                total += ten * 10;
            }
            else if (word is "hundred")
            {
                total = (seen ? total : 1) * 100;
            }
            else if (!(word is "a" && !seen))
            {
                return null;
            }

            seen = true;
        }

        return seen ? total : null;
    }

    public static CapabilityDescriptor Create(
        StorySwitch? storySwitch = null,
        EndingAnswer? endingAnswer = null,
        BeatRefusal? beatRefusal = null,
        AdventureDesk? desk = null,
        StoryRecapper? recapper = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Stories",
        Summary =
            "Stories the Commander flies, advanced by "
            + "their own journal. Driven from the Stories tab; nothing here is callable by the "
            + "model. The Commander can pause and resume a running story by voice, refuse the objective it is waiting on or change it in their own words, and "
            + "ask the Narrator for the story so far.",

        // The phrases that genuinely work.
        Examples =
        [
            "show me the stories",
            "open the stories tab",
            "pause the story",
            "resume the story",
            "where am I up to",
            "recap the story",
            "what's happened in the story so far",
            "accept the ending",
            "choose ending two",
            "this objective is not for me",
            "never ask this",
            "change this objective",
            "ask for a short story",
            "change the short story",
            "accept the short story",
            "reject the short story",
        ],

        // None.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Stories", Order = 59, ShowOnPanel = false },

        Settings =
        [
            new SettingRow
            {
                Key = StoryDownloadsKey,
                Label = "Download stock stories",
                Help =
                    "Stock stories are published as files on a GitHub release, the one the app updates itself from. "
                    + "D47 fetches the list and the cast pictures it shows when the Stories page first opens in a session, "
                    + "and a story's hidden layer and every cast picture when you open its page, and keeps them in data\\stories.\n\n"
                    + "Off, nothing is fetched and the Stories page lists only stories already on disk.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "download-stock-stories",
                EgressId = EgressDisclosure.StockStories,
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.StoryDownloads ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with { StoryDownloads = bool.TryParse(v, out var on) && on },
                    },
                },
            },
            new SettingRow
            {
                Key = StoryRatingsKey,
                Label = "Story ratings",
                Help =
                    "D47 fetches every stock story's average rating when the Stories page first opens in a session, "
                    + "and sends your stars when you rate a story you have picked. A vote carries the story, your stars "
                    + "and a random number made on this PC for your Commander. Off, nothing is fetched or sent and the "
                    + "Stories page shows no ratings.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "story-ratings",
                EgressId = EgressDisclosure.StoryRatings,
                Binding = new SettingBinding
                {
                    Read = s => s.Ui.StoryRatings ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Ui = s.Ui with { StoryRatings = bool.TryParse(v, out var on) && on },
                    },
                },
            },
            new SettingRow
            {
                Key = CastReactionsKey,
                Label = "Cast reactions",
                Help =
                    "While a stock story runs, one of its main characters says one line about something you did, such as "
                    + "a death, an interdiction, a promotion or your largest sale, the next time you are docked in your ship. "
                    + "At most one a play session. Written by the model; with no model, or personality off, there are none. "
                    + "Off, no story's cast reacts. Clues and chapter lines are unchanged.",
                Kind = SettingKind.Toggle,
                DocsAnchor = "cast-reactions",
                Binding = new SettingBinding
                {
                    Read = s => s.Callouts.CastReactions ? "true" : "false",
                    Write = (s, v) => s with
                    {
                        Callouts = s.Callouts with { CastReactions = bool.TryParse(v, out var on) && on },
                    },
                },
            },
            .. Ratings.Select(CareerRatingRow),
        ],

        Tools =
        [
            Switch(PauseTool, "Pause the Commander's running story: no objectives, nudges, clues or story chatter until it is resumed.", false, ["pause the story", "pause my story"], storySwitch),
            Switch(ResumeTool, "Resume the Commander's running story after a pause.", true, ["resume the story", "resume my story"], storySwitch),
            Answer(endingAnswer),
            Refuse(beatRefusal),
            Desk(ChangeObjectiveTool, "Open the entry where the Commander says what they would rather do instead of the story chapter's current objective; the core then proposes a replacement for them to use, keep or reword.", ["change this objective", "change the objective"], "What would you rather do?", desk, d => d.ChangeObjective, _ => null),
            Recap(recapper),
            Draft(ChangeDraftTool, "Open the entry for a remark that changes the Commander's draft short story.", ["change the short story", "change the adventure"], "What should change?", desk, d => d.Change),
            Draft(AcceptDraftTool, "Accept the Commander's draft short story, as the Accept button does.", ["accept the short story", "accept the adventure"], "The short story is accepted.", desk, d => d.Accept),
            Draft(RejectDraftTool, "Reject the Commander's draft short story and remove it, as the Decline button does.", ["reject the short story", "reject the adventure"], "The draft is removed.", desk, d => d.Reject),
            Desk(AskTool, "Open the Short Story page and the entry for a brief; committing the brief asks for the short story, as Go does.", ["ask for a short story", "ask for an adventure"], "What should it be about?", desk, d => d.Ask, _ => null),
        ],
    };

    private static ToolDefinition Answer(EndingAnswer? endingAnswer) => new()
    {
        Name = AnswerEndingTool,
        Description = "Answer the ending of a finished story with one of its options, numbered from one. The Commander's choice alone.",
        Parameters =
        [
            new ToolParameter
            {
                Name = "option",
                Type = ToolParameterType.Integer,
                Description = "The option's position in the ending message, from one. Leave out when the ending has one option.",
            },
        ],
        Commands =
        [
            new ToolCommandPhrase("accept the ending", new Dictionary<string, string>(StringComparer.Ordinal)),
            .. new[] { "one", "two", "three", "four" }.Select((word, index) => new ToolCommandPhrase(
                $"choose ending {word}",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["option"] = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) })),
        ],

        // The ending is the Commander's; the model is refused.
        Protected = true,
        Handler = (arguments, _) =>
        {
            int? option = arguments.TryGetInt32("option", out var number) ? number : null;
            var answer = endingAnswer?.Answer(option) ?? StoryAnswer.Refused("No ending is waiting for an answer.");

            return Task.FromResult(answer.Refusal is { } refusal ? ToolResult.Error(refusal) : ToolResult.Ok("Your answer is recorded."));
        },
    };

    private static ToolDefinition Refuse(BeatRefusal? beatRefusal) => new()
    {
        Name = RefuseBeatTool,
        Description =
            "Refuse the objective the Commander's story chapter is waiting on and write a different one in its place. The story remembers "
            + "the activity and does not ask for it again. The Commander's choice alone.",
        Commands =
        [
            new ToolCommandPhrase("this objective is not for me", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("never ask this", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("not for me", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("give me a different objective", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("this beat is not for me", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("give me a different beat", new Dictionary<string, string>(StringComparer.Ordinal)),
        ],

        // Skipping a beat is the Commander's decision; the model is refused.
        Protected = true,
        Handler = async (_, cancellationToken) =>
            await (beatRefusal?.Refuse(cancellationToken) ?? Task.FromResult<string?>(NoStory)).ConfigureAwait(false) is { } refusal
                ? ToolResult.Error(refusal)
                : ToolResult.Ok("That objective is replaced, and the story will not ask for it again."),
    };

    private static ToolDefinition Recap(StoryRecapper? recapper) => new()
    {
        Name = RecapTool,
        Description =
            "Have the Narrator recap the story under way: what has happened so far and where it waits now. The Commander's choice alone.",
        Commands =
        [
            new ToolCommandPhrase("where am I up to", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("recap the story", new Dictionary<string, string>(StringComparer.Ordinal)),
            new ToolCommandPhrase("what's happened in the story so far", new Dictionary<string, string>(StringComparer.Ordinal)),
        ],

        // The recap is the Commander's request alone; the model is refused, so the core cannot start the Narrator talking.
        Protected = true,
        Handler = async (_, cancellationToken) =>
            recapper is not null && await recapper.Write(cancellationToken).ConfigureAwait(false) is { } said
                ? ToolResult.Ok(said) with { SpokenBy = new ToolSpeaker(Audio.VoiceRole.Narrator, Persona.NarratorLine.Name) }
                : ToolResult.Error(StoryRecap.NoStory),
    };

    private static ToolDefinition Draft(
        string name, string description, string[] phrases, string done, AdventureDesk? desk, Func<AdventureDesk, Func<string?>> act) =>
        Desk(name, description, phrases, done, desk, act, d => d.HasDraft);

    private static ToolDefinition Desk(
        string name, string description, string[] phrases, string done, AdventureDesk? desk, Func<AdventureDesk, Func<string?>> act,
        Func<AdventureDesk, Func<bool>?> when)
    {
        desk ??= new AdventureDesk();

        return new ToolDefinition
        {
            Name = name,
            Description = description + " The Commander's choice alone.",
            Commands = [.. phrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string>(StringComparer.Ordinal)) { When = when(desk) })],

            // Short stories are the Commander's to ask for and decide; the model is refused.
            Protected = true,
            Handler = (_, _) => Task.FromResult(
                act(desk)() is { } refusal ? ToolResult.Error(refusal) : ToolResult.Ok(done)),
        };
    }

    private static ToolDefinition Switch(string name, string description, bool on, string[] phrases, StorySwitch? storySwitch) => new()
    {
        Name = name,
        Description = description,
        Commands = [.. phrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string>(StringComparer.Ordinal)))],

        // The Commander's act; the model is refused.
        Protected = true,
        Handler = (_, _) => Task.FromResult(
            (storySwitch?.Set(on) ?? NoStory) is { } refusal
                ? ToolResult.Error(refusal)
                : ToolResult.Ok(on ? "The story is back on." : "The story is paused. Say 'resume the story' when you want it back.")),
    };
}
