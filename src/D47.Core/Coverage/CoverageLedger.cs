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
public sealed record CoverageItem(
    string Kind,
    string Id,
    string CapabilityId,
    string Name,
    string Fingerprint)
{
    public string Key => $"{Kind}:{Id}";
}

/// <summary>
/// How it went the last time it was exercised.
/// <para>
/// <c>Ok</c> is deliberately the zero value, so a coverage.json written before outcomes
/// existed loads as "it ran and nothing said otherwise" rather than as a failure nobody saw.
/// </para>
/// </summary>
public enum CoverageOutcome
{
    /// <summary>It ran and came back clean.</summary>
    Ok,

    /// <summary>
    /// It ran and reported failure. Not a crash — a capability failing is a state, not a
    /// crash — but a state worth going back to, which is the whole point of recording it.
    /// </summary>
    Failed,
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
/// <param name="Outcome">
/// Null when it has never been exercised, because there is no outcome to report — which is a
/// different thing from having run without complaint.
/// </param>
public sealed record CoverageLine(
    CoverageItem Item,
    CoverageStatus Status,
    CoverageOutcome? Outcome,
    DateTimeOffset? LastSeen)
{
    public bool Failed => Outcome == CoverageOutcome.Failed;
}

/// <summary>What has been exercised and what has not, as of one moment.</summary>
public sealed record CoverageReport(IReadOnlyList<CoverageLine> Lines)
{
    public int Total => Lines.Count;

    public int Exercised => Lines.Count(l => l.Status == CoverageStatus.Exercised);

    public int Stale => Lines.Count(l => l.Status == CoverageStatus.Stale);

    public int Never => Lines.Count(l => l.Status == CoverageStatus.Never);

    /// <summary>
    /// How many came back with an error the last time they ran. Not part of the
    /// exercised/stale/never split — those three partition the inventory, and a failure is
    /// something that happened to a line already counted in one of them.
    /// </summary>
    public int Failed => Lines.Count(l => l.Failed);

    /// <summary>The one-line answer: how much of the app has been driven by hand.</summary>
    public string Summary => Total == 0
        ? "Nothing is registered to cover."
        : $"{Exercised} of {Total} exercised"
          + (Stale > 0 ? $", {Stale} changed since" : string.Empty)
          + (Never > 0 ? $", {Never} never" : string.Empty)
          + "."
          // Its own sentence, so the count is never read as a share of the ones before it.
          + (Failed > 0 ? $" {Failed} came back with an error." : string.Empty);

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

        // Failures first, and excluded from the sections below so nothing is listed twice.
        // Something that ran and reported an error is a stronger call to action than something
        // that was never tried at all.
        Section(text, "Came back with an error", l => l.Failed);
        Section(text, "Never exercised", l => !l.Failed && l.Status == CoverageStatus.Never);
        Section(text, "Changed since you last exercised them", l => !l.Failed && l.Status == CoverageStatus.Stale);
        Section(text, "Exercised", l => !l.Failed && l.Status == CoverageStatus.Exercised);

        return text.ToString();
    }

    private void Section(StringBuilder text, string heading, Func<CoverageLine, bool> include)
    {
        var lines = Lines.Where(include)
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
    /// <param name="outcome">
    /// Required rather than defaulted. A call site that forgot to say how it went would record
    /// a silent green, which is the one answer this whole record exists to stop being guessed.
    /// </param>
    public void Record(CoverageItem item, DateTimeOffset now, CoverageOutcome outcome)
    {
        lock (_lock)
        {
            // Last write wins, in both directions: something fixed since it last failed goes
            // green again, and something that has started failing goes red however long it
            // worked before.
            _marks[item.Key] = new CoverageMark(item.Fingerprint, now, outcome);
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
                        return new CoverageLine(item, CoverageStatus.Never, null, null);
                    }

                    var status = string.Equals(mark.Fingerprint, item.Fingerprint, StringComparison.Ordinal)
                        ? CoverageStatus.Exercised
                        : CoverageStatus.Stale;

                    return new CoverageLine(item, status, mark.Outcome, mark.When);
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

/// <summary>One recorded exercise: what it looked like, when, and how it went.</summary>
/// <param name="Outcome">
/// Defaulted so a record written before outcomes existed deserialises rather than throwing.
/// It lands on <see cref="CoverageOutcome.Ok"/> either way, since that is also the zero value.
/// </param>
public sealed record CoverageMark(
    string Fingerprint,
    DateTimeOffset When,
    CoverageOutcome Outcome = CoverageOutcome.Ok);
