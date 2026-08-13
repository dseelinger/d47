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
    private readonly Dictionary<string, string> _npcVoices = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Player voices, kept for the whole session and surviving hyperspace. A wingmate is the
    /// same person after a jump, and a voice that changed with the system would read as a fault.
    /// </summary>
    private readonly Dictionary<string, string> _playerVoices = new(StringComparer.OrdinalIgnoreCase);

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
    /// Which scope the assignment lives in. The whole of the "voices stick" item is that these
    /// two differ: players survive hyperspace, NPCs do not.
    /// </param>
    public VoiceSelection ForSender(string sender, bool isPlayer, VoiceRole role = VoiceRole.Comms)
    {
        var assignments = isPlayer ? _playerVoices : _npcVoices;

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

        for (var offset = 0; offset < Pool.Count; offset++)
        {
            var candidate = Pool[(start + offset) % Pool.Count];

            if (!assignments.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                assignments[sender] = candidate;
                return new VoiceSelection(candidate, Rate);
            }
        }

        // Every voice in the pool is spoken for, so this sender shares one. Better than
        // silence, and better than a voice that changes every time they speak.
        assignments[sender] = Pool[start];
        return new VoiceSelection(Pool[start], Rate);
    }

    /// <summary>
    /// A new system. Drops the NPC assignments and keeps the player ones, which is the
    /// asymmetry the checklist spells out.
    /// </summary>
    public void EnteredSystem() => _npcVoices.Clear();

    /// <summary>
    /// A new session, or a provider change. Everything goes: a voice id from one provider means
    /// nothing to another, so keeping the assignments across a switch would keep a table of
    /// ids that no longer resolve.
    /// </summary>
    public void Reset()
    {
        _npcVoices.Clear();
        _playerVoices.Clear();
        _roleVoices.Clear();
    }

    /// <summary>How many senders currently hold a voice. For diagnostics and for tests.</summary>
    public (int Players, int Npcs) Assignments => (_playerVoices.Count, _npcVoices.Count);

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
