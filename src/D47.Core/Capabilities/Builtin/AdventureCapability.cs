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

    public const string ChangeDraftTool = "change_adventure";

    public const string AcceptDraftTool = "accept_adventure";

    public const string RejectDraftTool = "reject_adventure";

    public const string AskTool = "ask_for_adventure";

    public const string StoryDownloadsKey = "adventures.storyDownloads";

    public const string StoryRatingsKey = "adventures.storyRatings";

    private const string NoStory = "No story is running.";

    private const string NoDraft = "There is no draft adventure.";

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

    /// <summary>Asks for an adventure and acts on the draft in front of the Commander, each returning a refusal or null. The app sets it once the Adventures page exists.</summary>
    public sealed class AdventureDesk
    {
        public Func<bool> HasDraft { get; set; } = () => false;

        public Func<string?> Change { get; set; } = () => NoDraft;

        public Func<string?> Accept { get; set; } = () => NoDraft;

        public Func<string?> Reject { get; set; } = () => NoDraft;

        /// <summary>Opens the Ask page and the brief entry whose commit presses Go.</summary>
        public Func<string?> Ask { get; set; } = () => "The Adventures page is not available.";
    }

    public static CapabilityDescriptor Create(
        StorySwitch? storySwitch = null, EndingAnswer? endingAnswer = null, BeatRefusal? beatRefusal = null, AdventureDesk? desk = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Adventures",
        Summary =
            "Stories the Commander flies, written by them or by the ship's AI, and advanced by "
            + "their own journal. Driven from the Stories tab; nothing here is callable by the "
            + "model. The Commander can pause and resume a running story by voice, and refuse the objective it is waiting on.",

        // The phrases that genuinely work.
        Examples =
        [
            "show me the adventures",
            "open the adventures tab",
            "pause the story",
            "resume the story",
            "accept the ending",
            "choose ending two",
            "this objective is not for me",
            "change the adventure",
            "accept the adventure",
            "reject the adventure",
            "ask for an adventure",
        ],

        // None.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Adventures", Order = 59, ShowOnPanel = false },

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
        ],

        Tools =
        [
            Switch(PauseTool, "Pause the Commander's running story: no objectives, nudges, clues or story chatter until it is resumed.", false, ["pause the story", "pause my story"], storySwitch),
            Switch(ResumeTool, "Resume the Commander's running story after a pause.", true, ["resume the story", "resume my story"], storySwitch),
            Answer(endingAnswer),
            Refuse(beatRefusal),
            Draft(ChangeDraftTool, "Open the entry for a remark that changes the Commander's draft adventure.", "change the adventure", "What should change?", desk, d => d.Change),
            Draft(AcceptDraftTool, "Accept the Commander's draft adventure, as the Accept button does.", "accept the adventure", "The adventure is accepted.", desk, d => d.Accept),
            Draft(RejectDraftTool, "Reject the Commander's draft adventure and remove it, as the Decline button does.", "reject the adventure", "The draft is removed.", desk, d => d.Reject),
            Desk(AskTool, "Open the Ask page and the entry for a brief; committing the brief asks for the adventure, as Go does.", "ask for an adventure", "What should it be about?", desk, d => d.Ask, _ => null),
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

    private static ToolDefinition Draft(
        string name, string description, string phrase, string done, AdventureDesk? desk, Func<AdventureDesk, Func<string?>> act) =>
        Desk(name, description, phrase, done, desk, act, d => d.HasDraft);

    private static ToolDefinition Desk(
        string name, string description, string phrase, string done, AdventureDesk? desk, Func<AdventureDesk, Func<string?>> act,
        Func<AdventureDesk, Func<bool>?> when)
    {
        desk ??= new AdventureDesk();

        return new ToolDefinition
        {
            Name = name,
            Description = description + " The Commander's choice alone.",
            Commands = [new ToolCommandPhrase(phrase, new Dictionary<string, string>(StringComparer.Ordinal)) { When = when(desk) }],

            // Adventures are the Commander's to ask for and decide; the model is refused.
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
