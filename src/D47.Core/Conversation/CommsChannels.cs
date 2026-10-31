using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Journal;

namespace D47.Core.Conversation;

/// <summary>A comms channel, in cycling order.</summary>
public enum CommsChannel
{
    Carrier,
    Target,
    System,
    Story,
    Crew,
    Characters,
    Shuttle,
}

/// <summary>One party on a channel. <see cref="Key"/> stays the same while they stay on it; <see cref="Name"/> may change.</summary>
public sealed record CommsSubChannel(string Key, string Name);

/// <summary>The selected channel and who on it; a null <see cref="SubChannel"/> is "no one".</summary>
public sealed record CommsSelection(CommsChannel Channel, CommsSubChannel? SubChannel)
{
    public bool IsNoOne => SubChannel is null;
}

/// <summary>What the channels are built from, read once per update.</summary>
public sealed record CommsInputs
{
    public bool CarrierOwned { get; init; }

    public TargetedShip? Target { get; init; }

    /// <summary>The roster, newest first, as <see cref="NpcsHeardHere.Present"/> gives it.</summary>
    public IReadOnlyList<NpcHeard> Heard { get; init; } = [];

    public bool StoryRunning { get; init; }

    /// <summary>The running story's id; its cast selection is remembered under it.</summary>
    public string? StoryId { get; init; }

    /// <summary>The running story's cast members the Commander has been introduced to.</summary>
    public IReadOnlyList<AdventureSpeaker> StoryCast { get; init; } = [];

    /// <summary>Whether the narrator takes replies on the STORY channel.</summary>
    public bool NarratorAnswers { get; init; }

    public ShipCrew Crew { get; init; } = ShipCrew.Empty;

    public IReadOnlyList<CustomCharacter> Characters { get; init; } = [];

    /// <summary>The shuttle ride in progress, or null.</summary>
    public ShuttleRide? Shuttle { get; init; }
}

/// <summary>The selected channel and the party last selected on each channel, for saving and restoring.</summary>
public sealed record CommsMemory
{
    public CommsChannel? Channel { get; init; }

    /// <summary>The party last selected on each channel but Story, by key.</summary>
    public IReadOnlyDictionary<CommsChannel, string> Parties { get; init; } = new Dictionary<CommsChannel, string>();

    /// <summary>The cast member last selected on Story, by story id.</summary>
    public IReadOnlyDictionary<string, string> StoryParties { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// The comms channels available now and the one selected. Reads no clock; the caller pushes state through
/// <see cref="Update"/>. Once a channel is selected the selection never moves by itself.
/// </summary>
public sealed class CommsChannels
{
    public const string CaptainKey = "captain";

    public const string TowerKey = "tower";

    public const string TargetKey = "target";

    public const string NarratorKey = ":narrator";

    public const string PilotKey = "pilot";

    private static readonly CommsChannel[] Order = Enum.GetValues<CommsChannel>();

    private readonly Lock _lock = new();
    private CommsInputs _inputs = new();
    private CommsChannel? _channel;
    private readonly Dictionary<CommsChannel, string> _parties = [];
    private readonly Dictionary<string, string> _storyParties = new(StringComparer.Ordinal);

    /// <summary>Raised, outside any lock, when the selection or the available set changes.</summary>
    public event Action? Changed;

    /// <summary>The selection, or null until a channel has first been available.</summary>
    public CommsSelection? Selected
    {
        get
        {
            lock (_lock)
            {
                return Selection();
            }
        }
    }

    /// <summary>The selected channel and the party last selected on each.</summary>
    public CommsMemory Memory
    {
        get
        {
            lock (_lock)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Takes up a saved selection; null returns to the launch state. Speaks nothing.</summary>
    public void Restore(CommsMemory? memory) => Change(() =>
    {
        _parties.Clear();
        _storyParties.Clear();
        _channel = memory?.Channel;

        if (memory is not null)
        {
            foreach (var (channel, key) in memory.Parties)
            {
                _parties[channel] = key;
            }

            foreach (var (story, key) in memory.StoryParties)
            {
                _storyParties[story] = key;
            }
        }

        return true;
    });

    /// <summary>The channels available now, in cycling order.</summary>
    public IReadOnlyList<CommsChannel> Available
    {
        get
        {
            lock (_lock)
            {
                return AvailableNow();
            }
        }
    }

    /// <summary>The parties on <paramref name="channel"/> now; empty when it is unavailable.</summary>
    public IReadOnlyList<CommsSubChannel> SubChannels(CommsChannel channel)
    {
        lock (_lock)
        {
            return PartiesOn(channel);
        }
    }

    /// <summary>Replaces the state the channels are built from.</summary>
    public void Update(CommsInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        Change(() =>
        {
            var previousStory = _inputs.StoryId;
            _inputs = inputs;

            if (_channel is null)
            {
                if (AvailableNow() is [var first, ..])
                {
                    Enter(first);
                }
            }
            else if (_channel == CommsChannel.Story && inputs.StoryRunning && inputs.StoryId != previousStory)
            {
                Enter(CommsChannel.Story);
            }

            return true;
        });
    }

    /// <summary>Moves to the next available channel, wrapping.</summary>
    public void NextChannel() => Change(() => Step(1));

    /// <summary>Moves to the previous available channel, wrapping.</summary>
    public void PreviousChannel() => Change(() => Step(-1));

    /// <summary>Moves to the next party on the selected channel, wrapping within it.</summary>
    public void NextSubChannel() => Change(() => StepWithin(1));

    /// <summary>Moves to the previous party on the selected channel, wrapping within it.</summary>
    public void PreviousSubChannel() => Change(() => StepWithin(-1));

    /// <summary>
    /// Selects an available channel and, when named, the party whose whole name matches ignoring case, else the first whose
    /// last word is the name given. False, with the selection unchanged, when the channel is unavailable or no party matches.
    /// </summary>
    public bool Select(CommsChannel channel, string? subChannel = null) => Change(() =>
    {
        if (!AvailableNow().Contains(channel))
        {
            return false;
        }

        if (subChannel is null)
        {
            Enter(channel);
            return true;
        }

        if (Match(PartiesOn(channel), subChannel) is not { } party)
        {
            return false;
        }

        _channel = channel;
        Remember(channel, party.Key);
        return true;
    });

    /// <summary>Selects the roster NPC who hailed the Commander. False, with the selection unchanged, when they are not on the roster.</summary>
    public bool Hailed(string rosterName) => Change(() =>
    {
        var party = PartiesOn(CommsChannel.System).FirstOrDefault(party => Same(party.Key, rosterName));

        if (party is null)
        {
            return false;
        }

        _channel = CommsChannel.System;
        Remember(CommsChannel.System, party.Key);
        return true;
    });

    private T Change<T>(Func<T> change)
    {
        T result;
        bool changed;

        lock (_lock)
        {
            var available = AvailableNow();
            var selected = Selection();
            var memory = Snapshot();

            result = change();

            changed = !available.SequenceEqual(AvailableNow()) || selected != Selection() || !Same(memory, Snapshot());
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return result;
    }

    private bool Step(int by)
    {
        var available = AvailableNow();

        if (available.Count == 0)
        {
            return false;
        }

        var from = _channel is { } channel ? Array.IndexOf(Order, channel) : (by > 0 ? -1 : 0);

        for (var i = 1; i <= Order.Length; i++)
        {
            var next = Order[((from + (by * i)) % Order.Length + Order.Length) % Order.Length];

            if (available.Contains(next))
            {
                Enter(next);
                return true;
            }
        }

        return false;
    }

    private bool StepWithin(int by)
    {
        if (_channel is not { } channel || PartiesOn(channel) is not { Count: > 0 } parties)
        {
            return false;
        }

        var key = RememberedKey(channel);
        var at = key is null ? -1 : parties.ToList().FindIndex(party => Same(party.Key, key));
        var next = at < 0
            ? (by > 0 ? 0 : parties.Count - 1)
            : ((at + by) % parties.Count + parties.Count) % parties.Count;

        Remember(channel, parties[next].Key);
        return true;
    }

    private void Enter(CommsChannel channel)
    {
        _channel = channel;

        var parties = PartiesOn(channel);
        var remembered = RememberedKey(channel);
        var chosen = parties.FirstOrDefault(party => remembered is not null && Same(party.Key, remembered)) ?? parties.FirstOrDefault();

        if (chosen is not null)
        {
            Remember(channel, chosen.Key);
        }
    }

    private string? RememberedKey(CommsChannel channel)
    {
        if (channel == CommsChannel.Story)
        {
            return _storyParties.GetValueOrDefault(_inputs.StoryId ?? string.Empty);
        }

        return _parties.GetValueOrDefault(channel);
    }

    private void Remember(CommsChannel channel, string key)
    {
        if (channel == CommsChannel.Story)
        {
            _storyParties[_inputs.StoryId ?? string.Empty] = key;
        }
        else if (channel != CommsChannel.Target)
        {
            _parties[channel] = key;
        }
    }

    private CommsMemory Snapshot() => new()
    {
        Channel = _channel,
        Parties = new Dictionary<CommsChannel, string>(_parties),
        StoryParties = new Dictionary<string, string>(_storyParties, StringComparer.Ordinal),
    };

    private static bool Same(CommsMemory left, CommsMemory right) =>
        left.Channel == right.Channel
        && left.Parties.Count == right.Parties.Count
        && left.Parties.All(pair => right.Parties.TryGetValue(pair.Key, out var key) && key == pair.Value)
        && left.StoryParties.Count == right.StoryParties.Count
        && left.StoryParties.All(pair => right.StoryParties.TryGetValue(pair.Key, out var key) && key == pair.Value);

    private CommsSelection? Selection()
    {
        if (_channel is not { } channel)
        {
            return null;
        }

        var parties = PartiesOn(channel);

        var remembered = RememberedKey(channel);

        var party = channel == CommsChannel.Target
            ? parties.FirstOrDefault()
            : parties.FirstOrDefault(party => remembered is not null && Same(party.Key, remembered));

        return new CommsSelection(channel, party);
    }

    private List<CommsChannel> AvailableNow() => [.. Order.Where(IsAvailable)];

    private bool IsAvailable(CommsChannel channel) => channel switch
    {
        CommsChannel.Carrier => _inputs.CarrierOwned,
        CommsChannel.Target => NpcTarget() is not null,
        CommsChannel.System => _inputs.Heard.Count > 0,
        CommsChannel.Story => _inputs.StoryRunning,
        CommsChannel.Crew => _inputs.Crew.Members.Any(member => member.Active),
        CommsChannel.Characters => _inputs.Characters.Count > 0,
        CommsChannel.Shuttle => _inputs.Shuttle is not null,
        _ => false,
    };

    private List<CommsSubChannel> PartiesOn(CommsChannel channel)
    {
        if (!IsAvailable(channel))
        {
            return [];
        }

        return channel switch
        {
            CommsChannel.Carrier => [new(CaptainKey, "Captain"), new(TowerKey, "Tower")],
            CommsChannel.Target when NpcTarget() is { } target =>
                [new(TargetKey, target.PilotName is { Length: > 0 } pilot ? pilot : target.Ship ?? "Target")],
            CommsChannel.System => [.. _inputs.Heard.Select(npc => new CommsSubChannel(npc.Name, npc.Name))],
            CommsChannel.Story =>
            [
                .. _inputs.StoryCast.Select(member => new CommsSubChannel(member.Id, member.Name)),
                .. _inputs.NarratorAnswers ? [new CommsSubChannel(NarratorKey, "Narrator")] : (CommsSubChannel[])[],
            ],
            CommsChannel.Crew =>
            [
                .. _inputs.Crew.Members
                    .Where(member => member.Active)
                    .Select(member => new CommsSubChannel(member.CrewId.ToString(CultureInfo.InvariantCulture), member.Name)),
            ],
            CommsChannel.Characters => [.. _inputs.Characters.Select(character => new CommsSubChannel(character.Id, character.Name))],
            CommsChannel.Shuttle when _inputs.Shuttle is { } ride => [new(PilotKey, PilotName(ride.Kind))],
            _ => [],
        };
    }

    /// <summary>The shuttle pilot's name for a kind of ride.</summary>
    public static string PilotName(ShuttleKind kind) => kind switch
    {
        ShuttleKind.Apex => "Apex pilot",
        ShuttleKind.Frontline => "Frontline pilot",
        _ => "Shuttle pilot",
    };

    private TargetedShip? NpcTarget() => _inputs.Target is { IsCommander: false } target ? target : null;

    private static CommsSubChannel? Match(List<CommsSubChannel> parties, string name)
    {
        var wanted = name.Trim();

        return parties.FirstOrDefault(party => Same(party.Name, wanted))
            ?? parties.FirstOrDefault(party => Same(LastWord(party.Name), wanted));
    }

    private static string LastWord(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is [.., var last] ? last : name;

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
