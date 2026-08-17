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
/// Which voice fills each role, and which voice belongs to which sender.
/// <para>
/// One component rather than a field on each caller, because the sticky assignments are the
/// part that is easy to get wrong: a wingmate whose voice changes on every jump reads as a bug
/// rather than as variety, and an NPC who keeps a voice forever means the cast never turns over
/// (list.md Phase 11, "Voices stick").
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
        // Voices already spoken for are stepped past rather than reused, so a system with four
        // NPCs in it has four distinct voices as long as the pool is large enough.
        var start = (int)(Hash(sender) % (uint)Pool.Count);

        // The first voice this sender could have, ignoring who else already holds one. Kept
        // because a pool that has run out has to share rather than fall silent, and the voice it
        // shares still must not be one that is somebody aboard.
        string? sharable = null;

        for (var offset = 0; offset < Pool.Count; offset++)
        {
            var candidate = Pool[(start + offset) % Pool.Count];

            if (Aboard(candidate))
            {
                continue;
            }

            sharable ??= candidate;

            if (!assignments.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                assignments[sender] = candidate;
                return new VoiceSelection(candidate, Rate);
            }
        }

        // Nothing in the pool that is not already somebody aboard. Falling back to the role is
        // the honest answer: there is no voice left that would mean anything.
        if (sharable is null)
        {
            return For(role);
        }

        // Every voice in the pool is spoken for, so this sender shares one. Better than
        // silence, and better than a voice that changes every time they speak.
        assignments[sender] = sharable;
        return new VoiceSelection(sharable, Rate);
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
