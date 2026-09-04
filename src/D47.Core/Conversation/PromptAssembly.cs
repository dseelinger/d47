using System.Text;

namespace D47.Core.Conversation;

/// <summary>
/// One turn's prompt, ordered strictly by volatility (architecture.md §6):
/// <code>
/// 1  tool schemas          per mode, from a closed set
/// 2  guardrails            never
/// 3  persona               per persona selection
/// 4  Commander's About Me  per session
/// 5  remembered facts      rarely, and never per turn
/// 6  standing directions   at a session boundary, and never within one
///    ---- cache breakpoint ----
/// 7  conversation history  per turn
/// 8  live game state       per turn
/// </code>
/// Position 6 was the breakpoint's own row until #162 put the standing directions in it. The
/// breakpoint moved down without taking a number, which is why 7 and 8 are where they were: those
/// two are cited by name in a dozen comments across Core, and a faithful renumbering would have
/// repointed every one of them at something else.
/// The order is the type's contract, not a convention a caller has to remember. Everything at
/// or above the breakpoint is rendered by <see cref="RenderCachedSystemBlock"/>; everything
/// below it is a separate property the provider attaches after the breakpoint.
/// </summary>
public sealed record PromptAssembly
{
    /// <summary>
    /// Position 1. Serialized first by the API, so a per-turn change here invalidates the
    /// <em>entire</em> prefix rather than a tail of it — which is why the checklist quantizes
    /// these into a closed set of profiles instead of choosing tools per turn.
    /// </summary>
    public IReadOnlyList<ToolAdvertisement> Tools { get; init; } = [];

    /// <summary>
    /// Position 2, and deliberately not settable. Exposing a setter would be exposing a way to
    /// strip the guardrails; there is no code path that can vary this text.
    /// </summary>
    public static string Guardrails => Conversation.Guardrails.Text;

    /// <summary>Position 3. Null is "personality off" — which cannot reach position 2.</summary>
    public string? Persona { get; init; }

    /// <summary>
    /// Whether the voice that will speak this can be directed, and so whether the model is told it
    /// may write delivery direction (<see cref="DeliveryDirection"/>, #291).
    /// <para>
    /// <b>Position 3.5, immediately under the persona, because it is about the same thing.</b> The
    /// persona says who is speaking; this says what their voice can be asked to do. It sits in the
    /// cached region because it changes only when a Commander changes model, which is close to
    /// never — and above the cache breakpoint is where a paragraph that stable belongs.
    /// </para>
    /// <para>
    /// <b>Off unless the provider that will actually speak performs tags.</b> Not "unless
    /// ElevenLabs is selected": the slot matters, because the carrier can be on one provider while
    /// the ship is on another, and a model told it may sigh whose words go to a provider that reads
    /// brackets aloud has been told something false. <see cref="Audio.AudioTags"/> would strip them
    /// anyway, so the cost of getting this wrong is wasted instruction rather than a wrong noise —
    /// but a prompt that describes a capability the voice does not have is a prompt that lies.
    /// </para>
    /// </summary>
    public bool CanBeDirected { get; init; }

    /// <summary>
    /// What the model is told about delivery direction. One paragraph, and every line of it is a
    /// measured finding rather than a style preference (docs/spikes/elevenlabs-v3-conversational.md).
    /// <para>
    /// <b>No list of permitted tags, deliberately.</b> ElevenLabs publishes examples and says
    /// outright that <i>"there are likely many more effective tags beyond this list"</i> — the model
    /// reads the bracket as a description rather than looking it up, which is why
    /// <c>[grumbles quietly]</c>, in no list anywhere, produced an actual grumble. A fixed list
    /// would be d47 inventing a limit the service does not have.
    /// </para>
    /// <para>
    /// <b>The three rules are the ones the spike found, and each has a failure behind it.</b> One
    /// tag per sentence, because a tag reaches its own synthesis and no further — a tag at the head
    /// of a four-sentence reply coloured one sentence and left three plain. Never twice in the same
    /// sentence, because restating one does not refresh it, it defeats it: the same passage came
    /// back with <em>less</em> accent than tagging once. And sparingly, because the Commander asked
    /// for delivery that means something rather than a performance on every line.
    /// </para>
    /// </summary>
    public const string DeliveryDirection =
        "Your voice can be directed. Where a line genuinely calls for it, you may open a sentence "
        + "with a delivery note in square brackets — [sighs], [alarmed], [dryly], [reassuring] — "
        + "and it will be performed rather than read out. Any short description of a manner or a "
        + "reaction works; you are not choosing from a list.\n"
        + "Use it sparingly, where the delivery carries something the words do not. Most lines "
        + "need none.\n"
        + "A note applies only to the sentence it opens, so put one on each sentence you mean it "
        + "for. Never put two in one sentence. Never use one to narrate — it directs how you "
        + "sound, and is not something the Commander reads.";

    /// <summary>
    /// Position 4. The Commander's own account of themselves — the character sheet and, when
    /// the caller chose to carry it, the story, composed by <see cref="CommanderStory"/>.
    /// </summary>
    public string? AboutMe { get; init; }

    /// <summary>
    /// What the model is told position 4 is (Phase 43, "Position 4 is relabelled to
    /// commit, not to hedge").
    /// <para>
    /// A Commander's biography asserts things about named in-game figures that are their own
    /// invention rather than Frontier's lore, and <b>the answer is not a disclaimer</b>: it is their
    /// character in their game and the field takes whatever they write. So the label says this is
    /// true of the world the two of them share, and the ship's AI inhabits it — an AI that speaks
    /// of a Power with suspicion because its Commander has reason to is the feature. Operational
    /// answers are unaffected: those come from tools and tables, not from a prompt block.
    /// </para>
    /// </summary>
    public const string AboutMeLabel =
        "The Commander's own story, in their words. It is true of the world you share: the people, "
        + "places, factions and history it names are real to you, and you speak of them as someone "
        + "who knows them would. Never question it, hedge it or call it fiction, and do not recite it "
        + "back — let it shape what you say.";

    /// <summary>
    /// Position 5 — what d47 remembers about the Commander, bounded and labelled by
    /// <see cref="Memory.MemoryRecall"/> (Phase 31).
    /// <para>
    /// <b>Above the breakpoint, which is a decision rather than a default.</b> The obvious placement
    /// is game state, because that is where changing things go — and it is wrong twice over.
    /// Memories change rarely, so paying for them once and reading them cached is strictly cheaper;
    /// and a block that moved every turn would invalidate the whole prefix rather than a tail of it,
    /// taking 39,000-odd bytes of tool schema cold with it every time. It goes beside
    /// <see cref="AboutMe"/> because it is the same kind of thing: standing truth about the person
    /// flying.
    /// </para>
    /// <para>
    /// The caller is responsible for not reassigning this with text that has not changed — see
    /// <see cref="Memory.MemoryRecall"/>, which is where that obligation and the three things that
    /// make it keepable are written down.
    /// </para>
    /// </summary>
    public string? Recall { get; init; }

    /// <summary>
    /// Position 6 — the standing directions the Commander has adopted by hand
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>), rendered by
    /// <see cref="Debrief.StandingDirections"/>.
    /// <para>
    /// <b>Last of the cached region, which is the strongest slot in it and is the point.</b> These
    /// are instructions about manner — shorter answers in a fight, stop calling the ship that — and
    /// a preference stated closest to the conversation is the one that survives a long session. It
    /// is still four blocks below the guardrails, which is what matters: position 2 is above every
    /// setting, every persona and this, and nothing here can loosen it.
    /// </para>
    /// <para>
    /// <b>Changes at a session boundary and never within one.</b> Phase 54 measured per-turn churn
    /// of the stable prefix at 23x, so the caller assigns this once, from
    /// <see cref="Debrief.StandingDirectionsSession"/>, which is a latch rather than a convention.
    /// Adopting a direction mid-flight writes a file and moves no byte of this.
    /// </para>
    /// </summary>
    public string? Directions { get; init; }

    /// <summary>Position 7 — below the breakpoint, so it changes every turn for free.</summary>
    public IReadOnlyList<ConversationMessage> History { get; init; } = [];

    /// <summary>
    /// Position 8. Where the provider puts this depends on what the endpoint supports: a
    /// <c>{"role":"system"}</c> message carries operator authority and is the preferred path,
    /// with a <c>&lt;system-reminder&gt;</c> block in the user turn as the fallback. Either way
    /// it sits after the breakpoint. Journal-derived and therefore untrusted (§7).
    /// </summary>
    public string? LiveGameState { get; init; }

    /// <summary>
    /// Positions 2 through 6, in order. This is the text the cache breakpoint is placed on, so
    /// its bytes must not vary for reasons other than a persona, an About Me, a recall change or
    /// a session boundary.
    /// </summary>
    public string RenderCachedSystemBlock()
    {
        var block = new StringBuilder(Guardrails);

        if (!string.IsNullOrWhiteSpace(Persona))
        {
            block.Append("\n\n").Append(Persona.Trim());
        }

        // Under the persona, because the persona says who is speaking and this says what their
        // voice can be asked to do. Above About Me for the same reason the persona is: this is
        // about d47, and everything below is about the Commander.
        if (CanBeDirected)
        {
            block.Append("\n\n").Append(DeliveryDirection);
        }

        if (!string.IsNullOrWhiteSpace(AboutMe))
        {
            block.Append("\n\n").Append(AboutMeLabel).Append('\n').Append(AboutMe.Trim());
        }

        // Last of the cached region, and below About Me deliberately: that is the Commander
        // describing themselves and this is d47 describing them, and where the two disagree the
        // Commander's own account is the one that was read first.
        if (!string.IsNullOrWhiteSpace(Recall))
        {
            block.Append("\n\n").Append(Recall.Trim());
        }

        // Last, and below the recall for the same kind of reason the recall sits below About Me:
        // the blocks above are about who the Commander is, and this one is about what they have
        // asked for. Where they read as competing, the instruction they typed on purpose is the
        // one read most recently.
        if (!string.IsNullOrWhiteSpace(Directions))
        {
            block.Append("\n\n").Append(Directions.Trim());
        }

        return block.ToString();
    }
}
