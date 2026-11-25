using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Which kinds a catalogue page lists.</summary>
public enum CodexKindStatus
{
    All,
    Missing,
    MissingImage,
    OnRecord,
}

/// <summary>The earliest find of a kind in a scope. <c>Body</c> is null for a Canonn entry logged without one.</summary>
public sealed record CodexFirstFind(DateTimeOffset At, string? System, string? Body, int? RegionId);

/// <summary>
/// One codex entry that counts in a scope: its known systems in the scope and in every region, and its first find in
/// the scope, null when it is missing. <c>MissingImage</c> is true when the kind is on record in a region of the scope
/// with no picture of the Commander's own for that region.
/// </summary>
public sealed record CodexKindDiscovery(
    CodexEntry Entry, int Systems, int SystemsAllRegions, CodexFirstFind? FirstFind, bool MissingImage = false)
{
    public bool IsOnRecord => FirstFind is not null;
}

/// <summary>
/// A tree node with the kinds that count under it. <c>Counted</c>, <c>OnRecord</c>, <c>Missing</c> and <c>MissingImage</c> cover every
/// kind in the subtree whatever the status filter; <c>Kinds</c> are the node's leaf entries (the node's own entry when
/// it is a leaf), and <c>Groups</c> its other children, both after the filter. A row with children is a group, never a kind.
/// </summary>
public sealed record CodexNodeDiscoveries(
    CodexNode Node,
    int Counted,
    int OnRecord,
    int Missing,
    IReadOnlyList<CodexKindDiscovery> Kinds,
    IReadOnlyList<CodexNodeDiscoveries> Groups,
    int MissingImage = 0)
{
    /// <summary>
    /// Compares the kinds under <paramref name="node"/> with the ledger in one region, or in every region when
    /// <paramref name="regionId"/> is null. In a region a kind counts when the table lists it there or the ledger found
    /// it there; in every region all kinds count. A group with no kind left under the filter is left out.
    /// <paramref name="ownPictures"/> are the Commander's own pictures, which decide <c>MissingImage</c>; null for none.
    /// </summary>
    public static CodexNodeDiscoveries For(
        CodexLedger ledger,
        CodexNode node,
        int? regionId,
        CodexKindStatus filter = CodexKindStatus.All,
        IReadOnlySet<CodexPictureKey>? ownPictures = null)
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

        var scope = new Scope(
            ledger, regionId, firstCanonn, firstKind, ownPictures ?? new HashSet<CodexPictureKey>(), RegionsOf(ledger, regionId));

        return Build(scope, node, filter);
    }

    private sealed record Scope(
        CodexLedger Ledger,
        int? RegionId,
        Dictionary<string, CodexLogEntry> FirstCanonn,
        Dictionary<string, CodexFoundKind>? FirstKind,
        IReadOnlySet<CodexPictureKey> Own,
        Dictionary<string, HashSet<int>>? Regions);

    /// <summary>The regions each kind is on record in; null when the scope is one region.</summary>
    private static Dictionary<string, HashSet<int>>? RegionsOf(CodexLedger ledger, int? regionId)
    {
        if (regionId is not null)
        {
            return null;
        }

        var regions = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        foreach (var kind in ledger.FoundKinds)
        {
            Add(regions, kind.Key, kind.RegionId);
        }

        foreach (var entry in ledger.Entries)
        {
            if (entry.RegionId is { } region)
            {
                Add(regions, CodexKeys.Of(entry.EntryId), region);
            }
        }

        return regions;
    }

    private static void Add(Dictionary<string, HashSet<int>> regions, string key, int region)
    {
        if (!regions.TryGetValue(key, out var set))
        {
            regions[key] = set = [];
        }

        set.Add(region);
    }

    private static CodexNodeDiscoveries Build(Scope scope, CodexNode node, CodexKindStatus filter)
    {
        var shown = new List<CodexKindDiscovery>();
        var groups = new List<CodexNodeDiscoveries>();
        var counted = 0;
        var onRecord = 0;
        var missingImage = 0;

        void Own(CodexNode entryNode)
        {
            if (Discover(scope, entryNode.Entry!) is not { } kind)
            {
                return;
            }

            counted++;
            onRecord += kind.IsOnRecord ? 1 : 0;
            missingImage += kind.MissingImage ? 1 : 0;

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

            var group = Build(scope, child, filter);
            counted += group.Counted;
            onRecord += group.OnRecord;
            missingImage += group.MissingImage;

            if (group.Kinds.Count > 0 || group.Groups.Count > 0)
            {
                groups.Add(group);
            }
        }

        return new CodexNodeDiscoveries(node, counted, onRecord, counted - onRecord, shown, groups, missingImage);
    }

    private static bool Passes(CodexKindDiscovery kind, CodexKindStatus filter) =>
        filter switch
        {
            CodexKindStatus.Missing => !kind.IsOnRecord,
            CodexKindStatus.MissingImage => kind.MissingImage,
            CodexKindStatus.OnRecord => kind.IsOnRecord,
            _ => true,
        };

    private static CodexKindDiscovery? Discover(Scope scope, CodexEntry entry)
    {
        var regionId = scope.RegionId;
        var allRegions = entry.Regions.Values.Sum();
        CodexFirstFind? first;

        if (entry.Kind is null)
        {
            first = scope.FirstCanonn.TryGetValue(entry.Key, out var logged)
                ? new CodexFirstFind(logged.At, logged.System, logged.Body, logged.RegionId)
                : null;
        }
        else
        {
            var found = regionId is { } region
                ? scope.Ledger.Found(entry.Key, region)
                : scope.FirstKind!.GetValueOrDefault(entry.Key);
            first = found is null ? null : new CodexFirstFind(found.At, found.System, found.Body, found.RegionId);
        }

        if (regionId is { } at && !entry.Regions.ContainsKey(at) && first is null)
        {
            return null;
        }

        var systems = regionId is { } here ? entry.Regions.GetValueOrDefault(here) : allRegions;

        return new CodexKindDiscovery(entry, systems, allRegions, first, first is not null && LacksPicture(scope, entry.Key));
    }

    private static bool LacksPicture(Scope scope, string key) =>
        scope.RegionId is { } region
            ? !scope.Own.Contains(new CodexPictureKey(key, region))
            : scope.Regions!.TryGetValue(key, out var regions) && regions.Any(id => !scope.Own.Contains(new CodexPictureKey(key, id)));
}
