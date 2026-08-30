namespace D47.Core.Audio;

/// <summary>
/// Who is speaking. Phase 11 is the point at which d47 stops being one voice, and this is the
/// closed set of things it can be.
/// <para>
/// A role rather than a voice: the role is what the code knows and what a settings row names,
/// and which voice fills it is a lookup that can change without a caller noticing. That
/// indirection is the whole of "chosen per role, so a Commander with a key hears it and
/// nothing else changes".
/// </para>
/// </summary>
public enum VoiceRole
{
    /// <summary>The persona aboard. Everything that was a voice before Phase 11 is this.</summary>
    ShipAi,

    /// <summary>
    /// A re-voiced in-game message from another Commander or an NPC. Never the ship AI's own
    /// voice, which is the entire requirement — a message arriving in your companion's voice
    /// reads as your companion saying it.
    /// </summary>
    Comms,

    /// <summary>The Commander's fleet carrier, answering as its captain.</summary>
    CarrierCaptain,

    /// <summary>The carrier's tower, handling arrivals and departures.</summary>
    TowerControl,

    /// <summary>A member of the invisible crew.</summary>
    Crew,
}

/// <summary>
/// What a role is called on a caption, when the voice is not the one a Commander expects
/// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>).
/// </summary>
public static class VoiceRoles
{
    /// <summary>
    /// The speaker ID for a captioned line, or null when the speaker needs no naming.
    /// <para>
    /// <b>Null for the ship's AI, and that is the standard's own rule rather than a shortcut.</b>
    /// Netflix's SDH guide asks for a speaker ID "when they cannot be visually identified", and in
    /// a headset nobody can — there is no face, no mouth and no visual channel for who is talking.
    /// What settles it is not visibility but ambiguity: d47 is the voice a caption band beside a
    /// cockpit is understood to belong to, so naming it on every line is the noise the rule exists
    /// to keep out. The carrier's tower, its captain and a crew member are somebody else, in
    /// somebody else's voice, and a reader has nothing else to tell them apart by.
    /// </para>
    /// <para>
    /// <see cref="VoiceRole.Comms"/> is here for completeness and reaches nothing: a re-voiced
    /// in-game message is not captioned at all, being already written on the comms page in full
    /// with its sender beside it.
    /// </para>
    /// </summary>
    public static string? Called(VoiceRole role) => role switch
    {
        VoiceRole.ShipAi => null,
        VoiceRole.CarrierCaptain => "Carrier",
        VoiceRole.TowerControl => "Tower",
        VoiceRole.Crew => "Crew",
        VoiceRole.Comms => "Comms",
        _ => null,
    };
}

/// <summary>
/// Which voice fills each role, and which voice belongs to which sender.
/// <para>
/// One component rather than a field on each caller, because the sticky assignments are the
/// part that is easy to get wrong: a wingmate whose voice changes on every jump reads as a bug
/// rather than as variety, and an NPC who keeps a voice forever means the cast never turns over
/// (Phase 11, "Voices stick").
/// </para>
/// <para>
/// Owns no thread and reads no clock. Scope changes are pushed in by whatever is already
/// watching the journal, so a replay drives this identically to a live session.
/// </para>
/// </summary>
public sealed class VoiceCast
{
    /// <summary>
    /// NPC voices, cleared on arrival in a new system. The cast turns over on a jump: the
    /// pirate who hailed you two systems ago is not here, and holding their voice forever
    /// would mean a fixed cast of strangers following you around the bubble.
    /// </summary>
    private readonly Dictionary<string, string> _perSystem = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Voices kept for the whole session, surviving hyperspace. A wingmate is the same person
    /// after a jump, and a voice that changed with the system would read as a fault.
    /// <para>
    /// Crew are here too, and for a stronger version of the same reason: they are aboard. Their
    /// assignments used to share the per-system table with the NPCs, so the gunner the Commander
    /// hired changed voice on every jump and could collide with a pirate — found while giving the
    /// crew the other half of "aboard", which is that they are not put through a radio.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, string> _lasting = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<VoiceRole, string> _roleVoices = new();

    /// <summary>
    /// The voices a sender may be assigned, in a stable order. Set from the provider's list,
    /// filtered to what the Commander would want to hear. Empty is supported and means every
    /// sender falls back to the role's own voice.
    /// </summary>
    public IReadOnlyList<string> Pool { get; set; } = [];

    /// <summary>
    /// Which of the pool's voices are a woman's, by id. Absent from the set means "not known to
    /// be", which is the same thing as a man's here — both providers tag gender, and a voice
    /// neither of them tags is one nothing can say anything about.
    /// <para>
    /// Kept beside <see cref="Pool"/> rather than in it because the pool is the order voices are
    /// walked in and this is a property of each voice; joining them would make the assignment
    /// arithmetic carry a record it does not otherwise need.
    /// </para>
    /// </summary>
    public IReadOnlySet<string> Feminine { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Speaking rate, normalised. One value for the whole cast rather than per role: it is a
    /// property of how fast the Commander likes to be spoken to, not of who is speaking.
    /// </summary>
    public double Rate { get; set; } = 1.0;

    /// <summary>The voice used when a role has none of its own, and for <see cref="VoiceRole.ShipAi"/>.</summary>
    public string? DefaultVoice { get; set; }

    /// <summary>Pins a voice to a role. Null clears it back to <see cref="DefaultVoice"/>.</summary>
    public void Assign(VoiceRole role, string? voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            _roleVoices.Remove(role);
            return;
        }

        _roleVoices[role] = voiceId;
    }

    /// <summary>What a role sounds like right now.</summary>
    public VoiceSelection For(VoiceRole role) =>
        new(_roleVoices.GetValueOrDefault(role) ?? DefaultVoice, Rate);

    /// <summary>
    /// What one sender sounds like, assigning them a voice the first time and keeping it after
    /// that.
    /// </summary>
    /// <param name="sender">
    /// The name as the game reported it. Untrusted — another Commander chose it — so it is only
    /// ever used as a dictionary key and as the seed below, never interpolated into a prompt or
    /// a path.
    /// </param>
    /// <param name="isPlayer">
    /// Whether this is another Commander. With <paramref name="role"/> it decides which scope the
    /// assignment lives in — see <see cref="Lasts"/>. The whole of the "voices stick" item is
    /// that the scopes differ: players survive hyperspace, NPC comms traffic does not.
    /// </param>
    public VoiceSelection ForSender(string sender, bool isPlayer, VoiceRole role = VoiceRole.Comms)
    {
        // <b>A role the Commander has cast has one voice, and a sender does not override it</b>
        // (<a href="https://github.com/dseelinger/d47/issues/109">#109</a>).
        //
        // This is the second half of that report, and the half nothing had noticed. The first was
        // that the carrier was identified too late for its docking chatter; fixing it made the
        // announcement <see cref="VoiceRole.TowerControl"/> and changed nothing a Commander could
        // hear, because a re-voiced message always carries a <c>Speaker</c> and every caller with a
        // speaker came here — where the cast voice was only ever reached as a fallback for an empty
        // pool. So the tower spoke in whichever pool voice its callsign hashed to, exactly as an
        // unknown NPC would, and the voice the Commander cast for it was never used for a word the
        // carrier actually said.
        //
        // Only the carrier's two roles are ever assigned one, and each is one person — a tower and
        // a captain — so "cast" and "has exactly one member" are the same set here. Comms is never
        // assigned, which is what keeps the per-sender draw below the answer for everybody else:
        // a system with four NPCs in it still gets four distinct voices.
        if (_roleVoices.ContainsKey(role))
        {
            return For(role);
        }

        var assignments = Lasts(isPlayer, role) ? _lasting : _perSystem;

        if (assignments.TryGetValue(sender, out var already))
        {
            return new VoiceSelection(already, Rate);
        }

        if (Pool.Count == 0)
        {
            return For(role);
        }

        // Seeded from the name rather than drawn at random, so the same Commander gets the same
        // voice in a replay as they did live — which is what makes this testable at all — and
        // so a name that has been seen before is recognisable even after the scope was cleared.
        var start = (int)(Hash(sender) % (uint)Pool.Count);

        // Everything this sender could be given, in the order they would be walked. Nobody
        // aboard the ship is in it: whatever else a police interceptor sounds like, it must not
        // sound like the companion in the cockpit.
        var eligible = new List<string>(Pool.Count);

        for (var offset = 0; offset < Pool.Count; offset++)
        {
            var candidate = Pool[(start + offset) % Pool.Count];

            if (!Aboard(candidate))
            {
                eligible.Add(candidate);
            }
        }

        // Nothing in the pool that is not already somebody aboard. Falling back to the role is
        // the honest answer: there is no voice left that would mean anything.
        if (eligible.Count == 0)
        {
            return For(role);
        }

        // Of the right sex where there is a right sex to be had. A woman called over the radio in
        // a man’s voice is a worse error than two women sharing one, so this narrows first and
        // only widens when the provider offers nothing that matches at all — which is the case
        // for a provider that tags no genders, and there the whole pool is the answer.
        var matching = eligible
            .Where(voice => Feminine.Contains(voice) == GivenNames.ReadsFemale(sender))
            .ToList();

        var drawnFrom = matching.Count > 0 ? matching : eligible;

        // Voices already spoken for are stepped past rather than reused, so a system with four
        // NPCs in it has four distinct voices as long as there are four to give out.
        foreach (var candidate in drawnFrom)
        {
            if (!assignments.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                assignments[sender] = candidate;
                return new VoiceSelection(candidate, Rate);
            }
        }

        // They are all spoken for, so this sender shares one — still of the right sex. Better
        // than silence, and better than a voice that changes every time they speak.
        assignments[sender] = drawnFrom[0];
        return new VoiceSelection(drawnFrom[0], Rate);
    }

    /// <summary>
    /// Whether a sender's voice outlives the system it was assigned in.
    /// <para>
    /// Only an NPC transmitting over comms turns over on a jump, because only that cast changes.
    /// A player is the same person after a jump and everyone aboard is still aboard, so the
    /// question is not "is this a player" but "is this someone the Commander will meet again".
    /// </para>
    /// </summary>
    private static bool Lasts(bool isPlayer, VoiceRole role) => isPlayer || role is not VoiceRole.Comms;

    /// <summary>
    /// Whether a voice already belongs to somebody in the ship, and so is not one to hand to a
    /// stranger.
    /// <para>
    /// This is the smaller half of "a voice appropriate for the NPC": whatever else a police
    /// interceptor sounds like, it must not sound like the companion in the cockpit. Before this,
    /// nothing stopped the pool handing out the ship AI's own voice — and hearing d47's voice
    /// arrive from a pirate, through a radio, is worse than either of them alone.
    /// </para>
    /// </summary>
    private bool Aboard(string voiceId) =>
        string.Equals(voiceId, DefaultVoice, StringComparison.OrdinalIgnoreCase)
        || _roleVoices.Values.Contains(voiceId, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A new system. Drops the per-system assignments and keeps the lasting ones, which is the
    /// asymmetry the checklist spells out.
    /// </summary>
    public void EnteredSystem() => _perSystem.Clear();

    /// <summary>
    /// A new session, or a provider change. Everything goes: a voice id from one provider means
    /// nothing to another, so keeping the assignments across a switch would keep a table of
    /// ids that no longer resolve.
    /// </summary>
    public void Reset()
    {
        _perSystem.Clear();
        _lasting.Clear();
        _roleVoices.Clear();
    }

    /// <summary>How many senders currently hold a voice. For diagnostics and for tests.</summary>
    public (int Lasting, int PerSystem) Assignments => (_lasting.Count, _perSystem.Count);

    /// <summary>
    /// FNV-1a. Chosen over <see cref="string.GetHashCode()"/> because that one is randomised
    /// per process by design, and a voice assignment that differed between two runs of the same
    /// recorded session would make the replay harness non-deterministic for no benefit.
    /// </summary>
    private static uint Hash(string value)
    {
        var hash = 2166136261u;

        foreach (var character in value)
        {
            hash ^= char.ToLowerInvariant(character);
            hash *= 16777619u;
        }

        return hash;
    }
}
