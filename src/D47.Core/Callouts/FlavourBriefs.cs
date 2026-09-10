using D47.Core.Audio;
using D47.Core.Conversation;

namespace D47.Core.Callouts;

/// <summary>
/// What to ask a model for when a callout is being said in character, and what context that question is
/// allowed to carry.
/// </summary>
public sealed record FlavourBrief
{
    /// <summary>The instruction the model is given.</summary>
    public required string Instruction { get; init; }

    /// <summary>Whether the core aboard's persona block goes with it.</summary>
    public required bool NeedsPersona { get; init; }

    /// <summary>Whether the live game state goes with it.</summary>
    public required bool NeedsGameState { get; init; }

    /// <summary>Whether the Commander's own account of themselves goes with it (Phase 43).</summary>
    public required bool NeedsAboutMe { get; init; }

    /// <summary>Whether the story goes as well as the character sheet.</summary>
    public bool NeedsStory { get; init; }

    /// <summary>
    /// The character brief for a speaker who is not the ship's AI, or null when <see
    /// cref="NeedsPersona"/> supplies one instead.
    /// </summary>
    public string? Speaker { get; init; }
}

/// <summary>
/// Which callouts get said in character, and what the model is asked (Phase 11, "with varied LLM
/// arrival and departure responses").
/// </summary>
public static class FlavourBriefs
{
    /// <summary>
    /// What a core is asked when it introduces itself, or null when the authored line is to be said
    /// exactly as written.
    /// </summary>
    /// <param name="intro">The core's authored first line, which is also the fallback.</param>
    public static FlavourBrief? Introducing(string? intro, bool personalityEnabled)
    {
        // Personality off means said exactly as written, which is the same rule the announcements below
        // follow — and with nothing authored there is nothing to reword.
        if (!personalityEnabled || string.IsNullOrWhiteSpace(intro))
        {
            return null;
        }

        return new FlavourBrief
        {
            Instruction =
                "You have just been switched on and the Commander is meeting you for the first "
                + "time. This is how you would introduce yourself, as an example of your voice "
                + $"rather than a script: \"{intro}\" Say it again in your own words — the same "
                + "substance and the same things said, not the same sentences. Do not greet them "
                + "formally, do not offer a list of what you can do, and add nothing you were not "
                + "given.",
            NeedsPersona = true,

            // No game state.
            NeedsGameState = false,

            // The sheet, so the first words are addressed to somebody — a name, not "Commander" every time.
            NeedsAboutMe = true,
        };
    }

    /// <summary>The brief for one announcement, or null when it is to be said exactly as written.</summary>
    /// <param name="personalityEnabled">Personality off silences all of it.</param>
    public static FlavourBrief? For(Announcement announcement, bool personalityEnabled)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (!personalityEnabled)
        {
            return null;
        }

        // **Somebody else's words are never handed to a model.** `IncomingMessages` says of itself that "none
        // of it ever reaches the model … there is deliberately no path from here into a prompt", and until
        // this line there was one: a message from the Commander's own carrier is assigned
        // VoiceRole.TowerControl, and the carrier brief below is chosen by role — so an in-game transmission
        // was being interpolated into an instruction inside quotes.
        if ((announcement.CommsChannel is not null || announcement.Transcript is not null)
            && announcement.Key is not (IncomingMessages.CarrierCannedKey or IncomingMessages.AuthorityCannedKey))
        {
            return null;
        }

        if (announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal))
        {
            // **Spoken to the Commander, never narrated** (#222).
            return new FlavourBrief
            {
                Instruction =
                    "Say one short idle thing to the Commander while you are "
                    + $"{AmbientLines.Describe(SituationOf(announcement.Key))}. Nothing has happened; "
                    + "this is you filling a quiet moment in character, speaking to the Commander — "
                    + "not describing the view. No scene-setting, no telling them what hangs where, "
                    + "no atmosphere. One or two sentences. Do not ask a question, do not offer "
                    + "help, and do not comment on the Commander's decisions. This authored line "
                    + "has the register wanted — compose your own line in that voice rather than "
                    + $"rewording it: \"{announcement.Text}\"",
                NeedsPersona = true,
                NeedsGameState = true,

                // The only brief that ever carries the story, because it is the only one written often enough
                // for "occasionally" to mean anything — and the one where it pays: a remark about a docking
                // bay that means something to this Commander is the difference between company and ten ways
                // to say nothing.
                NeedsAboutMe = true,
                NeedsStory = CommanderStory.TellsStory(announcement.Variant),
            };
        }

        // A beat of the Commander's adventure, or its opening (Phase 47, "The ship's AI tells it, and the
        // authored beat is the floor").
        if (announcement.Key.StartsWith(Adventures.AdventureCallout.KeyPrefix, StringComparison.Ordinal))
        {
            var opening = announcement.Variant < 0;

            return new FlavourBrief
            {
                Instruction =
                    (opening
                        ? "The Commander has just agreed to hear a story you are telling, and this is how it "
                          + "opens. "
                        : "The Commander has just reached a beat of the story you are telling them. ")
                    + "Say this in your own voice, keeping every fact, name and number in it and adding none: "
                    + $"\"{announcement.Text}\" Show the place and what is in it; never tell the Commander "
                    + "what they feel. Two to four sentences, spoken in a cockpit. If the text ends by saying "
                    + "where the Commander goes next, end by saying the same place and the same act in your own "
                    + "words — that is the only thing you say about what is ahead; do not guess at what is "
                    + "there or why, and never send the Commander to find or speak to anyone.",
                NeedsPersona = true,
                NeedsGameState = true,
                NeedsAboutMe = true,
            };
        }

        // Somebody has been paid to hunt the Commander, and d47 reacts to it in character (<a
        // href=".com/dseelinger/d47/issues/137">#137</a>).
        if (string.Equals(announcement.Key, AnnouncedAttackCallout.HuntedKey, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Instruction =
                    "Someone has been hunting the Commander and has just been heard on the radio "
                    + "looking for them. Say this once, in your own voice, keeping what it means: "
                    + $"\"{announcement.Text}\" One or two sentences. Do not say who they are, why "
                    + "they are there or who sent them — you do not know, and guessing would be "
                    + "inventing it. Do not tell the Commander what to do and do not ask a question.",
                NeedsPersona = true,

                // Where they are is the whole of what makes this worth remarking on: being hunted in a busy
                // system and being hunted alone are different situations.
                NeedsGameState = true,

                // The sheet, so it is addressed to somebody.
                NeedsAboutMe = true,
            };
        }

        // Phase 31's opening line.
        if (string.Equals(announcement.Key, ContinuityCallout.Key, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Instruction =
                    "The Commander has just sat down. Greet them once, in your own voice, from this: "
                    + $"\"{announcement.Text}\" Keep the first sentence's time of day. Finish the second "
                    + "sentence — \"Ready to …\" — in a few words of your own, plainly, without reaching "
                    + "for a synonym of \"ready\" dressed up in your usual words. Say what you are ready "
                    + "for only if something real makes it worth naming — what is aboard, what the last "
                    + "session ended with. Otherwise leave it at \"Ready to go.\" Two short sentences, "
                    + "nothing else: no facts, no questions, no remarks about the ship or the list.",
                NeedsPersona = true,

                // No game state.
                NeedsGameState = false,

                // The sheet: a greeting is the one line where knowing whose ship this is matters most.
                NeedsAboutMe = true,
            };
        }

        // A System Authority vessel's canned line, spoken while the Commander shares a system with their own
        // carrier (#248's second half).
        if (string.Equals(announcement.Key, IncomingMessages.AuthorityCannedKey, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Speaker =
                    "You are the officer on watch aboard a System Authority vessel patrolling "
                    + "the space around a privately owned fleet carrier. Professional law "
                    + "enforcement — brief, courteous, watchful. One short sentence. Never "
                    + "mention being an AI.",
                Instruction =
                    "The pilot in your patrol area owns the fleet carrier you are covering. Say "
                    + "this in your own words, once: keep every fact, add none, and use no name "
                    + "you were not given. The canned attitude is yours to replace — a scan that "
                    + "found nothing is said courteously, not as a taunt. Courteous to the owner "
                    + "whose assets you are here to protect — you are not subordinate to them, "
                    + "and the law is still the law: "
                    + $"\"{announcement.Text}\"",
                NeedsPersona = false,
                NeedsGameState = false,
                NeedsAboutMe = false,
            };
        }

        if (announcement.Voice is VoiceRole.CarrierCaptain or VoiceRole.TowerControl)
        {
            var speaker =
                $"You are {(announcement.Voice == VoiceRole.CarrierCaptain
                    ? "the captain of the Commander's fleet carrier"
                    : "the tower controller aboard the Commander's fleet carrier")}. You are a "
                + "professional, not a character — brief, competent and human. One short sentence. "
                + "Never mention being an AI.";

            // The docked exchange composes rather than rewords (#220). "Drinks are on me" and "I'll meet you
            // in the hangar" are not rewordings of each other — they are different things a captain says in
            // the same situation — so these two name a situation and a range instead of a sentence to
            // paraphrase.
            if (announcement.Key is CarrierCallout.SecuredKey or CarrierCallout.HomeKey)
            {
                return new FlavourBrief
                {
                    Speaker = speaker,
                    Instruction = announcement.Key == CarrierCallout.SecuredKey
                        ? "The owner of this carrier has just set their ship down on the deck. "
                          + "Acknowledge it in one short sentence: the moment is the ship being "
                          + "secured — do not say docking was granted, that happened minutes ago "
                          + "and was already announced. Invent no facts: no crew names, no deck "
                          + "reports, no events. Address the owner exactly as this authored line "
                          + "does, and treat it as a register sample rather than a script: "
                          + $"\"{announcement.Text}\""
                        : "The owner of this carrier has just docked aboard, and the tower has "
                          + "acknowledged their ship secured. Say one short welcoming thing a "
                          + "captain might say to the owner — hospitality in your own words, "
                          + "different each time: meeting them below, something waiting for them, "
                          + "plain gladness the ship is back. Invent no facts: no named crew, no "
                          + "events aboard, no reports and no promises about the ship. Address "
                          + "the owner exactly as this authored line does, and treat it as a "
                          + "register sample rather than a script: "
                          + $"\"{announcement.Text}\"",
                    NeedsPersona = false,
                    NeedsGameState = false,
                    NeedsAboutMe = false,
                };
            }

            // A canned line Elite sent from the Commander's own carrier (#248): Frontier's string, allowed
            // near a model precisely because no player wrote it — see IncomingMessages, where the $-key is
            // what proves that.
            if (string.Equals(announcement.Key, IncomingMessages.CarrierCannedKey, StringComparison.Ordinal))
            {
                return new FlavourBrief
                {
                    Speaker = speaker,
                    Instruction =
                        "Your station just addressed its own owner with this canned line. Say it "
                        + "in your own words, once, to the owner of this carrier — not a visiting "
                        + "pilot: keep every fact in it, add none, and give them the respect the "
                        + "deck they own is owed. Address them by rank and surname alone — "
                        + "\"Commander\" and the last word of their name, never the full name: "
                        + $"\"{announcement.Text}\"",
                    NeedsPersona = false,
                    NeedsGameState = false,
                    NeedsAboutMe = false,
                };
            }

            return new FlavourBrief
            {
                Speaker = speaker,
                Instruction = $"Say this in your own words, once: \"{announcement.Text}\"",
                NeedsPersona = false,
                NeedsGameState = false,

                // A stranger on a comms channel.
                NeedsAboutMe = false,
            };
        }

        return null;
    }

    /// <summary>
    /// Phrases that mean a model has answered a rewording brief by talking about itself (GitHub issue
    /// 46).
    /// </summary>
    private static readonly string[] TalkingAboutItself =
    [
        "i don't have that capabilit",
        "i do not have that capabilit",
        "i don't have the capabilit",
        "i do not have the capabilit",
        "i don't have a tool",
        "i do not have a tool",
        "i don't have any tool",
        "i have no tool",
        "i'm not able to do that",
        "i am not able to do that",
        "i'm unable to do that",
        "i am unable to do that",
        "i can't do that",
        "i cannot do that",
        "as an ai",
        "as a language model",
        "i don't have access to",
        "i do not have access to",

        // The 2026-08-25 pair, kept even though the input that produced them can no longer reach a brief.
        "system rules",
        "operating parameters",
        "system prompt",

        // **2026-08-26, and it is the shape this list's own summary said it would miss.** A Commander's
        // carrier captain was handed *"Commander inbound."* to reword and said: "I appreciate the test, but I
        // need to decline.
        "i need to decline",
        "i have to decline",
        "i must decline",
        "mine to explain",
        "mine to restate",
        "i appreciate the test",
        "those rules",
    ];

    /// <summary>Whether a model's answer to a rewording brief may be spoken (GitHub issue 46).</summary>
    public static bool MayBeSpoken(string? line) =>
        !string.IsNullOrWhiteSpace(line)
        && !TalkingAboutItself.Any(said => line.Contains(said, StringComparison.OrdinalIgnoreCase));

    /// <summary>Which situation an ambient announcement was about, from its key.</summary>
    public static AmbientSituation SituationOf(string key) =>
        Enum.TryParse<AmbientSituation>(
            key[AmbientCallout.KeyPrefix.Length..], ignoreCase: true, out var situation)
            ? situation
            : AmbientSituation.None;
}
