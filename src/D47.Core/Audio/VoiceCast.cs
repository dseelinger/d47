using System.Globalization;
using D47.Core.Speech;

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

    /// <summary>Tells the Commander's story in the third person. Never addressed, and calls no tools.</summary>
    Narrator,
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
        VoiceRole.Narrator => "Narrator",
        _ => null,
    };
}

/// <summary>Which voice fills each role, and which voice belongs to which sender.</summary>
public sealed class VoiceCast
{
    /// <summary>NPC voices, cleared on arrival in a system.</summary>
    private readonly Dictionary<string, string> _perSystem = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Station voices, cleared on arrival, on a drop into normal space and on leaving it.</summary>
    private readonly Dictionary<string, string> _perDrop = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Group ship voices by sender and message time, cleared on arrival.</summary>
    private readonly Dictionary<string, string> _perMessage = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each group ship's latest voice in this system, cleared on arrival.</summary>
    private readonly Dictionary<string, string> _groupLatest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>NPCs who interdicted the Commander, whose voices outlast an arrival.</summary>
    private readonly HashSet<string> _followers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The voices of <see cref="_followers"/> carried out of earlier systems, given back when they speak again.</summary>
    private readonly Dictionary<string, string> _carried = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Voices kept for the whole session, surviving hyperspace.</summary>
    private readonly Dictionary<string, string> _lasting = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<VoiceRole, string> _roleVoices = new();

    /// <summary>Held over every read and write of the NPC tables: the tick changes scopes while speech casts.</summary>
    private readonly Lock _gate = new();

    /// <summary>The voices a sender may be assigned, in a stable order.</summary>
    public IReadOnlyList<string> Pool { get; set; } = [];

    /// <summary>Which of the pool's voices are a woman's, by id.</summary>
    public IReadOnlySet<string> Feminine { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which of the pool's voices read as British, by id (#68).</summary>
    public IReadOnlySet<string> British { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The provider's voices, by id, for what each one sounds like.</summary>
    public IReadOnlyDictionary<string, VoiceInfo> Voices { get; set; } =
        new Dictionary<string, VoiceInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The accents the pool's voices carry, without repeats.</summary>
    public IReadOnlyList<string> Accents =>
        [.. Pool.Select(AccentOf).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// The reading recorded for a sender's name: null while unanswered. Never waits on the model.
    /// </summary>
    public Func<string, NameReading?>? ReadingOfName { get; set; }

    /// <summary>
    /// The sex recorded for a given name (male, female or indeterminate): null while unanswered. Never waits on
    /// the model.
    /// </summary>
    public Func<string, string?>? SexOfGivenName { get; set; }

    /// <summary>Called with a name that has no answer yet, so the question can be queued.</summary>
    public Action<string>? NameUnknown { get; set; }

    /// <summary>
    /// For a full name, null where nothing is to be waited for, otherwise a task completing when the sex of its
    /// given name is answered. Queues the question.
    /// </summary>
    public Func<string, Task?>? GivenNameAnswer { get; set; }

    /// <summary>
    /// The task a first line from this sender waits on for their given name's sex, or null where it does not
    /// wait: another Commander, a role other than comms, a role the Commander has cast, a sender already voiced
    /// here, or a given name with nothing to wait for.
    /// </summary>
    public Task? SexAnswerFor(string sender, bool isPlayer, VoiceRole role, string? own = null, SenderKind kind = SenderKind.Person)
    {
        lock (_gate)
        {
            return SexAnswerForLocked(sender, isPlayer, role, own, kind);
        }
    }

    private Task? SexAnswerForLocked(string sender, bool isPlayer, VoiceRole role, string? own, SenderKind kind)
    {
        if (isPlayer
            || role != VoiceRole.Comms
            || own is not null
            || GivenNameAnswer is null
            || Pool.Count == 0
            || _roleVoices.ContainsKey(role)
            || HeardAlready(sender, kind))
        {
            return null;
        }

        return GivenNameAnswer(sender);
    }

    /// <summary>Whether an NPC of this kind has been given a voice in its current scope.</summary>
    private bool HeardAlready(string sender, SenderKind kind) => kind switch
    {
        SenderKind.Station => _perDrop.ContainsKey(sender),
        SenderKind.GroupShip => _groupLatest.ContainsKey(sender),
        _ => _perSystem.ContainsKey(sender) || _carried.ContainsKey(sender),
    };

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
        Of(role == VoiceRole.Narrator ? NarratorVoice() : _roleVoices.GetValueOrDefault(role) ?? DefaultVoice, role);

    private VoiceSelection Of(string? voiceId, VoiceRole role) => new(voiceId, Rate) { Role = role };

    /// <summary>
    /// The narrator's pinned voice, or the first castable pool voice that is not the ship's when none is pinned or the
    /// pinned one is the ship's.
    /// </summary>
    private string? NarratorVoice()
    {
        var pinned = _roleVoices.GetValueOrDefault(VoiceRole.Narrator);

        if (pinned is not null && !string.Equals(pinned, DefaultVoice, StringComparison.OrdinalIgnoreCase))
        {
            return pinned;
        }

        var candidates = Pool
            .Where(voice => !string.Equals(voice, DefaultVoice, StringComparison.OrdinalIgnoreCase) && KokoroAssets.IsCastable(voice))
            .ToList();
        var english = candidates.Where(voice => NarratorAccents.Contains(AccentOf(voice) ?? "")).ToList();

        return english.FirstOrDefault(DescribedAsNarration)
            ?? english.FirstOrDefault()
            ?? candidates.FirstOrDefault()
            ?? pinned
            ?? DefaultVoice;
    }

    /// <summary>The accents an unpinned Narrator may be given.</summary>
    private static readonly HashSet<string> NarratorAccents = new(StringComparer.OrdinalIgnoreCase)
    {
        "American", "British", "English", "Scottish", "Welsh", "Irish", "Australian", "New Zealand", "Canadian",
    };

    /// <summary>Whether a voice's name or description mentions narration.</summary>
    private bool DescribedAsNarration(string voiceId) =>
        Voices.TryGetValue(voiceId, out var voice)
        && (voice.Label.Contains("narrat", StringComparison.OrdinalIgnoreCase)
            || voice.Description?.Contains("narrat", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// What one sender sounds like, assigning them a voice the first time and keeping it for as long as
    /// <paramref name="kind"/> lasts: another Commander and a role for the session, a person until the next
    /// arrival, a station until normal space is left, and a group ship for one message.
    /// </summary>
    /// <param name="sender">The name as the game reported it.</param>
    /// <param name="isPlayer">Whether this is another Commander.</param>
    /// <param name="allegiance">
    /// The docked station's <c>StationAllegiance</c>, or null — most stations have none (#68).
    /// </param>
    /// <param name="own">The sender's own voice, spoken in when this cast lists it.</param>
    /// <param name="sentAt">The message's journal timestamp: a group ship's voice is seeded from it and kept for it.</param>
    public VoiceSelection ForSender(
        string sender,
        bool isPlayer,
        VoiceRole role = VoiceRole.Comms,
        string? allegiance = null,
        string? own = null,
        SenderKind kind = SenderKind.Person,
        DateTimeOffset? sentAt = null)
    {
        lock (_gate)
        {
            return ForSenderLocked(sender, isPlayer, role, allegiance, own, kind, sentAt);
        }
    }

    private VoiceSelection ForSenderLocked(
        string sender,
        bool isPlayer,
        VoiceRole role,
        string? allegiance,
        string? own,
        SenderKind kind,
        DateTimeOffset? sentAt)
    {
        if (own is not null && (Voices.Count == 0 || Voices.ContainsKey(own)))
        {
            return Of(own, role);
        }

        // A role the Commander has cast has one voice, and a sender does not override it (<a
        // href=".com/dseelinger/d47/issues/109">#109</a>).
        if (_roleVoices.ContainsKey(role))
        {
            return For(role);
        }

        var lasts = Lasts(isPlayer, role);
        var npcKind = lasts ? SenderKind.Person : kind;

        var (assignments, key, scopeStart) = lasts ? (_lasting, sender, (DateTimeOffset?)null)
            : npcKind switch
            {
                SenderKind.Station => (_perDrop, sender, Drop ?? Arrival),
                SenderKind.GroupShip => (_perMessage, PerMessageKey(sender, sentAt), sentAt ?? Drop ?? Arrival),
                _ => (_perSystem, sender, Arrival),
            };

        if (assignments.TryGetValue(key, out var already))
        {
            return Of(already, role);
        }

        if (!lasts && npcKind == SenderKind.Person && _carried.TryGetValue(sender, out var carried))
        {
            _perSystem[sender] = carried;
            return Of(carried, role);
        }

        if (Pool.Count == 0)
        {
            return For(role);
        }

        // Seeded rather than random so a replay gives the live run's voices. A lasting sender is seeded from
        // the name alone; an NPC from the name, the system and the journal time its scope opened.
        var seed = lasts ? Hash(sender) : Scoped(sender, System, scopeStart);
        var start = (int)(seed % (uint)Pool.Count);

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

        // The name's accent first, then an Empire station's British one (#68). Federation and Alliance are
        // left as they are: neither has a canon accent as settled as the Empire's.
        var reading = NameReadingOf(sender, isPlayer, role);
        var named = reading is null
            ? null
            : Accents.FirstOrDefault(accent => string.Equals(accent, reading.Accent, StringComparison.OrdinalIgnoreCase));
        var accentMatched = named is null
            ? new List<string>()
            : eligible.Where(voice => string.Equals(AccentOf(voice), named, StringComparison.OrdinalIgnoreCase)).ToList();

        if (accentMatched.Count == 0)
        {
            accentMatched = allegiance == "Empire" ? eligible.Where(British.Contains).ToList() : eligible;
        }

        var accentPool = accentMatched.Count > 0 ? accentMatched : eligible;

        // Of the sex the model gave the name where there is one to be had, within whichever pool the accent left.
        var matching = reading?.Sex is NameReading.Female or NameReading.Male
            ? accentPool.Where(voice => Feminine.Contains(voice) == (reading.Sex == NameReading.Female)).ToList()
            : [];

        var drawnFrom = matching.Count > 0 ? matching : accentPool;

        // Voices already spoken for are stepped past rather than reused, so a system with four NPCs in it has
        // four distinct voices as long as there are four to give out.
        var spokenFor = lasts
            ? new HashSet<string>(_lasting.Values, StringComparer.OrdinalIgnoreCase)
            : VoicesHeardHere();
        var free = drawnFrom
            .Where(voice => !spokenFor.Contains(voice))
            .ToList();

        // They are all spoken for, so this sender shares one, still of the sex chosen; a group ship still steps past
        // the voice its last message was in.
        var previous = npcKind == SenderKind.GroupShip ? _groupLatest.GetValueOrDefault(sender) : null;
        var besidesPrevious = drawnFrom.Where(voice => !string.Equals(voice, previous, StringComparison.OrdinalIgnoreCase)).ToList();

        var chosen = free.Count > 0 ? free[(int)(Mixed(seed, 0) % (uint)free.Count)]
            : previous is not null && besidesPrevious.Count > 0 ? besidesPrevious[(int)(Mixed(seed, 0) % (uint)besidesPrevious.Count)]
            : drawnFrom[0];

        assignments[key] = chosen;

        if (npcKind == SenderKind.GroupShip && !lasts)
        {
            _groupLatest[sender] = chosen;
        }

        return Of(chosen, role);
    }

    /// <summary>The key a group ship's voice for one message is kept under.</summary>
    private static string PerMessageKey(string sender, DateTimeOffset? sentAt) =>
        sentAt is { } at ? sender + "\u001f" + at.UtcTicks.ToString(CultureInfo.InvariantCulture) : sender;

    /// <summary>
    /// An NPC's seed from its name, the system and the journal time its scope opened; the name alone where neither is
    /// known, and the name and system where only the time is not.
    /// </summary>
    private static uint Scoped(string sender, string? system, DateTimeOffset? opened) =>
        (system, opened) switch
        {
            (null, null) => Hash(sender),
            (_, null) => Hash(sender + "\u001f" + system),
            (_, { } at) => Hash(sender + "\u001f" + system + "\u001f" + at.UtcTicks.ToString(CultureInfo.InvariantCulture)),
        };

    /// <summary>Every NPC voiced in the current scopes, with their voice: people, stations and each group ship's latest.</summary>
    private IEnumerable<(string Name, string VoiceId)> HeardHere() =>
        _perSystem.Concat(_perDrop).Concat(_groupLatest).Select(pair => (pair.Key, pair.Value));

    /// <summary>The voices <see cref="HeardHere"/> holds, and those carried for an interdictor.</summary>
    private HashSet<string> VoicesHeardHere() =>
        new(HeardHere().Select(met => met.VoiceId).Concat(_carried.Values), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The accent and sex a name is recorded as suggesting, or null. A name with no answer yet is reported to
    /// <see cref="NameUnknown"/>; other Commanders and roles other than comms are never looked up.
    /// </summary>
    private NameReading? NameReadingOf(string sender, bool isPlayer, VoiceRole role)
    {
        if (isPlayer || role != VoiceRole.Comms)
        {
            return null;
        }

        var recorded = ReadingOfName?.Invoke(sender);
        var sex = SexOfGivenName?.Invoke(NameAccents.GivenNameOf(sender));

        if ((ReadingOfName is not null && recorded is null) || (SexOfGivenName is not null && sex is null))
        {
            NameUnknown?.Invoke(sender);
        }

        return recorded is null && sex is null ? null : new NameReading(recorded?.Accent ?? string.Empty, sex ?? NameReading.Unknown);
    }

    /// <summary>Gives an NPC in this system a voice chosen before their name was known.</summary>
    public void Keep(string sender, string voiceId)
    {
        lock (_gate)
        {
            _perSystem[sender] = voiceId;
        }
    }

    /// <summary>
    /// A pool voice for a speaker heard in every session: the pool walked from a hash of <paramref name="name"/>, skipping
    /// voices aboard. Null with an empty pool.
    /// </summary>
    public string? Lasting(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        lock (_gate)
        {
            if (Pool.Count == 0)
            {
                return null;
            }

            var start = (int)(Hash(name) % (uint)Pool.Count);

            return Enumerable.Range(0, Pool.Count)
                .Select(offset => Pool[(start + offset) % Pool.Count])
                .FirstOrDefault(voice => !Aboard(voice)) ?? Pool[start];
        }
    }

    /// <summary>The NPCs holding a voice here, with it: people in this system, stations in this drop and each group ship's latest.</summary>
    public IReadOnlyList<(string Name, string VoiceId)> MetHere
    {
        get
        {
            lock (_gate)
            {
                return [.. HeardHere()];
            }
        }
    }

    /// <summary>
    /// Voices for people not yet named, one per slot: the Commander's voice for comms where they cast
    /// one, otherwise pool voices nobody aboard or in this system has, walked from a deterministic seed,
    /// British first at an Empire station (#68), alternating by gender where the pool is labelled.
    /// </summary>
    public IReadOnlyList<string> Roster(int count, uint seed, string? allegiance = null)
    {
        lock (_gate)
        {
            return RosterLocked(count, seed, allegiance);
        }
    }

    private IReadOnlyList<string> RosterLocked(int count, uint seed, string? allegiance)
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

        var heard = VoicesHeardHere();
        var unused = walked.Where(voice => !heard.Contains(voice)).ToList();

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

            var tier = empire && left.Any(British.Contains) ? left.Where(British.Contains).ToList() : left;
            var sexed = tier.Where(voice => Feminine.Contains(voice) == wantFeminine).ToList();
            var from = sexed.Count > 0 ? sexed : tier;
            var pick = from[(int)(Mixed(seed, (uint)chosen.Count) % (uint)from.Count)];
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

    /// <summary>The system NPC voices are currently drawn for, or null before one is known.</summary>
    public string? System { get; set; }

    /// <summary>The journal time of the arrival in <see cref="System"/>, or null where it is not known.</summary>
    public DateTimeOffset? Arrival { get; private set; }

    /// <summary>The journal time of the drop into normal space, or null outside it.</summary>
    public DateTimeOffset? Drop { get; private set; }

    /// <summary>
    /// An arrival in <paramref name="system"/> at journal time <paramref name="at"/>: NPC voices other than those of an
    /// NPC who interdicted the Commander are cleared and drawn again, and normal space is left.
    /// </summary>
    public void EnteredSystem(string? system = null, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            System = system;
            Arrival = at;

            foreach (var (name, voice) in _perSystem.Where(pair => _followers.Contains(pair.Key)))
            {
                _carried[name] = voice;
            }

            _perSystem.Clear();

            _perMessage.Clear();
            _groupLatest.Clear();
            Drop = null;
            _perDrop.Clear();
        }
    }

    /// <summary>A drop into normal space at journal time <paramref name="at"/>: stations are drawn new voices.</summary>
    public void Dropped(DateTimeOffset at)
    {
        lock (_gate)
        {
            Drop = at;
            _perDrop.Clear();
        }
    }

    /// <summary>Normal space is left: stations' voices are cleared.</summary>
    public void LeftNormalSpace()
    {
        lock (_gate)
        {
            Drop = null;
            _perDrop.Clear();
        }
    }

    /// <summary>An NPC interdicted the Commander: it keeps the voice it has, or is given next, for the session.</summary>
    public void Interdicted(string interdictor)
    {
        lock (_gate)
        {
            _followers.Add(interdictor);
        }
    }

    /// <summary>A seed and slot mixed so that nearby seeds and consecutive slots give unrelated indexes.</summary>
    private static uint Mixed(uint seed, uint slot)
    {
        var x = unchecked((seed + slot * 0x9E3779B9u) * 2654435761u);
        x ^= x >> 15;
        x = unchecked(x * 2246822519u);
        x ^= x >> 13;
        return x;
    }

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
