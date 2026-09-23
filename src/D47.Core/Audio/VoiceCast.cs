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

    /// <summary>Which of the pool's voices read as British, by id (#68).</summary>
    public IReadOnlySet<string> British { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The provider's voices, by id, for what each one sounds like.</summary>
    public IReadOnlyDictionary<string, VoiceInfo> Voices { get; set; } =
        new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The accent a voice is heard in, or null where its listing names none.</summary>
    public string? AccentOf(string? voiceId) =>
        voiceId is not null && Voices.TryGetValue(voiceId, out var voice) ? VoicePool.AccentOf(voice) : null;

    /// <summary>What the provider's listing says a voice's gender is.</summary>
    public VoiceGender GenderOf(string? voiceId) =>
        voiceId is not null && Voices.TryGetValue(voiceId, out var voice)
            ? VoicePool.GenderOf(voice.Gender)
            : VoiceGender.Unlabelled;

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
    /// <param name="allegiance">
    /// The docked station's <c>StationAllegiance</c>, or null — most stations have none (#68).
    /// </param>
    public VoiceSelection ForSender(
        string sender,
        bool isPlayer,
        VoiceRole role = VoiceRole.Comms,
        string? allegiance = null)
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

        // An Empire station sounds British where the pool has a British voice to give it (#68). Federation
        // and Alliance are left as they are: neither has a canon accent as settled as the Empire's.
        var accentMatched = allegiance == "Empire"
            ? eligible.Where(British.Contains).ToList()
            : eligible;

        var accentPool = accentMatched.Count > 0 ? accentMatched : eligible;

        // Of the right sex where there is a right sex to be had, within whichever pool the accent left.
        var matching = accentPool
            .Where(voice => Feminine.Contains(voice) == GivenNames.ReadsFemale(sender))
            .ToList();

        var drawnFrom = matching.Count > 0 ? matching : accentPool;

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

    /// <summary>Gives an NPC in this system a voice chosen before their name was known.</summary>
    public void Keep(string sender, string voiceId) => _perSystem[sender] = voiceId;

    /// <summary>The NPCs given a voice in this system so far, with it.</summary>
    public IReadOnlyList<(string Name, string VoiceId)> MetHere =>
        [.. _perSystem.Select(pair => (pair.Key, pair.Value))];

    /// <summary>
    /// Voices for people not yet named, one per slot: the Commander's voice for comms where they cast
    /// one, otherwise pool voices nobody aboard or in this system has, walked from a deterministic seed,
    /// British first at an Empire station (#68), alternating by gender where the pool is labelled.
    /// </summary>
    public IReadOnlyList<string> Roster(int count, uint seed, string? allegiance = null)
    {
        if (count <= 0)
        {
            return [];
        }

        if (_roleVoices.ContainsKey(VoiceRole.Comms) || Pool.Count == 0)
        {
            return For(VoiceRole.Comms).VoiceId is { } only ? [.. Enumerable.Repeat(only, count)] : [];
        }

        var start = (int)(seed % (uint)Pool.Count);
        var walked = Enumerable.Range(0, Pool.Count)
            .Select(offset => Pool[(start + offset) % Pool.Count])
            .Where(voice => !Aboard(voice))
            .ToList();

        var unused = walked
            .Where(voice => !_perSystem.Values.Contains(voice, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var candidates = unused.Count >= count ? unused : walked;

        if (candidates.Count == 0)
        {
            return For(VoiceRole.Comms).VoiceId is { } fallback ? [.. Enumerable.Repeat(fallback, count)] : [];
        }

        var empire = allegiance == "Empire";
        var chosen = new List<string>(count);
        var wantFeminine = (seed & 1) == 1;

        while (chosen.Count < count)
        {
            var left = candidates.Where(voice => !chosen.Contains(voice)).ToList();

            if (left.Count == 0)
            {
                chosen.Add(candidates[chosen.Count % candidates.Count]);
                continue;
            }

            var pick = left
                .OrderBy(voice => empire && !British.Contains(voice) ? 1 : 0)
                .ThenBy(voice => Feminine.Contains(voice) == wantFeminine ? 0 : 1)
                .First();
            chosen.Add(pick);
            wantFeminine = !wantFeminine;
        }

        return chosen;
    }

    /// <summary>A roster seed from an exchange's place in the sequence and the system it is in.</summary>
    public static uint SeedOf(int exchangeIndex, string? system) =>
        unchecked(Hash(system ?? string.Empty) ^ ((uint)exchangeIndex * 2654435761u));

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
