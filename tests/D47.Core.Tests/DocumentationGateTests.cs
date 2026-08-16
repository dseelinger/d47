using System.Text.RegularExpressions;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The gate that makes "write the docs later" impossible rather than merely discouraged
/// (list.md Phase 1, "Every capability has a documentation page"). It lives as a test so CI
/// needs no separate step that could drift from the capability list.
/// </summary>
public partial class DocumentationGateTests
{
    private const string CapabilityDocsFolder = "docs/capabilities";

    public static TheoryData<string> CapabilityIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in Registry().All.Select(c => c.Descriptor.Id))
            {
                data.Add(id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CapabilityIds))]
    public void EveryRegisteredCapabilityHasADocumentationPage(string id)
    {
        var page = Path.Combine(RepositoryRoot(), CapabilityDocsFolder, $"{id}.md");

        Assert.True(
            File.Exists(page),
            $"Capability '{id}' is registered but has no documentation page. Create {CapabilityDocsFolder}/{id}.md.");
    }

    [Theory]
    [MemberData(nameof(CapabilityIds))]
    public void EveryPageQuotesRealCodeOrOutput(string id)
    {
        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), CapabilityDocsFolder, $"{id}.md"));

        // A page that describes a capability without quoting one real artifact from it is the
        // kind of documentation that goes stale without anybody noticing.
        Assert.True(
            page.Contains("```", StringComparison.Ordinal),
            $"The documentation page for '{id}' quotes no code block or real output.");
    }

    [Theory]
    [MemberData(nameof(CapabilityIds))]
    public void EveryPageQuotesTheCurrentToolSchema(string id)
    {
        var capability = Registry().Find(id);
        Assert.NotNull(capability);

        var page = File.ReadAllText(Path.Combine(RepositoryRoot(), CapabilityDocsFolder, $"{id}.md"));

        foreach (var (tool, schema) in capability.ToolSchemas)
        {
            // The name as well as the schema, and the name first. Every no-argument tool
            // serialises to the same schema text, so a capability registering five of them was
            // satisfied entirely by one code block quoted once — the gate reported a fully
            // documented capability with four tools missing from its page. Checking the name is
            // what makes the assertion per-tool rather than per-distinct-schema.
            Assert.True(
                page.Contains($"`{tool}`", StringComparison.Ordinal),
                $"""
                 The documentation page for '{id}' does not document the tool '{tool}'.
                 Add a section for it to {CapabilityDocsFolder}/{id}.md, naming it as `{tool}`.
                 """);

            // Quoting the canonical schema means the page cannot drift from the tool. When this
            // fails, the fix is to paste the schema below into the page.
            Assert.True(
                page.Contains(schema, StringComparison.Ordinal),
                $"""
                 The documentation page for '{id}' does not quote the current schema for '{tool}'.
                 Paste this into {CapabilityDocsFolder}/{id}.md:

                 {schema}
                 """);
        }
    }

    [Fact]
    public void GeneralHelpExistsAlongsideTheCapabilityPages()
    {
        var root = RepositoryRoot();

        Assert.True(File.Exists(Path.Combine(root, "docs/index.md")), "docs/index.md is missing.");
        Assert.True(File.Exists(Path.Combine(root, "docs/install.md")), "docs/install.md is missing.");
    }

    /// <summary>
    /// Where a capability's page sits in the nav, derived from where the capability sits in the
    /// registry. The hundred is a floor rather than a meaning: it leaves room below for the
    /// general help pages, so the nav opens with "how do I install this" rather than with the
    /// first capability that happens to be registered.
    /// </summary>
    private static int NavOrderFor(int registryIndex) => 100 + registryIndex;

    /// <summary>
    /// The nav is grouped by <see cref="CapabilityDescriptor.Group"/>, and this is what stops
    /// that being a second hand-maintained list (list.md Phase 19, "The published docs need a
    /// left nav").
    /// <para>
    /// The site is Jekyll and cannot read the registry, so the grouping lives in each page's
    /// front matter and this asserts the two still agree. Same arrangement as the schema gate
    /// above and for the same reason: when it fails it says exactly what to paste.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryCapabilityPageIsFiledUnderTheGroupItsCapabilityDeclares()
    {
        var wrong = new List<string>();

        foreach (var (capability, index) in Registry().All.Select((c, i) => (c, i)))
        {
            var descriptor = capability.Descriptor;
            var path = Path.Combine(RepositoryRoot(), CapabilityDocsFolder, $"{descriptor.Id}.md");
            var front = FrontMatter(File.ReadAllText(path));

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = descriptor.Name,
                ["group"] = descriptor.Group,
                ["nav_order"] = NavOrderFor(index).ToString(),
            };

            foreach (var (key, want) in expected)
            {
                if (!string.Equals(front.GetValueOrDefault(key), want, StringComparison.Ordinal))
                {
                    wrong.Add(
                        $"{descriptor.Id}.md has {key}: {front.GetValueOrDefault(key) ?? "(missing)"}, "
                        + $"and its capability says {key}: {want}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    /// <summary>
    /// Every published page is reachable from the nav.
    /// <para>
    /// The gate above asserts a page <em>exists</em> and asserts nothing about anybody being able
    /// to find it, which is how a page can pass CI and be invisible. It is not hypothetical:
    /// nine capability pages carried no front-matter title at all, so minima left them out of its
    /// header run entirely and the only way to reach them was to already know the URL.
    /// </para>
    /// <para>
    /// Reachability is <c>nav_order</c>, because that is the field <c>_layouts/default.html</c>
    /// filters on — a page without one is not in the nav, whatever else it has.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryPublishedPageIsReachableFromTheNav()
    {
        var missing = PublishedPages()
            .Where(page => !FrontMatter(File.ReadAllText(page.Path)).ContainsKey("nav_order"))
            .Select(page => page.Relative)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Published but unreachable from the nav: {string.Join(", ", missing)}. "
            + "Give each one a title, a group and a nav_order in its front matter.");
    }

    [Fact]
    public void EveryPublishedPageIsNamedAndGroupedAndSitsSomewhereUnique()
    {
        var pages = PublishedPages()
            .Select(page => (page.Relative, Front: FrontMatter(File.ReadAllText(page.Path))))
            .ToArray();

        var unnamed = pages
            .Where(page => !page.Front.ContainsKey("title") || !page.Front.ContainsKey("group"))
            .Select(page => page.Relative)
            .ToArray();

        Assert.True(unnamed.Length == 0, $"Published with no title or no group: {string.Join(", ", unnamed)}");

        // Two pages at the same position is a nav whose order depends on how the file system
        // enumerated them, which is a nav that reorders itself between builds.
        var clashes = pages
            .GroupBy(page => page.Front["nav_order"], StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(" and ", group.Select(page => page.Relative))}")
            .ToArray();

        Assert.True(clashes.Length == 0, $"Two pages share a nav_order — {string.Join("; ", clashes)}");
    }

    /// <summary>
    /// The pages the site actually publishes. <c>_config.yml</c> excludes the spike write-ups and
    /// the implementation plans — those are contributor material that GitHub renders in the repo
    /// — so a gate about the published site must exclude them too or it fails on documents that
    /// were never meant to have a nav entry.
    /// </summary>
    private static IEnumerable<(string Path, string Relative)> PublishedPages()
    {
        var docs = Path.Combine(RepositoryRoot(), "docs");

        return Directory
            .EnumerateFiles(docs, "*.md", SearchOption.AllDirectories)
            .Where(path => !Excluded(Path.GetRelativePath(docs, path)))
            .Select(path => (path, Path.GetRelativePath(docs, path).Replace('\\', '/')));
    }

    private static bool Excluded(string relative) =>
        relative.StartsWith("spikes", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("plans", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("_", StringComparison.Ordinal);

    /// <summary>
    /// A page's front matter as key/value pairs. Deliberately not a YAML parser: this reads three
    /// scalar keys off the top of a file the repository writes, and a dependency for that would
    /// be a dependency in the test project's graph for the sake of thirty lines.
    /// </summary>
    private static Dictionary<string, string> FrontMatter(string page)
    {
        var front = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!page.StartsWith("---", StringComparison.Ordinal))
        {
            return front;
        }

        foreach (var line in page.Split('\n').Skip(1))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                break;
            }

            var colon = trimmed.IndexOf(':');

            if (colon > 0 && !trimmed.StartsWith('#'))
            {
                front[trimmed[..colon].Trim()] = trimmed[(colon + 1)..].Trim();
            }
        }

        return front;
    }

    [Fact]
    public void EveryCapabilityPageBelongsToARegisteredCapability()
    {
        var registered = Registry().All.Select(c => c.Descriptor.Id).ToHashSet(StringComparer.Ordinal);

        var orphans = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), CapabilityDocsFolder), "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !registered.Contains(name))
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"Documentation pages with no registered capability: {string.Join(", ", orphans)}");
    }

    private static CapabilityRegistry Registry() => Surface().Registry;

    /// <summary>
    /// Every gesture a page offers as the out-of-the-box default is one a Commander will try,
    /// and one that no longer exists is worse than no documentation at all — they will conclude
    /// the feature is broken rather than that the page is.
    /// <para>
    /// Checked as a set rather than page-by-page: it needs no map from a sentence to a settings
    /// key, which would be a second thing to keep in step. A gesture that is nobody's default
    /// is the bug, whichever page it is on. This is how <c>F10</c> survived on the settings
    /// hotkey long after the default became <c>Ctrl+,</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryDocumentedDefaultGestureIsSomethingTheAppActuallyShips()
    {
        var shipped = ShippedGestures();

        var documented = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), "docs"), "*.md", SearchOption.AllDirectories)
            .SelectMany(file => DefaultGesturesIn(File.ReadAllText(file))
                .Select(gesture => (File: Path.GetFileName(file), Gesture: gesture)))
            .ToList();

        Assert.NotEmpty(documented);

        var wrong = documented
            .Where(mention => !shipped.Contains(mention.Gesture))
            .Select(mention => $"{mention.File} offers '{mention.Gesture}'")
            .ToList();

        Assert.True(
            wrong.Count == 0,
            $"Documented as the default, but nothing ships it: {string.Join("; ", wrong)}. "
            + $"The defaults are: {string.Join(", ", shipped.Order())}.");
    }

    /// <summary>
    /// The gestures a fresh install actually starts with, in the spelling a Commander sees
    /// rather than the one the settings file stores.
    /// </summary>
    private static HashSet<string> ShippedGestures()
    {
        var settings = new D47Settings();

        return
        [
            .. new[]
            {
                settings.Hotkeys.OpenSettings,
                settings.Hotkeys.FocusAsk,
                settings.Hotkeys.Reanchor,
                settings.Speech.ShutUpHotkey,
                settings.Listening.PushToTalkKey,
            }
            .Where(gesture => !string.IsNullOrWhiteSpace(gesture))
            .Select(Readable!),
        ];
    }

    /// <summary>
    /// "Ctrl+OemComma" is Avalonia's spelling and "Ctrl+," is the Commander's. The pages are
    /// written in the second, so the comparison has to be too.
    /// </summary>
    private static string Readable(string gesture) => gesture
        .Replace("OemComma", ",", StringComparison.Ordinal)
        .Replace("OemPeriod", ".", StringComparison.Ordinal)
        .Replace("OemQuestion", "/", StringComparison.Ordinal)
        .Replace("OemPlus", "=", StringComparison.Ordinal)
        .Replace("OemMinus", "-", StringComparison.Ordinal);

    /// <summary>
    /// Gestures a page names as the default, in either voice the pages use: `Ctrl+L` out of the
    /// box, or **Ctrl+Alt+R** out of the box. Only that phrasing, because a page may mention a
    /// key for other reasons — the paste that navigation.md sends to Elite is not a default of
    /// d47's and is not one to check.
    /// </summary>
    private static IEnumerable<string> DefaultGesturesIn(string page) =>
        DefaultGesturePattern()
            .Matches(page)
            .Select(match => match.Groups["gesture"].Value);

    [GeneratedRegex(@"[`*]{1,2}(?<gesture>[A-Za-z0-9+,./=\-]+)[`*]{1,2},? (?:out of the box|by default)")]
    private static partial Regex DefaultGesturePattern();

    /// <summary>
    /// A throwaway install: the gate cares about identity, schemas and settings rows, none of
    /// which depends on where the app happens to be installed.
    /// </summary>
    private static TestSurface Surface() => TestSurface.For(new TempInstall());

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
