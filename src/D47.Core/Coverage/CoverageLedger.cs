using System.Security.Cryptography;
using System.Text;

namespace D47.Core.Coverage;

/// <summary>What kind of thing was exercised. Kinds keep ids from colliding across inventories.</summary>
public static class CoverageKind
{
    public const string Tool = "tool";
    public const string Setting = "setting";
}

/// <summary>
/// One thing a Commander can exercise, and a fingerprint of what it was at the time.
/// </summary>
/// <param name="Fingerprint">
/// A hash of the thing's own definition — for a tool, the canonical schema that the caching
/// invariant already guarantees is byte-identical across runs. When this changes, the thing
/// changed, and having exercised the old one says nothing about the new one.
/// </param>
public sealed record CoverageItem(string Kind, string Id, string Name, string Fingerprint)
{
    public string Key => $"{Kind}:{Id}";
}

/// <summary>Where one inventory item stands.</summary>
public enum CoverageStatus
{
    /// <summary>Never exercised in a real session.</summary>
    Never,

    /// <summary>Exercised, but it has changed since — so the last run proved nothing about it.</summary>
    Stale,

    /// <summary>Exercised as it stands now.</summary>
    Exercised,
}

/// <summary>One inventory item and where it stands.</summary>
public sealed record CoverageLine(CoverageItem Item, CoverageStatus Status, DateTimeOffset? LastSeen);

/// <summary>What has been exercised and what has not, as of one moment.</summary>
public sealed record CoverageReport(IReadOnlyList<CoverageLine> Lines)
{
    public int Total => Lines.Count;

    public int Exercised => Lines.Count(l => l.Status == CoverageStatus.Exercised);

    public int Stale => Lines.Count(l => l.Status == CoverageStatus.Stale);

    public int Never => Lines.Count(l => l.Status == CoverageStatus.Never);

    /// <summary>The one-line answer: how much of the app has been driven by hand.</summary>
    public string Summary => Total == 0
        ? "Nothing is registered to cover."
        : $"{Exercised} of {Total} exercised"
          + (Stale > 0 ? $", {Stale} changed since" : string.Empty)
          + (Never > 0 ? $", {Never} never" : string.Empty)
          + ".";

    /// <summary>
    /// The whole thing, for reading in a file rather than squinting at a panel row. Ordered by
    /// what still needs attention, because a list that opens with the work is a list that gets
    /// used.
    /// </summary>
    public string ToMarkdown(DateTimeOffset now)
    {
        var text = new StringBuilder();

        text.AppendLine("# What you have actually exercised");
        text.AppendLine();
        text.AppendLine($"As of {now:yyyy-MM-dd HH:mm}. {Summary}");
        text.AppendLine();

        Section(text, "Never exercised", CoverageStatus.Never);
        Section(text, "Changed since you last exercised them", CoverageStatus.Stale);
        Section(text, "Exercised", CoverageStatus.Exercised);

        return text.ToString();
    }

    private void Section(StringBuilder text, string heading, CoverageStatus status)
    {
        var lines = Lines.Where(l => l.Status == status)
            .OrderBy(l => l.Item.Kind, StringComparer.Ordinal)
            .ThenBy(l => l.Item.Id, StringComparer.Ordinal)
            .ToList();

        if (lines.Count == 0)
        {
            return;
        }

        text.AppendLine($"## {heading} ({lines.Count})");
        text.AppendLine();

        foreach (var line in lines)
        {
            var seen = line.LastSeen is { } when ? $" — last {when:yyyy-MM-dd}" : string.Empty;
            text.AppendLine($"- `{line.Item.Kind}` **{line.Item.Id}** — {line.Item.Name}{seen}");
        }

        text.AppendLine();
    }
}

/// <summary>
/// A record of what has been exercised in the running app, kept so "have I actually driven this
/// by hand, and has it changed since I did" has an answer that is not a person's memory.
/// <para>
/// This is a testing aid for whoever builds d47, not a feature. Nothing here is collected
/// unless it is switched on deliberately, and nothing leaves the machine — it is a file in
/// <c>data/</c> like everything else d47 writes.
/// </para>
/// <para>
/// No clock and no thread, like everything in Core: the caller passes the time in.
/// </para>
/// </summary>
public sealed class CoverageLedger
{
    private readonly Dictionary<string, CoverageMark> _marks;
    private readonly Lock _lock = new();

    public CoverageLedger(IEnumerable<KeyValuePair<string, CoverageMark>>? existing = null)
    {
        _marks = existing is null
            ? new Dictionary<string, CoverageMark>(StringComparer.Ordinal)
            : new Dictionary<string, CoverageMark>(existing, StringComparer.Ordinal);
    }

    /// <summary>Everything recorded so far, for persisting.</summary>
    public IReadOnlyDictionary<string, CoverageMark> Marks
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, CoverageMark>(_marks, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Whether anything has been recorded since the last time this was cleared.</summary>
    public bool Dirty { get; private set; }

    /// <summary>
    /// Notes that something ran, with the fingerprint it had when it did. Recording the
    /// fingerprint at exercise time is the whole mechanism: a later comparison against the
    /// current one is what turns "I tested that" into "I tested what it used to be".
    /// </summary>
    public void Record(CoverageItem item, DateTimeOffset now)
    {
        lock (_lock)
        {
            _marks[item.Key] = new CoverageMark(item.Fingerprint, now);
            Dirty = true;
        }
    }

    /// <summary>Marks the ledger as saved.</summary>
    public void Saved() => Dirty = false;

    /// <summary>Where each thing in <paramref name="inventory"/> stands right now.</summary>
    public CoverageReport Report(IEnumerable<CoverageItem> inventory)
    {
        lock (_lock)
        {
            return new CoverageReport(
            [
                .. inventory.Select(item =>
                {
                    if (!_marks.TryGetValue(item.Key, out var mark))
                    {
                        return new CoverageLine(item, CoverageStatus.Never, null);
                    }

                    var status = string.Equals(mark.Fingerprint, item.Fingerprint, StringComparison.Ordinal)
                        ? CoverageStatus.Exercised
                        : CoverageStatus.Stale;

                    return new CoverageLine(item, status, mark.When);
                }),
            ]);
        }
    }

    /// <summary>
    /// A short, stable hash of whatever defines a thing. Truncated: this identifies a version of
    /// a definition in a file a person reads, not a security boundary.
    /// </summary>
    public static string Fingerprint(string definition) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition)))[..16];
}

/// <summary>One recorded exercise: what it looked like, and when.</summary>
public sealed record CoverageMark(string Fingerprint, DateTimeOffset When);
