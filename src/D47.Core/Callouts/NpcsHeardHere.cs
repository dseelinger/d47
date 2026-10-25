using System.Text.RegularExpressions;
using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>Whether an NPC in the roster was made up or arrived in comms.</summary>
public enum NpcKind
{
    Invented,
    Comms,
}

/// <summary>
/// One NPC heard in the current system. The family is the canned one for a comms NPC, or null. The
/// personality is written once for the stay, or null before it is. The role, channel and allegiance are
/// what the NPC's message was voiced with.
/// </summary>
public sealed record NpcHeard(
    string Name,
    NpcKind Kind,
    string? Family,
    string? VoiceId,
    DateTimeOffset LastHeardAt,
    string? Personality = null,
    VoiceRole Role = VoiceRole.Comms,
    string? Channel = null,
    string? Allegiance = null)
{
    /// <summary>The message a comms NPC was heard in, with only its voice fields, or null for an invented NPC.</summary>
    public Announcement? HeardAs => Kind == NpcKind.Comms && Channel is { } channel
        ? IncomingMessages.VoicedAs(Name, Role, channel, isPlayer: false, Allegiance)
        : null;
}

/// <summary>
/// The NPCs heard in the current system, newest first. An NPC leaves on a jump or a sign-off and on
/// nothing else. Holds names, families and personalities, never message text.
/// </summary>
public sealed partial class NpcsHeardHere
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, NpcHeard> _heard = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The last line an invented NPC says before leaving.</summary>
    public static string SignOff(string name) => $"Got to go. {name} out.";

    /// <summary>Whether <paramref name="said"/> ends with <paramref name="name"/>'s sign-off.</summary>
    public static bool SignsOff(string said, string name) =>
        said.TrimEnd().EndsWith(SignOff(name), StringComparison.OrdinalIgnoreCase);

    /// <summary>Takes an NPC off the roster, as their sign-off does.</summary>
    public void Left(string name)
    {
        lock (_lock)
        {
            _heard.Remove(name);
        }
    }

    /// <summary>The NPCs heard here, most recently heard first.</summary>
    public IReadOnlyList<NpcHeard> Present
    {
        get
        {
            lock (_lock)
            {
                return [.. _heard.Values.OrderByDescending(npc => npc.LastHeardAt)];
            }
        }
    }

    /// <summary>The NPC heard here under this name, or null.</summary>
    public NpcHeard? Find(string name)
    {
        lock (_lock)
        {
            return _heard.GetValueOrDefault(name);
        }
    }

    /// <summary>Keeps a personality on the NPC until they leave; ignored when the NPC is not here.</summary>
    public void SetPersonality(string name, string personality)
    {
        lock (_lock)
        {
            if (_heard.TryGetValue(name, out var npc))
            {
                _heard[name] = npc with { Personality = personality };
            }
        }
    }

    /// <summary>Records a spoken announcement's speaker, or removes them if the line is their sign-off.</summary>
    public void Heard(Announcement announcement, string? voiceId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (announcement.Speaker is not { Length: > 0 } name || announcement.SpeakerIsPlayer)
        {
            return;
        }

        NpcKind kind;
        string? family = null;

        if (announcement.Invented is not null)
        {
            kind = NpcKind.Invented;

            if (SignedOff().Match(announcement.Text) is { Success: true } off
                && string.Equals(off.Groups[1].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    _heard.Remove(name);
                }

                return;
            }
        }
        else if (announcement.CommsChannel is not null)
        {
            kind = NpcKind.Comms;

            if (announcement.Key.StartsWith(IncomingMessages.CannedKeyPrefix, StringComparison.Ordinal))
            {
                family = announcement.Key[IncomingMessages.CannedKeyPrefix.Length..];
            }
        }
        else
        {
            return;
        }

        lock (_lock)
        {
            _heard[name] = new NpcHeard(
                name,
                kind,
                family,
                voiceId,
                at,
                _heard.GetValueOrDefault(name)?.Personality,
                announcement.Voice,
                announcement.CommsChannel,
                announcement.SpeakerAllegiance);
        }
    }

    /// <summary>A new system.</summary>
    public void EnteredSystem()
    {
        lock (_lock)
        {
            _heard.Clear();
        }
    }

    [GeneratedRegex(@"^Got to go\. (.+) out\.$")]
    private static partial Regex SignedOff();
}
