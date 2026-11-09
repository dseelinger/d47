using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Campaigns;

/// <summary>Each Commander's campaign, by Frontier id, kept at <c>data/campaigns.json</c>.</summary>
public sealed class CampaignStore(string path, IFileSystem files, ILogger<CampaignStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock _gate = new();

    /// <summary>Held across the read that decides a change and the write that applies it.</summary>
    private readonly Lock _writeGate = new();

    private ImmutableDictionary<string, Campaign> _byCommander = ImmutableDictionary.Create<string, Campaign>(StringComparer.Ordinal);

    /// <summary>Set when the file exists and could not be read; nothing is written over it.</summary>
    private volatile bool _unreadable;

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var loaded = ImmutableDictionary.CreateBuilder<string, Campaign>(StringComparer.Ordinal);

            foreach (var record in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(record.FrontierId))
                {
                    continue;
                }

                loaded[record.FrontierId] = new Campaign
                {
                    Faction = string.IsNullOrWhiteSpace(record.Faction) ? null : record.Faction,
                    Intents = (record.Intents ?? [])
                        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && Enum.IsDefined(pair.Value))
                        .Aggregate(
                            ImmutableDictionary.Create<string, SystemIntent>(StringComparer.OrdinalIgnoreCase),
                            (intents, pair) => intents.SetItem(pair.Key, pair.Value)),
                    Chosen = record.Chosen is { System: { Length: > 0 } chosenSystem } chosen && Enum.IsDefined(chosen.Kind)
                        ? new ChosenFront(chosen.Kind, chosenSystem, chosen.Since, chosen.Ticks ?? []) { Reported = chosen.Reported }
                        : null,
                    Chronicle = [.. (record.Chronicle ?? []).Select(Entry).OfType<ChronicleEntry>()],
                    Arc = Arc(record.Arc),
                };
            }

            lock (_gate)
            {
                _byCommander = loaded.ToImmutable();
            }

            _unreadable = false;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            _unreadable = true;
            logger.LogWarning(ex, "Could not read {Path}; no campaigns are held and none is written until it can be read", path);
        }
    }

    /// <summary>This Commander's campaign, or <see cref="Campaign.None"/>.</summary>
    public Campaign For(string? frontierId)
    {
        if (string.IsNullOrEmpty(frontierId))
        {
            return Campaign.None;
        }

        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId) ?? Campaign.None;
        }
    }

    /// <summary>Names the campaign faction; <paramref name="clearIntents"/> drops the intents set for the faction before. False when the write failed.</summary>
    public bool SetFaction(string frontierId, string faction, bool clearIntents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(faction);

        return Change(frontierId, campaign => campaign with
        {
            Faction = faction.Trim(),
            Intents = clearIntents ? campaign.Intents.Clear() : campaign.Intents,
            Chosen = null,
            Arc = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Arc : null,
        });
    }

    /// <summary>Makes <paramref name="kind"/> in <paramref name="system"/> the front being worked. False when the write failed.</summary>
    public bool Choose(string frontierId, FrontKind kind, string system, DateTimeOffset since)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        return Change(frontierId, campaign => campaign with { Chosen = new ChosenFront(kind, system.Trim(), since, []) });
    }

    /// <summary>Drops the front being worked without a chronicle entry. False when the write failed or none was chosen.</summary>
    public bool Drop(string frontierId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return For(frontierId).Chosen is not null && Change(frontierId, campaign => campaign with { Chosen = null });
    }

    /// <summary>Notes a BGS tick on the front being worked, once per tick.</summary>
    public bool NoteTick(string frontierId, FrontKind kind, string system, DateTimeOffset tick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(
            frontierId,
            campaign => campaign.Chosen is { } chosen && Is(chosen, kind, system) && !chosen.Ticks.Contains(tick)
                ? campaign with { Chosen = chosen with { Ticks = [.. chosen.Ticks, tick] } }
                : campaign);
    }

    /// <summary>Notes the BGS tick the Liaison gave its dispatch for on the front being worked.</summary>
    public bool NoteReported(string frontierId, FrontKind kind, string system, DateTimeOffset tick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(
            frontierId,
            campaign => campaign.Chosen is { } chosen && Is(chosen, kind, system) && chosen.Reported != tick
                ? campaign with { Chosen = chosen with { Reported = tick } }
                : campaign);
    }

    /// <summary>
    /// Adds <paramref name="entry"/> to the chronicle and clears the chosen front, only while that front is still the
    /// one chosen. False when it is not, or the write failed.
    /// </summary>
    public bool Resolve(string frontierId, FrontKind kind, string system, ChronicleEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentNullException.ThrowIfNull(entry);

        var resolved = false;

        return Change(
            frontierId,
            campaign =>
            {
                if (campaign.Chosen is not { } chosen || !Is(chosen, kind, system))
                {
                    return campaign;
                }

                resolved = true;

                return campaign with { Chosen = null, Chronicle = [.. campaign.Chronicle, entry] };
            })
            && resolved;
    }

    /// <summary>Sets the arc under way, or clears it with null. False when the write failed.</summary>
    public bool SetArc(string frontierId, CampaignArc? arc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(frontierId, campaign => campaign with { Arc = arc });
    }

    private static bool Is(ChosenFront chosen, FrontKind kind, string system) =>
        chosen.Kind == kind && string.Equals(chosen.System, system, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sets the intent for one system. False when the write failed.</summary>
    public bool SetIntent(string frontierId, string system, SystemIntent intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(system);

        var name = system.Trim();

        return Change(frontierId, campaign => campaign with { Intents = campaign.Intents.Remove(name).Add(name, intent) });
    }

    private bool Change(string frontierId, Func<Campaign, Campaign> change)
    {
        lock (_writeGate)
        {
            ImmutableDictionary<string, Campaign> held;

            lock (_gate)
            {
                held = _byCommander;
            }

            var next = held.SetItem(frontierId, change(held.GetValueOrDefault(frontierId) ?? Campaign.None));

            if (!Write(next))
            {
                return false;
            }

            lock (_gate)
            {
                _byCommander = next;
            }

            return true;
        }
    }

    private bool Write(ImmutableDictionary<string, Campaign> byCommander)
    {
        if (_unreadable)
        {
            return false;
        }

        try
        {
            var document = new Document
            {
                Commanders =
                [
                    .. byCommander
                        .Where(pair => !pair.Value.IsEmpty)
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new CommanderRecord
                        {
                            FrontierId = pair.Key,
                            Faction = pair.Value.Faction,
                            Intents = pair.Value.Intents
                                .OrderBy(intent => intent.Key, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(intent => intent.Key, intent => intent.Value, StringComparer.OrdinalIgnoreCase),
                            Chosen = pair.Value.Chosen is { } held
                                ? new ChosenRecord { Kind = held.Kind, System = held.System, Since = held.Since, Ticks = [.. held.Ticks], Reported = held.Reported }
                                : null,
                            Chronicle = [.. pair.Value.Chronicle.Select(ToRecord)],
                            Arc = pair.Value.Arc is { } arc
                                ? new ArcRecord { Kind = arc.Kind, Rival = arc.Rival, Leader = arc.Leader, Since = arc.Since, Answered = arc.Answered }
                                : null,
                        }),
                ],
            };

            files.WriteText(path, JsonSerializer.Serialize(document, Json));
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return false;
        }
    }

    private static ChronicleEntry? Entry(EntryRecord record) =>
        string.IsNullOrWhiteSpace(record.Kind) || string.IsNullOrWhiteSpace(record.System) || string.IsNullOrWhiteSpace(record.Outcome)
            ? null
            : new ChronicleEntry(
                record.Kind,
                record.System,
                string.IsNullOrWhiteSpace(record.Opponent) ? null : record.Opponent,
                record.Outcome,
                record.Ticks ?? [],
                [.. (record.Work ?? []).Where(work => Enum.IsDefined(work.Kind)).Select(work => new ChronicleWork(work.Kind, work.Amount))]);

    private static CampaignArc? Arc(ArcRecord? record) =>
        record is { Rival: { Length: > 0 } rival, Leader: { Length: > 0 } leader } && Enum.IsDefined(record.Kind)
            ? new CampaignArc(record.Kind, rival, leader, record.Since, Math.Max(0, record.Answered))
            : null;

    private static EntryRecord ToRecord(ChronicleEntry entry) => new()
    {
        Kind = entry.Kind,
        System = entry.System,
        Opponent = entry.Opponent,
        Outcome = entry.Outcome,
        Ticks = [.. entry.Ticks],
        Work = [.. entry.Work.Select(work => new WorkRecord { Kind = work.Kind, Amount = work.Amount })],
    };

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord>? Commanders { get; set; }
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public string? Faction { get; set; }

        public Dictionary<string, SystemIntent>? Intents { get; set; }

        public ChosenRecord? Chosen { get; set; }

        public IReadOnlyList<EntryRecord>? Chronicle { get; set; }

        public ArcRecord? Arc { get; set; }
    }

    private sealed class ArcRecord
    {
        public ArcKind Kind { get; set; }

        public string? Rival { get; set; }

        public string? Leader { get; set; }

        public DateTimeOffset Since { get; set; }

        public int Answered { get; set; }
    }

    private sealed class ChosenRecord
    {
        public FrontKind Kind { get; set; }

        public string? System { get; set; }

        public DateTimeOffset Since { get; set; }

        public IReadOnlyList<DateTimeOffset>? Ticks { get; set; }

        public DateTimeOffset? Reported { get; set; }
    }

    private sealed class EntryRecord
    {
        public string? Kind { get; set; }

        public string? System { get; set; }

        public string? Opponent { get; set; }

        public string? Outcome { get; set; }

        public IReadOnlyList<DateTimeOffset>? Ticks { get; set; }

        public IReadOnlyList<WorkRecord>? Work { get; set; }
    }

    private sealed class WorkRecord
    {
        public BgsWorkKind Kind { get; set; }

        public long Amount { get; set; }
    }
}
