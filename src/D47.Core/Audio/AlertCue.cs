namespace D47.Core.Audio;

/// <summary>
/// A short non-speech marker played ahead of a warning, saying <em>which</em> warning it is before
/// the sentence has finished arriving (Phase 15).
/// <para>
/// The phase asks for this in as many words: each id group gets "its own spoken line and its own
/// alert cue rather than one generic warning, because the game has already told us which situation
/// it is". A cue is the fastest thing d47 can put in a Commander's ear — the median warning here is
/// six to eight seconds ahead of the shooting, and the first second of that is spent on the word
/// "interdiction".
/// </para>
/// <para>
/// Named like <see cref="LoopState"/> and for the same reason: the cue file is named for the enum
/// member and <see cref="CueLibrary"/> asserts the shipped set matches this enum exactly. A hand
/// written member-to-filename table is a place for a typo to live, and a typo there goes wrong as
/// silence — the one failure mode a warning cannot afford, since a warning that did not fire and a
/// warning whose cue did not load sound identical.
/// </para>
/// </summary>
public enum AlertCue
{
    /// <summary>
    /// A pirate is lining up an interdiction. 88% of these were followed by an attack, at a median
    /// of six seconds — the strongest signal in the corpus.
    /// </summary>
    Interdiction,

    /// <summary>
    /// A pirate demanding cargo. Far commoner than the interdiction line and correspondingly
    /// weaker at 67%, which is why it does not share a cue with it: the Commander who learns the
    /// difference by ear can weigh them differently.
    /// </summary>
    Piracy,

    /// <summary>
    /// A bounty hunter, who is here for the Commander rather than for the hold. Its own cue
    /// because the answer is different — cargo will not buy this one off.
    /// <para>
    /// <b>A hitman shares it</b> (<a href="https://github.com/dseelinger/d47/issues/137">#137</a>),
    /// because the rule for a distinct cue is that the <em>response</em> differs and here it does
    /// not: both are here for the Commander, and neither can be bought off with the hold. Giving
    /// the hitman a fourth sound would be accumulating cues rather than deciding on one.
    /// </para>
    /// </summary>
    BountyHunter,

    /// <summary>
    /// Somebody is hurting the ship right now — shot, shields gone, hull opened, or pulled out of
    /// supercruise into it (<a href="https://github.com/dseelinger/d47/issues/136">#136</a>).
    /// <para>
    /// <b>The cue was on the warning that an attack might happen and not on the one saying it
    /// is.</b> Every line in <see cref="Callouts.DangerCallout"/> is
    /// <see cref="Callouts.CalloutUrgency.Urgent"/> precisely because it is that kind of moment,
    /// and every one of them used to arrive as prose with no marker in front of it — so a pirate
    /// <em>announcing</em> an interdiction got a sound before the sentence and being shot did not.
    /// </para>
    /// <para>
    /// <b>One cue across the whole hostile set rather than one per line.</b> The three that came
    /// before are distinct because the answer differs — cargo buys off a pirate and not a bounty
    /// hunter — and shields-down, hull damage and under-attack do not divide that way: they are one
    /// situation reported by three sensors, and the answer to all of them is fight, run or
    /// high-wake. Phase 15 was explicit that a cue per announcement "would make the common ones
    /// into an alarm and leave the Commander no way to tell the four that matter apart".
    /// </para>
    /// </summary>
    UnderFire,

    /// <summary>
    /// The ship is cooking itself (#136). The one urgent danger that is not somebody else's doing.
    /// <para>
    /// <b>The split that earned itself</b>, where shields-and-hull did not. Heat is a different
    /// problem with a different answer — cut the throttle, drop silent running, get off the star —
    /// and nothing about it is answered by fighting or by running from anyone. So the urgent set
    /// carries two cues and not one, and that is the decision rather than a count that grew: heat
    /// and hostiles, and no third.
    /// </para>
    /// <para>
    /// It is also the only alert here that does not fall. The others are all somebody hostile and
    /// use a falling interval to say so; this pulses on one note, which reads as a warning lamp
    /// rather than as a threat.
    /// </para>
    /// </summary>
    Overheating,

    /// <summary>
    /// Flying in a rival Power's space. Not a thing that just happened but a condition entered,
    /// and the only cue here that is not about somebody shooting.
    /// </summary>
    RivalTerritory,

    /// <summary>
    /// A timer or an alarm has gone off (Phase 24, "A timer says its own name").
    /// <para>
    /// The only member here that is not about somebody shooting, and the only one the Commander
    /// asked for. It joins this family rather than getting machinery of its own because the
    /// family's contract is exactly what it needs: named for the enum member, with the test that
    /// asserts the shipped set matches this enum, because a hand-written member-to-filename table
    /// is a place for a typo to live and a typo there goes wrong as silence.
    /// </para>
    /// <para>
    /// <b>One clip for every timer, and d47 speaks the name.</b> Runtime synthesis was the
    /// alternative and would have let each timer carry its own tone — genuinely useful in a
    /// headset where you cannot glance — but it is new machinery in the audio path for a
    /// distinction the voice already makes better. So the cue says <em>something finished</em>
    /// and the sentence after it says which.
    /// </para>
    /// </summary>
    TimerElapsed,
}

/// <summary>
/// What each cue is called when it is written down rather than heard
/// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>).
/// <para>
/// <b>The one sound in d47 that has to be captioned.</b> A cue is played immediately <em>ahead</em>
/// of an urgent callout, and its whole documented purpose is saying which warning this is before
/// the sentence has arrived. A hearing Commander gets the marker and then the words; a Commander
/// reading captions used to get only the words, losing both the warning and the head start — which
/// is the FCC's "same extent" test failing on the one sound in the app that carries
/// safety-relevant meaning.
/// </para>
/// <para>
/// <b>Bracketed and lowercase, which is the standard's own form</b> for a sound event rather than
/// a spoken line — Netflix's SDH guide asks for descriptive bracketed text, and broadcast
/// captioning has written <c>[alarm]</c> that way for as long as it has written anything. Each cue
/// names its own situation because each cue <em>is</em> a distinct situation: a Commander who has
/// learned the four apart by ear knows which one fired, and a reader gets the same.
/// </para>
/// <para>
/// The loop-state cues are deliberately not here, and neither are the thinking bed, ambience or
/// music. The standard's condition is that a sound event is captioned when it <em>cannot be
/// visually identified</em>: the loop state is on the panel, and the other three are continuous and
/// carry no information a caption would be conveying. Captioning them is captioning noise, and a
/// caption band that flashes before every utterance is one nobody reads.
/// </para>
/// </summary>
public static class AlertCues
{
    /// <summary>
    /// The bracketed line for one cue.
    /// <para>
    /// A switch with no default arm rather than a dictionary, so a cue added tomorrow does not
    /// compile until somebody has decided what it is called — the same discipline
    /// <see cref="CueLibrary"/> applies to the clip files, and for the same reason: a hole here
    /// goes wrong as silence, which is the one failure mode a warning cannot afford.
    /// </para>
    /// </summary>
    public static string Caption(AlertCue cue) => cue switch
    {
        AlertCue.Interdiction => "[interdiction alert]",
        AlertCue.Piracy => "[pirate alert]",
        AlertCue.BountyHunter => "[bounty hunter alert]",
        AlertCue.UnderFire => "[attack alarm]",
        AlertCue.Overheating => "[heat alarm]",
        AlertCue.RivalTerritory => "[territory alert]",
        AlertCue.TimerElapsed => "[timer chime]",
        _ => "[alert]",
    };
}
