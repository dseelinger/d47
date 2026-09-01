namespace D47.Core.Conversation;

/// <summary>
/// How adventurous the sampler may be on one call
/// (<a href="https://github.com/dseelinger/d47/issues/98">#98</a>).
/// <para>
/// <b>Until this existed d47 sent no sampling field at all</b>, so the character of every line it
/// spoke was decided by a default it had never chosen and which varies by provider and by model.
/// That is a confound sitting underneath every judgement anybody makes about how a Guardian core
/// sounds: a flat-reading core may be a sampling artefact rather than a persona problem, and
/// there was no way to rule it in or out.
/// </para>
/// <para>
/// <b>Per call class, and never a settings row.</b> This is a property of the call, not a
/// preference — a Commander asking for more characterful ambient lines is asking for a different
/// persona, not a different sampler. The classes below are the ones d47 already had; each states
/// its value and why, so a future <i>the voice sounds flat</i> report can be checked against a
/// number rather than guessed at.
/// </para>
/// <para>
/// <b>Stated here and still not always sent.</b> Two things drop it downstream, and both are the
/// provider's business rather than the caller's: a model that has removed the field — every
/// Anthropic model from the 4.7 generation on returns a 400 for it — and an endpoint that refuses
/// it, which demotes through the path every other optional field already uses. So a caller says
/// what it wants and never has to know who is answering.
/// </para>
/// </summary>
public sealed record LlmSampling
{
    /// <summary>
    /// In character. <b>0.9 rather than a guess</b>: reported guidance for character writing puts
    /// the useful band at 0.8 to 1.0 — flat below about 0.7, incoherent above about 1.2 — and
    /// most of what d47 says is meant to be in character rather than correct.
    /// </summary>
    public const double Warm = 0.9;

    /// <summary>
    /// No warmth at all, for the calls that are questions about d47's own configuration or that
    /// are checked against the world afterwards. Nought rather than merely low: what these want
    /// is the same answer twice, and anything above nought is buying variation nobody asked for
    /// in an answer that gets validated.
    /// </summary>
    public const double Cold = 0.0;

    private LlmSampling(double? temperature) => Temperature = temperature;

    /// <summary>
    /// What to ask for, or null to send no sampling field and take whatever the endpoint does by
    /// default. Null is a real choice and not an absence — see <see cref="Unstated"/>.
    /// </summary>
    public double? Temperature { get; }

    /// <summary>
    /// Say nothing, deliberately. <b>Not the same as the state this issue was about</b>: that was
    /// every call silently taking an unchosen default, and this is one call for which the
    /// endpoint's own default is the right answer because the call is not about the output.
    /// <para>
    /// The key check is the whole of it. It asks one token in order to learn whether a key works,
    /// against a gateway that may validate fields d47 has never met — and a rejected field there
    /// reads as a rejected key, which sends a Commander to their account page to issue another
    /// one that will fail in exactly the same way.
    /// </para>
    /// </summary>
    // Cast because a record's copy constructor is also a one-argument constructor, and a bare
    // null cannot tell the two apart.
    public static readonly LlmSampling Unstated = new((double?)null);

    /// <summary>
    /// A turn the Commander asked for. Warm, because a reply spoken over a cockpit is a core
    /// talking rather than a function returning — and because the tools it may call are what make
    /// this call factual, not the sampler.
    /// </summary>
    public static readonly LlmSampling Conversation = new(Warm);

    /// <summary>
    /// An ambient remark, an opening brief, a gap reaction, a re-voiced callout, a carrier's
    /// tower, invented NPC chatter. Warm for the same reason as <see cref="Conversation"/> and
    /// more so: none of these was asked for, so a dull one is worse than none.
    /// </summary>
    public static readonly LlmSampling InCharacter = new(Warm);

    /// <summary>
    /// A lore lookup: what a web search turned up about a system, reported as a search result.
    /// <b>Cold, which is the opposite of the line beside it in the same breath</b> — the remark
    /// that precedes it is d47 speaking and this is d47 quoting, and the failure this class has
    /// is invention rather than dullness.
    /// </summary>
    public static readonly LlmSampling Lore = new(Cold);

    /// <summary>
    /// Adventure generation. <b>Cold although the output is a story</b>, and that is the call
    /// #98 made rather than an oversight: the beats are validated against the real galaxy and
    /// re-asked where they cannot stand, so this call's observed failure is naming places that do
    /// not exist. Variety comes from the systems within reach and the ship being flown, which
    /// change every time; warmth here buys invention in the one field that must be exact.
    /// <para>
    /// The re-ask is not endangered by this: it goes back with the refusals <em>and</em> the
    /// beats that stood, so it is a different prompt rather than the same one asked twice.
    /// </para>
    /// </summary>
    public static readonly LlmSampling Adventure = new(Cold);

    /// <summary>
    /// The Commander's log. Cold, and the existing note on <c>LogPrompt.Request</c> already
    /// argued it for effort: the entire difficulty of the task is <i>do not add anything</i>. It
    /// is also quoted at a price and refused if the model changed between quote and write, so it
    /// is the call least entitled to a surprise.
    /// </summary>
    public static readonly LlmSampling Log = new(Cold);

    /// <summary>
    /// Voice casting. Cold: a mechanical question about d47's own configuration, never spoken
    /// aloud, answered in a fixed format. There is nobody for it to be in character for.
    /// </summary>
    public static readonly LlmSampling VoiceCasting = new(Cold);
}
