using D47.Core.Audio;
using D47.Core.Conversation;

namespace D47.Core.Callouts;

/// <summary>
/// What to ask a model for when a callout is being said in character, and what context that
/// question is allowed to carry.
/// </summary>
public sealed record FlavourBrief
{
    /// <summary>The instruction the model is given.</summary>
    public required string Instruction { get; init; }

    /// <summary>
    /// Whether the core aboard's persona block goes with it.
    /// <para>
    /// False for the carrier's two roles, and that is not a detail: handing a Guardian core the
    /// captain's lines would put one of them in two places at once, which is the one thing the
    /// isolation model cannot survive (guardian-personas.md). The brief below supplies its own
    /// description of who is speaking instead.
    /// </para>
    /// </summary>
    public required bool NeedsPersona { get; init; }

    /// <summary>
    /// Whether the live game state goes with it. An ambient remark is about where the Commander
    /// actually is; the carrier's lines are about nothing that has happened.
    /// </summary>
    public required bool NeedsGameState { get; init; }

    /// <summary>
    /// Whether the Commander's own account of themselves goes with it (list.md Phase 43).
    /// <para>
    /// False for the carrier's two roles, for the reason they get <see cref="NeedsPersona"/>
    /// false — a stranger on a comms channel does not know the Commander's history, and putting
    /// one of them in two places at once is the thing the isolation model cannot survive. The two
    /// flags correlate and are deliberately not merged: they answer different questions, <em>whose
    /// voice is this</em> and <em>who is it speaking to</em>.
    /// </para>
    /// </summary>
    public required bool NeedsAboutMe { get; init; }

    /// <summary>
    /// Whether the story goes as well as the character sheet. Only meaningful with
    /// <see cref="NeedsAboutMe"/>; the sheet is some forty tokens and always goes, the story is
    /// thirteen hundred and goes one call in <see cref="Conversation.CommanderStory.StoryEvery"/>
    /// — chosen by the index on the announcement and never by a clock.
    /// </summary>
    public bool NeedsStory { get; init; }

    /// <summary>
    /// The character brief for a speaker who is not the ship's AI, or null when
    /// <see cref="NeedsPersona"/> supplies one instead. Occupies the same slot in the prompt.
    /// </summary>
    public string? Speaker { get; init; }
}

/// <summary>
/// Which callouts get said in character, and what the model is asked (list.md Phase 11, "with
/// varied LLM arrival and departure responses").
/// <para>
/// Only the ambient remarks, the session's opening line and the carrier's own lines. <b>A danger
/// callout is never rewritten by a model</b>: those fire on the event and say exactly what
/// happened, and "shields are down"
/// is not a line that benefits from personality (list.md Phase 8). That exclusion is the whole
/// safety property here, and it was expressed as the default arm of a switch inside
/// <c>AppHost.VaryAsync</c> — where nothing could assert it, and where adding a case above it
/// would have been the entire cost of losing it.
/// </para>
/// <para>
/// Pure. The root still resolves the persona block, reads the live game state and makes the call;
/// what was lifted is which line is eligible and what it is asked (list.md Phase 19).
/// </para>
/// </summary>
public static class FlavourBriefs
{
    /// <summary>
    /// What a core is asked when it introduces itself, or null when the authored line is to be
    /// said exactly as written.
    /// <para>
    /// <b>The authored intro is a sample rather than a script.</b> <c>guardian-personas.md</c>
    /// files these under <em>sample lines, not intended to be verbatim</em> — they demonstrate
    /// how a core sounds, and reading one out word for word means every Commander who ever picks
    /// Warden hears the same paragraph every time.
    /// </para>
    /// <para>
    /// A rewording brief and never a composition one, the same call the opening line makes: the
    /// substance is the persona pack's and only the sentences are the model's. A core's first
    /// words carry things a Commander needs — Cora states the arrangement and asks them to
    /// confirm it — and a model asked to improvise a greeting would lose them.
    /// </para>
    /// <para>
    /// Here rather than at the call site for the reason the rest of this file is here: the
    /// decision about what d47 is asked used to live inside the composition root, where nothing
    /// could assert it.
    /// </para>
    /// </summary>
    /// <param name="intro">The core's authored first line, which is also the fallback.</param>
    public static FlavourBrief? Introducing(string? intro, bool personalityEnabled)
    {
        // Personality off means said exactly as written, which is the same rule the announcements
        // below follow — and with nothing authored there is nothing to reword.
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

            // No game state. A core's first line is about itself, and where the Commander happens
            // to be is how a model comes to open with the wrong subject.
            NeedsGameState = false,

            // The sheet, so the first words are addressed to somebody — a name, not "Commander"
            // every time. Not the story: the instruction says to add nothing, and it is a
            // rewording.
            NeedsAboutMe = true,
        };
    }

    /// <summary>
    /// The brief for one announcement, or null when it is to be said exactly as written.
    /// </summary>
    /// <param name="personalityEnabled">
    /// Personality off silences all of it. The checklist puts "no ambient remarks" in that item's
    /// own acceptance criteria, and a carrier captain improvising is personality by any reading.
    /// </param>
    public static FlavourBrief? For(Announcement announcement, bool personalityEnabled)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (!personalityEnabled)
        {
            return null;
        }

        if (announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Instruction =
                    "Make one short unprompted remark about where the Commander is right now — you are "
                    + $"{AmbientLines.Describe(SituationOf(announcement.Key))}. Nothing has happened; this is you "
                    + "filling a quiet moment in character. One or two sentences. Do not ask a question, "
                    + "do not offer help, and do not comment on the Commander's decisions.",
                NeedsPersona = true,
                NeedsGameState = true,

                // The only brief that ever carries the story, because it is the only one written
                // often enough for "occasionally" to mean anything — and the one where it pays:
                // a remark about a docking bay that means something to this Commander is the
                // difference between company and ten ways to say nothing. The index is the
                // callout's own, the one it picked the stock line with.
                NeedsAboutMe = true,
                NeedsStory = CommanderStory.TellsStory(announcement.Variant),
            };
        }

        // A beat of the Commander's adventure, or its opening (list.md Phase 47, "The ship's AI tells
        // it, and the authored beat is the floor"). A rewording brief: the authored line carries
        // what the beat has to say and the model says it in the core's voice. The story itself —
        // premise, stake, what has fired — rides in the game state below the breakpoint, which is
        // why this one needs it where the continuity line does not.
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

        // Phase 31's opening line. Eligible for the same reason the ambient remarks are: it is d47
        // filling a moment rather than reporting an event, and it is the first thing a Commander
        // hears in a session — which is precisely where a core sounding like itself is worth
        // something.
        //
        // The facts are handed over already written and the instruction says not to add any. A
        // model given a session to comment on embellishes, and the whole quality bar of the memory
        // store is that nothing in it is stated more confidently than it arrived — so this is a
        // rewording brief like the carrier's, never a composition brief like the ambient one.
        if (string.Equals(announcement.Key, ContinuityCallout.Key, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Instruction =
                    "The Commander has just sat down. Greet them once, in your own voice, from this: "
                    + $"\"{announcement.Text}\" Keep the first sentence's time of day. Finish the second "
                    + "sentence — \"Ready to …\" — in character, in a few words of your own. Two short "
                    + "sentences, nothing else: no facts, no questions, no remarks about the ship or "
                    + "the list.",
                NeedsPersona = true,

                // No game state. The line is about what was true before this session, and handing
                // over where they are now is how a model comes to write about the wrong one.
                NeedsGameState = false,

                // The sheet: a greeting is the one line where knowing whose ship this is matters
                // most. Not the story — "no facts" is in the instruction.
                NeedsAboutMe = true,
            };
        }

        if (announcement.Voice is VoiceRole.CarrierCaptain or VoiceRole.TowerControl)
        {
            return new FlavourBrief
            {
                Speaker =
                    $"You are {(announcement.Voice == VoiceRole.CarrierCaptain
                        ? "the captain of the Commander's fleet carrier"
                        : "the tower controller aboard the Commander's fleet carrier")}. You are a "
                    + "professional, not a character — brief, competent and human. One short sentence. "
                    + "Never mention being an AI.",
                Instruction = $"Say this in your own words, once: \"{announcement.Text}\"",
                NeedsPersona = false,
                NeedsGameState = false,

                // A stranger on a comms channel. They do not know the Commander's history, and a
                // tower that did would be the ship's AI wearing a different hat.
                NeedsAboutMe = false,
            };
        }

        return null;
    }

    /// <summary>
    /// Which situation an ambient announcement was about, from its key. Carried on the key rather
    /// than read back off the callout, because a batch may hold more than one and the callout
    /// only remembers the last.
    /// </summary>
    public static AmbientSituation SituationOf(string key) =>
        Enum.TryParse<AmbientSituation>(
            key[AmbientCallout.KeyPrefix.Length..], ignoreCase: true, out var situation)
            ? situation
            : AmbientSituation.None;
}
