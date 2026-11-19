using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Which kinds a catalogue page lists.</summary>
public enum CodexKindStatus
{
    All,
    Missing,
    OnRecord,
}

/// <summary>The earliest find of a kind in a scope. <c>Body</c> is null for a Canonn entry logged without one.</summary>
public sealed record CodexFirstFind(DateTimeOffset At, string? System, string? Body, int? RegionId);

/// <summary>
/// One codex entry that counts in a scope: its known systems in the scope and in every region, and its first find in
/// the scope, null when it is missing.
/// </summary>
public sealed record CodexKindDiscovery(CodexEntry Entry, int Systems, int SystemsAllRegions, CodexFirstFind? FirstFind)
{
    public bool IsOnRecord => FirstFind is not null;
}

/// <summary>
/// A tree node with the kinds that count under it. <c>Counted</c>, <c>OnRecord</c> and <c>Missing</c> cover every
/// kind in the subtree whatever the status filter; <c>Kinds</c> are the node's leaf entries (the node's own entry when
/// it is a leaf), and <c>Groups</c> its other children, both after the filter. A row with children is a group, never a kind.
/// </summary>
public sealed record CodexNodeDiscoveries(
    CodexNode Node,
    int Counted,
    int OnRecord,
    int Missing,
    IReadOnlyList<CodexKindDiscovery> Kinds,
    IReadOnlyList<CodexNodeDiscoveries> Groups)
{
    /// <summary>
    /// Compares the kinds under <paramref name="node"/> with the ledger in one region, or in every region when
    /// <paramref name="regionId"/> is null. In a region a kind counts when the table lists it there or the ledger found
    /// it there; in every region all kinds count. A group with no kind left under the filter is left out.
    /// </summary>
    public static CodexNodeDiscoveries For(
        CodexLedger ledger, CodexNode node, int? regionId, CodexKindStatus filter = CodexKindStatus.All)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(node);

        var firstCanonn = ledger.Entries
            .Where(entry => regionId is null || entry.RegionId == regionId)
            .GroupBy(entry => CodexKeys.Of(entry.EntryId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.MinBy(entry => entry.At)!,
                StringComparer.Ordinal);
        var firstKind = regionId is null
            ? ledger.FoundKinds
                .GroupBy(kind => kind.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.MinBy(kind => kind.At)!, StringComparer.Ordinal)
            : null;

        return Build(ledger, node, regionId, filter, firstCanonn, firstKind);
    }

    private static CodexNodeDiscoveries Build(
        CodexLedger ledger,
        CodexNode node,
        int? regionId,
        CodexKindStatus filter,
        Dictionary<string, CodexLogEntry> firstCanonn,
        Dictionary<string, CodexFoundKind>? firstKind)
    {
        var shown = new List<CodexKindDiscovery>();
        var groups = new List<CodexNodeDiscoveries>();
        var counted = 0;
        var onRecord = 0;

        void Own(CodexNode entryNode)
        {
            if (Discover(ledger, entryNode.Entry!, regionId, firstCanonn, firstKind) is not { } kind)
            {
                return;
            }

            counted++;
            onRecord += kind.IsOnRecord ? 1 : 0;

            if (Passes(kind, filter))
            {
                shown.Add(kind);
            }
        }

        if (node.Entry is not null && node.Children.Count == 0)
        {
            Own(node);
        }

        foreach (var child in node.Children)
        {
            if (child.Children.Count == 0)
            {
                if (child.Entry is not null)
                {
                    Own(child);
                }

                continue;
            }

            var group = Build(ledger, child, regionId, filter, firstCanonn, firstKind);
            counted += group.Counted;
            onRecord += group.OnRecord;

            if (group.Kinds.Count > 0 || group.Groups.Count > 0)
            {
                groups.Add(group);
            }
        }

        return new CodexNodeDiscoveries(node, counted, onRecord, counted - onRecord, shown, groups);
    }

    private static bool Passes(CodexKindDiscovery kind, CodexKindStatus filter) =>
        filter switch
        {
            CodexKindStatus.Missing => !kind.IsOnRecord,
            CodexKindStatus.OnRecord => kind.IsOnRecord,
            _ => true,
        };

    private static CodexKindDiscovery? Discover(
        CodexLedger ledger,
        CodexEntry entry,
        int? regionId,
        Dictionary<string, CodexLogEntry> firstCanonn,
        Dictionary<string, CodexFoundKind>? firstKind)
    {
        var allRegions = entry.Regions.Values.Sum();
        CodexFirstFind? first;

        if (entry.Kind is null)
        {
            first = firstCanonn.TryGetValue(entry.Key, out var logged)
                ? new CodexFirstFind(logged.At, logged.System, logged.Body, logged.RegionId)
                : null;
        }
        else
        {
            var found = regionId is { } region
                ? ledger.Found(entry.Key, region)
                : firstKind!.GetValueOrDefault(entry.Key);
            first = found is null ? null : new CodexFirstFind(found.At, found.System, found.Body, found.RegionId);
        }

        if (regionId is { } scope && !entry.Regions.ContainsKey(scope) && first is null)
        {
            return null;
        }

        var systems = regionId is { } at ? entry.Regions.GetValueOrDefault(at) : allRegions;

        return new CodexKindDiscovery(entry, systems, allRegions, first);
    }
}
