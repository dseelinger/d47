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
                    Secret = Secret(record.Secret),
                    Informant = Informant(record.Informant),
                    Ideology = Ideology(record.Ideology),
                    Council = Council(record.Council),
                    Crises = [.. (record.Crises ?? []).Where(crisis => !string.IsNullOrWhiteSpace(crisis.System) && !string.IsNullOrWhiteSpace(crisis.State)).Select(crisis => new OpenCrisis(crisis.System!, crisis.State!, string.IsNullOrWhiteSpace(crisis.Blamed) ? null : crisis.Blamed, crisis.Since, crisis.Exploited))],
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
            Secret = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Secret : null,
            Informant = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Informant : null,
            Ideology = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Ideology : null,
            Council = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Council : null,
            Crises = string.Equals(campaign.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase) ? campaign.Crises : [],
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

        var kept = Change(
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

        if (kept)
        {
            ChronicleGrew?.Invoke(frontierId);
        }

        return kept;
    }

    /// <summary>Raised, with the Commander's Frontier id, after a front enters the chronicle. Handlers must not block.</summary>
    public event Action<string>? ChronicleGrew;

    /// <summary>Keeps <paramref name="legend"/> on chronicle entry <paramref name="index"/>, only while that entry is still <paramref name="asked"/> and has none. False otherwise, or when the write failed.</summary>
    public bool SetLegend(string frontierId, int index, ChronicleEntry asked, string legend)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentException.ThrowIfNullOrWhiteSpace(legend);

        var set = false;

        return Change(
            frontierId,
            campaign =>
            {
                if (index < 0
                    || index >= campaign.Chronicle.Count
                    || !string.IsNullOrWhiteSpace(campaign.Chronicle[index].Legend)
                    || !SameEntry(campaign.Chronicle[index], asked))
                {
                    return campaign;
                }

                set = true;

                return campaign with
                {
                    Chronicle = [.. campaign.Chronicle.Select((entry, at) => at == index ? entry with { Legend = legend.Trim() } : entry)],
                };
            })
            && set;
    }

    /// <summary>Sets the open crises. False when the write failed.</summary>
    public bool SetCrises(string frontierId, IReadOnlyList<OpenCrisis> crises)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentNullException.ThrowIfNull(crises);

        return Change(frontierId, campaign => campaign with { Crises = crises });
    }

    private static bool SameEntry(ChronicleEntry left, ChronicleEntry right) =>
        left.Kind == right.Kind
        && string.Equals(left.System, right.System, StringComparison.OrdinalIgnoreCase)
        && left.Outcome == right.Outcome
        && left.Ticks.SequenceEqual(right.Ticks);

    /// <summary>Sets the arc under way, or clears it with null, clearing a secret that belongs to another rival. False when the write failed.</summary>
    public bool SetArc(string frontierId, CampaignArc? arc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        var opened = false;

        var kept = Change(
            frontierId,
            campaign =>
            {
                opened = arc is not null && campaign.Arc is null;

                return campaign with
                {
                    Arc = arc,
                    Secret = arc is not null && campaign.Secret is { } secret && string.Equals(secret.Rival, arc.Rival, StringComparison.OrdinalIgnoreCase)
                        ? secret
                        : null,
                };
            });

        if (kept && opened)
        {
            ArcOpened?.Invoke(frontierId);
        }

        return kept;
    }

    /// <summary>Raised, with the Commander's Frontier id, after an arc is set where there was none. Handlers must not block.</summary>
    public event Action<string>? ArcOpened;

    /// <summary>
    /// Keeps <paramref name="secret"/> for the arc, only while the arc under way has <paramref name="asked"/>'s rival and
    /// start and no secret; its From is the chronicle's count now. False otherwise, or when the write failed.
    /// </summary>
    public bool SetSecret(string frontierId, CampaignArc asked, ArcSecret secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(secret);

        var set = false;

        return Change(
            frontierId,
            campaign =>
            {
                if (campaign.Secret is not null
                    || campaign.Arc is not { } arc
                    || !string.Equals(arc.Rival, asked.Rival, StringComparison.OrdinalIgnoreCase)
                    || arc.Since != asked.Since
                    || !string.Equals(secret.Rival, arc.Rival, StringComparison.OrdinalIgnoreCase))
                {
                    return campaign;
                }

                set = true;

                return campaign with { Secret = secret with { From = campaign.Chronicle.Count, Revealed = [] } };
            })
            && set;
    }

    /// <summary>Replaces the secret with <paramref name="next"/>, only while the secret held is still <paramref name="held"/>. False otherwise, or when the write failed.</summary>
    public bool SetRevealed(string frontierId, ArcSecret held, ArcSecret next)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(next);

        var set = false;

        return Change(
            frontierId,
            campaign =>
            {
                if (!ReferenceEquals(campaign.Secret, held))
                {
                    return campaign;
                }

                set = true;

                return campaign with { Secret = next };
            })
            && set;
    }

    /// <summary>Sets the Informant and the leaks given, or clears it with null. False when the write failed.</summary>
    public bool SetInformant(string frontierId, CampaignInformant? informant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(frontierId, campaign => campaign with { Informant = informant });
    }

    /// <summary>Sets the Ideology arc, or clears it with null. False when the write failed.</summary>
    public bool SetIdeology(string frontierId, IdeologyArc? ideology)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(frontierId, campaign => campaign with { Ideology = ideology });
    }

    /// <summary>Sets the council, or clears it with null. False when the write failed.</summary>
    public bool SetCouncil(string frontierId, CampaignCouncil? council)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontierId);

        return Change(frontierId, campaign => campaign with { Council = council });
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
                            Secret = pair.Value.Secret is { } secret
                                ? new SecretRecord
                                {
                                    Rival = secret.Rival,
                                    Kind = secret.Kind,
                                    Culprit = secret.Culprit,
                                    Truth = secret.Truth,
                                    Reveals = [.. secret.Reveals],
                                    From = secret.From,
                                    Revealed = [.. secret.Revealed.Select(point => new RevealRecord { Entry = point.Entry, System = point.System, At = point.At })],
                                }
                                : null,
                            Informant = pair.Value.Informant is { } informant
                                ? new InformantRecord
                                {
                                    Name = informant.Name,
                                    Since = informant.Since,
                                    Leaks = [.. informant.Leaks.Select(leak => new LeakRecord
                                    {
                                        Kind = leak.Kind, System = leak.System, Rival = leak.Rival, Influence = leak.Influence, Level = leak.Level, At = leak.At,
                                    })],
                                }
                                : null,
                            Ideology = pair.Value.Ideology is { } ideology
                                ? new IdeologyRecord
                                {
                                    Rival = ideology.Rival,
                                    OurGovernment = ideology.OurGovernment,
                                    TheirGovernment = ideology.TheirGovernment,
                                    Since = ideology.Since,
                                    Answered = ideology.Answered,
                                    Fronts = [.. ideology.Fronts],
                                    Elections = [.. ideology.Elections],
                                }
                                : null,
                            Council = pair.Value.Council is { } council
                                ? new CouncilRecord
                                {
                                    Hawk = council.Hawk,
                                    Dove = council.Dove,
                                    Since = council.Since,
                                    Noted = council.Noted,
                                    Sidings = [.. council.Sidings.Select(siding => new SidingRecord { System = siding.System, Sided = siding.Sided, At = siding.At })],
                                }
                                : null,
                            Crises = [.. pair.Value.Crises.Select(crisis => new CrisisRecord { System = crisis.System, State = crisis.State, Blamed = crisis.Blamed, Since = crisis.Since, Exploited = crisis.Exploited })],
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
                [.. (record.Work ?? []).Where(work => Enum.IsDefined(work.Kind)).Select(work => new ChronicleWork(work.Kind, work.Amount))])
            {
                Legend = string.IsNullOrWhiteSpace(record.Legend) ? null : record.Legend,
            };

    private static CampaignArc? Arc(ArcRecord? record) =>
        record is { Rival: { Length: > 0 } rival, Leader: { Length: > 0 } leader } && Enum.IsDefined(record.Kind)
            ? new CampaignArc(record.Kind, rival, leader, record.Since, Math.Max(0, record.Answered))
            : null;

    private static ArcSecret? Secret(SecretRecord? record) =>
        record is { Rival: { Length: > 0 } rival, Culprit: { Length: > 0 } culprit, Truth: { Length: > 0 } truth, Reveals: { } reveals }
            && Enum.IsDefined(record.Kind)
            && reveals.Count == CampaignSecrets.RevealCount
            && reveals.All(reveal => !string.IsNullOrWhiteSpace(reveal))
            ? new ArcSecret(
                rival,
                record.Kind,
                culprit,
                truth,
                [.. reveals],
                Math.Max(0, record.From),
                [.. (record.Revealed ?? [])
                    .Where(point => point.Entry >= 0 && !string.IsNullOrWhiteSpace(point.System))
                    .Select(point => new RevealPoint(point.Entry, point.System!, point.At))
                    .Take(CampaignSecrets.RevealCount)])
            : null;

    private static CampaignInformant? Informant(InformantRecord? record) =>
        record is { Name: { Length: > 0 } name }
            ? new CampaignInformant(
                name,
                record.Since,
                [.. (record.Leaks ?? [])
                    .Where(leak => Enum.IsDefined(leak.Kind) && !string.IsNullOrWhiteSpace(leak.System) && !string.IsNullOrWhiteSpace(leak.Rival))
                    .Select(leak => new Leak(leak.Kind, leak.System!, leak.Rival!, leak.Influence, Math.Clamp(leak.Level, 1, CampaignLeaks.MostLevels), leak.At))])
            : null;

    private static IdeologyArc? Ideology(IdeologyRecord? record) =>
        record is { Rival: { Length: > 0 } rival, OurGovernment: { Length: > 0 } ours, TheirGovernment: { Length: > 0 } theirs }
            ? new IdeologyArc(
                rival,
                ours,
                theirs,
                record.Since,
                Math.Max(0, record.Answered),
                [.. (record.Fronts ?? []).Where(system => !string.IsNullOrWhiteSpace(system))],
                [.. (record.Elections ?? []).Where(key => !string.IsNullOrWhiteSpace(key))])
            : null;

    private static CampaignCouncil? Council(CouncilRecord? record) =>
        record is { Hawk: { Length: > 0 } hawk, Dove: { Length: > 0 } dove }
            ? new CampaignCouncil(
                hawk,
                dove,
                record.Since,
                record.Noted,
                [.. (record.Sidings ?? [])
                    .Where(siding => Enum.IsDefined(siding.Sided) && !string.IsNullOrWhiteSpace(siding.System))
                    .Select(siding => new CouncilSiding(siding.System!, siding.Sided, siding.At))])
            : null;

    private static EntryRecord ToRecord(ChronicleEntry entry) => new()
    {
        Kind = entry.Kind,
        System = entry.System,
        Opponent = entry.Opponent,
        Outcome = entry.Outcome,
        Ticks = [.. entry.Ticks],
        Work = [.. entry.Work.Select(work => new WorkRecord { Kind = work.Kind, Amount = work.Amount })],
        Legend = entry.Legend,
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

        public SecretRecord? Secret { get; set; }

        public InformantRecord? Informant { get; set; }

        public IdeologyRecord? Ideology { get; set; }

        public CouncilRecord? Council { get; set; }

        public IReadOnlyList<CrisisRecord>? Crises { get; set; }
    }

    private sealed class CrisisRecord
    {
        public string? System { get; set; }

        public string? State { get; set; }

        public string? Blamed { get; set; }

        public DateTimeOffset Since { get; set; }

        public bool Exploited { get; set; }
    }

    private sealed class IdeologyRecord
    {
        public string? Rival { get; set; }

        public string? OurGovernment { get; set; }

        public string? TheirGovernment { get; set; }

        public DateTimeOffset Since { get; set; }

        public int Answered { get; set; }

        public IReadOnlyList<string>? Fronts { get; set; }

        public IReadOnlyList<string>? Elections { get; set; }
    }

    private sealed class CouncilRecord
    {
        public string? Hawk { get; set; }

        public string? Dove { get; set; }

        public DateTimeOffset Since { get; set; }

        public DateTimeOffset? Noted { get; set; }

        public IReadOnlyList<SidingRecord>? Sidings { get; set; }
    }

    private sealed class SidingRecord
    {
        public string? System { get; set; }

        public CouncilSide Sided { get; set; }

        public DateTimeOffset At { get; set; }
    }

    private sealed class InformantRecord
    {
        public string? Name { get; set; }

        public DateTimeOffset Since { get; set; }

        public IReadOnlyList<LeakRecord>? Leaks { get; set; }
    }

    private sealed class LeakRecord
    {
        public LeakKind Kind { get; set; }

        public string? System { get; set; }

        public string? Rival { get; set; }

        public double Influence { get; set; }

        public int Level { get; set; }

        public DateTimeOffset At { get; set; }
    }

    private sealed class SecretRecord
    {
        public string? Rival { get; set; }

        public SecretKind Kind { get; set; }

        public string? Culprit { get; set; }

        public string? Truth { get; set; }

        public IReadOnlyList<string>? Reveals { get; set; }

        public int From { get; set; }

        public IReadOnlyList<RevealRecord>? Revealed { get; set; }
    }

    private sealed class RevealRecord
    {
        public int Entry { get; set; }

        public string? System { get; set; }

        public DateTimeOffset At { get; set; }
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

        public string? Legend { get; set; }
    }

    private sealed class WorkRecord
    {
        public BgsWorkKind Kind { get; set; }

        public long Amount { get; set; }
    }
}
