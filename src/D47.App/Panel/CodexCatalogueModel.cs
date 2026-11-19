using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The status and region the catalogue pages share for the session, and the segment chosen on each node's page.</summary>
public sealed class CodexCatalogueChoice
{
    private readonly Dictionary<string, int> _segments = new(StringComparer.Ordinal);
    private CodexKindStatus _status = CodexKindStatus.All;
    private bool _here;
    private int? _other;

    public CodexKindStatus Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    /// <summary>Counts in the region the Commander is in rather than in every region.</summary>
    public bool Here
    {
        get => _here;
        set
        {
            Set(ref _here, value);

            if (value)
            {
                Set(ref _other, null);
            }
        }
    }

    /// <summary>A region chosen from the picker that is not the Commander's; null for none.</summary>
    public int? Other
    {
        get => _other;
        set
        {
            Set(ref _other, value);

            if (value is not null)
            {
                Set(ref _here, false);
            }
        }
    }

    public bool IsDefault => _status == CodexKindStatus.All && !_here && _other is null;

    /// <summary>Changes whenever a value does, so a page can tell another page changed it.</summary>
    public long Version { get; private set; }

    /// <summary>The segment chosen on a node's page; 0 until one is chosen.</summary>
    public int Segment(string nodeKey) => _segments.GetValueOrDefault(nodeKey);

    public void ChooseSegment(string nodeKey, int index)
    {
        _segments[nodeKey] = index;
        Version++;
    }

    public void Clear()
    {
        _status = CodexKindStatus.All;
        _here = false;
        _other = null;
        Version++;
    }

    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            Version++;
        }
    }
}

/// <summary>A group row: a family or a group, with its kinds' counts in the scope.</summary>
public sealed record CodexGroupRow(CodexNode Node, string Name, string? Line, int Kinds, int OnRecord, int Missing, string? PlaceholderFile);

/// <summary>A kind row: the kind's name within its group, its systems in the scope and in every region, and its first find.</summary>
public sealed record CodexKindRow(CodexEntry Entry, string Name, int Systems, int SystemsAllRegions, CodexFirstFind? FirstFind, string? PlaceholderFile)
{
    public bool OnRecord => FirstFind is not null;
}

/// <summary>One segment of a node's page: a child group whose kinds the page lists when it is chosen.</summary>
public sealed record CodexCatalogueSegment(CodexNode Node, string Label, int Kinds, int OnRecord);

/// <summary>
/// What a catalogue page draws for a node, a scope and a status. <c>Shown</c> of <c>Counted</c> kinds pass the status;
/// with <c>Segments</c>, <c>Kinds</c> are the chosen segment's.
/// </summary>
public sealed record CodexCatalogueView(
    CodexNode Node,
    int? RegionId,
    int Counted,
    int OnRecord,
    int Missing,
    int Shown,
    IReadOnlyList<CodexGroupRow> Groups,
    IReadOnlyList<CodexKindRow> Kinds,
    IReadOnlyList<CodexCatalogueSegment> Segments,
    int Segment);

/// <summary>The rows behind Commander › Ledgers › Codex › Catalogue, built from the codex tree and the ledger.</summary>
public static class CodexCatalogueModel
{
    public const string HeadsetRootSummary = "Every kind of object in the Codex.";

    public const string EveryRegion = "Every region";

    public const string EveryRegionStatus = "A kind counts once, wherever it is known";

    public const string HereStatus = "The region you are in";

    public const string NotFoundHere = "Not found in this region";

    public const string NotFound = "Not found";

    public const string NoMatchText = "No kind here matches these filters.";

    public const string YourPicture = "Your picture";

    public const string StockPicture = "Stock picture";

    public const string GroupPicture = "Group picture";

    public const string FamilyPlaceholder = "Family placeholder";

    public static readonly IReadOnlyList<string> StatusLabels = ["All", "Missing", "On record"];

    private static readonly Dictionary<string, string> FamilyLines = new(StringComparer.Ordinal)
    {
        ["Stars"] = "By type, then luminosity band",
        ["Gas giant planets"] = "By class, then rings",
        ["Terrestrial planets"] = "By type, then terraforming and atmosphere",
        ["Organic structures"] = "By genus, then species and variant",
        ["Geology and anomalies"] = "By type, then variant",
        ["Guardian objects"] = "One list of kinds",
        ["Thargoid objects"] = "By type, then variant",
    };

    /// <summary>The page's summary: on the root, how many kinds are on record.</summary>
    public static string Summary(CodexCatalogueView view, bool headset = false) =>
        view.Node.Parent is null
            ? headset
            ? HeadsetRootSummary
            : $"Every kind of object in the Codex. {CodexModel.Number(view.OnRecord)} of {CodexModel.Number(view.Counted)} are on your record."
            : $"The kinds under {view.Node.Name}.";

    /// <summary>A name as a row or crumb shows it, its first letter capitalised: "Not terraformable".</summary>
    public static string Display(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>The line under a family row saying how its kinds are grouped; null for any other node.</summary>
    public static string? FamilyLine(CodexNode node) =>
        node.Parent is { Parent: null } && FamilyLines.TryGetValue(node.Name, out var line) ? line : null;

    /// <summary>The family node a node is under; the node itself for a family, null for the root.</summary>
    public static CodexNode? FamilyOf(CodexNode node)
    {
        var at = node;

        while (at.Parent is { Parent: not null } parent)
        {
            at = parent;
        }

        return at.Parent is null ? null : at;
    }

    /// <summary>A planet's with-atmosphere group, whose kinds are listed with its parent's no-atmosphere kind.</summary>
    public static bool IsWithAtmosphere(CodexNode node) =>
        node.Entry?.Kind is not null && node.Children.Count > 0 && node.Key.EndsWith(".atmosphere", StringComparison.Ordinal);

    /// <summary>
    /// The child groups a node's page shows as a segmented control over one kinds list: two to four groups and no kinds
    /// of its own, each group's children all kinds once a with-atmosphere group is folded into its parent; a planet's
    /// terraformable group comes first. Empty otherwise, and always for the root, which lists its families as rows.
    /// </summary>
    public static IReadOnlyList<CodexNode> SegmentsOf(CodexNode node)
    {
        if (node.Parent is null || node.Children.Any(child => child.Children.Count == 0))
        {
            return [];
        }

        var groups = node.Children.OrderBy(child => child.Key.EndsWith(".terraformable", StringComparison.Ordinal) ? 0 : 1).ToList();

        return groups.Count is >= 2 and <= 4 && groups.All(group => Folded(group).All(child => child.Children.Count == 0))
            ? groups
            : [];
    }

    /// <summary>The page for <paramref name="node"/> in <paramref name="regionId"/>, or in every region when it is null.</summary>
    public static CodexCatalogueView View(
        CodexLedger ledger, CodexTree tree, CodexNode node, int? regionId, CodexKindStatus status, int segment = 0)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(node);

        var found = CodexNodeDiscoveries.For(ledger, node, regionId, status);
        var shown = status switch
        {
            CodexKindStatus.Missing => found.Missing,
            CodexKindStatus.OnRecord => found.OnRecord,
            _ => found.Counted,
        };
        var groupNodes = SegmentsOf(node);

        if (groupNodes.Count > 0)
        {
            var each = groupNodes.Select(group => CodexNodeDiscoveries.For(ledger, group, regionId, status)).ToList();
            var chosen = Math.Clamp(segment, 0, each.Count - 1);

            return new CodexCatalogueView(
                node,
                regionId,
                found.Counted,
                found.OnRecord,
                found.Missing,
                shown,
                [],
                KindRows(tree, each[chosen]),
                [.. each.Select(group => new CodexCatalogueSegment(group.Node, Display(group.Node.ShortName), group.Counted, group.OnRecord))],
                chosen);
        }

        var groups = found.Groups
            .Where(group => !IsWithAtmosphere(group.Node))
            .Select(group => new CodexGroupRow(
                group.Node,
                Display(group.Node.ShortName),
                FamilyLine(group.Node),
                group.Counted,
                group.OnRecord,
                group.Missing,
                FamilyOf(group.Node) is { } family ? CodexFamilies.FileOfTableName(family.Name) : null))
            .ToList();

        return new CodexCatalogueView(
            node, regionId, found.Counted, found.OnRecord, found.Missing, shown, groups, KindRows(tree, found), [], -1);
    }

    /// <summary>The Kinds table's columns; Systems, most first, is the default order.</summary>
    public static IReadOnlyList<SortColumn<CodexKindRow>> KindHeads(string? region) =>
    [
        SortColumn<CodexKindRow>.Text("Kind", row => row.Name) with { Second = "Picture shown" },
        SortColumn<CodexKindRow>.Figure("Systems", row => (long?)row.Systems, SortDirection.Descending)
            with { Second = region is null ? "Known in every region" : region + " / all regions" },
        SortColumn<CodexKindRow>.Figure("First found", row => (long?)row.FirstFind?.At.UtcTicks, SortDirection.Ascending)
            with { Second = "System · body · UTC" },
        SortColumn<CodexKindRow>.Figure<int>("Status", row => row.OnRecord ? 1 : 0, SortDirection.Descending)
            with { Second = region is null ? "In any region" : "In this region" },
    ];

    /// <summary>The key of Systems in the Kinds table's fields.</summary>
    public const string SystemsKey = "Systems";

    /// <summary>The group table's columns: the heads' second lines name the region the counts are for.</summary>
    public static IReadOnlyList<SortColumn<CodexGroupRow>> GroupHeads(string parent, string? region) =>
    [
        SortColumn<CodexGroupRow>.Text("Name", row => row.Name) with { Second = parent },
        SortColumn<CodexGroupRow>.Figure("Kinds", row => (long?)row.Kinds, SortDirection.Descending)
            with { Second = region is null ? "Known in every region" : "Known in " + region },
        SortColumn<CodexGroupRow>.Figure("On record", row => (long?)row.OnRecord, SortDirection.Descending)
            with { Second = region is null ? "Found in any region" : "Found here" },
        SortColumn<CodexGroupRow>.Figure("Missing", row => (long?)row.Missing, SortDirection.Descending)
            with { Second = region is null ? "Found in no region" : "Not found here" },
    ];

    /// <summary>"{n} kinds · {n} on record": a segment's status line on the desktop.</summary>
    public static string SegmentLine(CodexCatalogueSegment segment) =>
        $"{CodexModel.Number(segment.Kinds)} {(segment.Kinds == 1 ? "kind" : "kinds")} · {CodexModel.Number(segment.OnRecord)} on record";

    /// <summary>The line under a kind's name saying which picture its square shows.</summary>
    public static string PictureLine(CodexPictureChoiceLine line) => line switch
    {
        CodexPictureChoiceLine.Yours => YourPicture,
        CodexPictureChoiceLine.Stock => StockPicture,
        CodexPictureChoiceLine.Group => GroupPicture,
        _ => FamilyPlaceholder,
    };

    /// <summary>A node's kinds, a with-atmosphere group's folded in, most systems first: the order until the Commander sorts.</summary>
    private static IReadOnlyList<CodexKindRow> KindRows(CodexTree tree, CodexNodeDiscoveries found) =>
    [
        .. found.Kinds
            .Concat(found.Groups.Where(group => IsWithAtmosphere(group.Node)).SelectMany(group => group.Kinds))
            .OrderByDescending(kind => kind.Systems)
            .Select(kind => new CodexKindRow(
                kind.Entry,
                Display(tree.Node(kind.Entry.Key)?.ShortName ?? kind.Entry.Name),
                kind.Systems,
                kind.SystemsAllRegions,
                kind.FirstFind,
                CodexFamilies.FileOfTableName(kind.Entry.SubCategory))),
    ];

    private static IEnumerable<CodexNode> Folded(CodexNode group) =>
        group.Children.SelectMany(child => IsWithAtmosphere(child) ? child.Children : [child]);
}

/// <summary>Which picture a kind's square shows.</summary>
public enum CodexPictureChoiceLine
{
    Placeholder,
    Yours,
    Stock,
    Group,
}
