namespace D47.Core.Audio;

/// <summary>Who is speaking.</summary>
public enum VoiceRole
{
    /// <summary>The persona aboard.</summary>
    ShipAi,

    /// <summary>A re-voiced in-game message from another Commander or an NPC.</summary>
    Comms,

    /// <summary>The Commander's fleet carrier, answering as its captain.</summary>
    CarrierCaptain,

    /// <summary>The carrier's tower, handling arrivals and departures.</summary>
    TowerControl,

    /// <summary>A member of the invisible crew.</summary>
    Crew,
}

/// <summary>
/// What a role is called on a caption, when the voice is not the one a Commander expects (#201).
/// </summary>
public static class VoiceRoles
{
    /// <summary>The speaker ID for a captioned line, or null when the speaker needs no naming.</summary>
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

/// <summary>Which voice fills each role, and which voice belongs to which sender.</summary>
public sealed class VoiceCast
{
    /// <summary>NPC voices, cleared on arrival in a new system.</summary>
    private readonly Dictionary<string, string> _perSystem = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Voices kept for the whole session, surviving hyperspace.</summary>
    private readonly Dictionary<string, string> _lasting = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<VoiceRole, string> _roleVoices = new();

    /// <summary>The voices a sender may be assigned, in a stable order.</summary>
    public IReadOnlyList<string> Pool { get; set; } = [];

    /// <summary>Which of the pool's voices are a woman's, by id.</summary>
    public IReadOnlySet<string> Feminine { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Speaking rate, normalised.</summary>
    public double Rate { get; set; } = 1.0;

    /// <summary>The voice used when a role has none of its own, and for <see cref="VoiceRole.ShipAi"/>.</summary>
    public string? DefaultVoice { get; set; }

    /// <summary>Pins a voice to a role.</summary>
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
    /// What one sender sounds like, assigning them a voice the first time and keeping it after that.
    /// </summary>
    /// <param name="sender">The name as the game reported it.</param>
    /// <param name="isPlayer">Whether this is another Commander.</param>
    public VoiceSelection ForSender(string sender, bool isPlayer, VoiceRole role = VoiceRole.Comms)
    {
        // A role the Commander has cast has one voice, and a sender does not override it (<a
        // href=".com/dseelinger/d47/issues/109">#109</a>).
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

        // Seeded from the name rather than drawn at random, so the same Commander gets the same voice in a
        // replay as they did live — which is what makes this testable at all — and so a name that has been
        // seen before is recognisable even after the scope was cleared.
        var start = (int)(Hash(sender) % (uint)Pool.Count);

        // Everything this sender could be given, in the order they would be walked.
        var eligible = new List<string>(Pool.Count);

        for (var offset = 0; offset < Pool.Count; offset++)
        {
            var candidate = Pool[(start + offset) % Pool.Count];

            if (!Aboard(candidate))
            {
                eligible.Add(candidate);
            }
        }

        // Nothing in the pool that is not already somebody aboard.
        if (eligible.Count == 0)
        {
            return For(role);
        }

        // Of the right sex where there is a right sex to be had.
        var matching = eligible
            .Where(voice => Feminine.Contains(voice) == GivenNames.ReadsFemale(sender))
            .ToList();

        var drawnFrom = matching.Count > 0 ? matching : eligible;

        // Voices already spoken for are stepped past rather than reused, so a system with four NPCs in it has
        // four distinct voices as long as there are four to give out.
        foreach (var candidate in drawnFrom)
        {
            if (!assignments.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                assignments[sender] = candidate;
                return new VoiceSelection(candidate, Rate);
            }
        }

        // They are all spoken for, so this sender shares one — still of the right sex.
        assignments[sender] = drawnFrom[0];
        return new VoiceSelection(drawnFrom[0], Rate);
    }

    /// <summary>Whether a sender's voice outlives the system it was assigned in.</summary>
    private static bool Lasts(bool isPlayer, VoiceRole role) => isPlayer || role is not VoiceRole.Comms;

    /// <summary>
    /// Whether a voice already belongs to somebody in the ship, and so is not one to hand to a
    /// stranger.
    /// </summary>
    private bool Aboard(string voiceId) =>
        string.Equals(voiceId, DefaultVoice, StringComparison.OrdinalIgnoreCase)
        || _roleVoices.Values.Contains(voiceId, StringComparer.OrdinalIgnoreCase);

    /// <summary>A new system.</summary>
    public void EnteredSystem() => _perSystem.Clear();

    /// <summary>A new session, or a provider change.</summary>
    public void Reset()
    {
        _perSystem.Clear();
        _lasting.Clear();
        _roleVoices.Clear();
    }

    /// <summary>How many senders currently hold a voice.</summary>
    public (int Lasting, int PerSystem) Assignments => (_lasting.Count, _perSystem.Count);

    /// <summary>FNV-1a.</summary>
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
