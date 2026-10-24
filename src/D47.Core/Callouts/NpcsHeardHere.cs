using System.Text.RegularExpressions;

namespace D47.Core.Callouts;

/// <summary>Whether an NPC in the roster was made up or arrived in comms.</summary>
public enum NpcKind
{
    Invented,
    Comms,
}

/// <summary>One NPC heard in the current system. The family is the canned one for a comms NPC, or null.</summary>
public sealed record NpcHeard(string Name, NpcKind Kind, string? Family, string? VoiceId, DateTimeOffset LastHeardAt);

/// <summary>
/// The NPCs heard in the current system, newest first. An NPC leaves on a jump or a sign-off and on
/// nothing else. Holds names and families, never message text.
/// </summary>
public sealed partial class NpcsHeardHere
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, NpcHeard> _heard = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The last line an invented NPC says before leaving.</summary>
    public static string SignOff(string name) => $"Got to go. {name} out.";

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
            _heard[name] = new NpcHeard(name, kind, family, voiceId, at);
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
