using System.Text.RegularExpressions;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The gate that makes "write the docs later" impossible rather than merely discouraged.</summary>
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

        // A page that describes a capability without quoting one real artifact from it is the kind of
        // documentation that goes stale without anybody noticing.
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
            // The name as well as the schema, and the name first.
            Assert.True(
                page.Contains($"`{tool}`", StringComparison.Ordinal),
                $"""
                 The documentation page for '{id}' does not document the tool '{tool}'.
                 Add a section for it to {CapabilityDocsFolder}/{id}.md, naming it as `{tool}`.
                 """);

            // Quoting the canonical schema means the page cannot drift from the tool.
            Assert.True(
                FencedBlocksIn(page).Any(block => string.Equals(block, schema.Trim(), StringComparison.Ordinal)),
                $"""
                 The documentation page for '{id}' does not quote the current schema for '{tool}'
                 as a fenced block of its own. Paste this into {CapabilityDocsFolder}/{id}.md,
                 inside a fence with nothing else in it:

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
    /// registry.
    /// </summary>
    private static int NavOrderFor(int registryIndex) => 100 + registryIndex;

    /// <summary>
    /// The gate numbers pages from a registry with timers and alarms in it, whatever the app's startup
    /// flag says (#90); leaving the capability out removes that one entry and moves no other.
    /// </summary>
    [Fact]
    public void TheGateNumbersThePagesWithTimersAndAlarmsRegistered()
    {
        var id = Capabilities.Builtin.UtilitiesCapability.Id;

        Assert.NotNull(Registry().Find(id));

        using var install = new TempInstall();

        var without = TestSurface.For(install, timersAndAlarms: false).Registry.All.Select(c => c.Descriptor.Id);
        var with = Registry().All.Select(c => c.Descriptor.Id).Where(other => other != id);

        Assert.Equal(with, without);
    }

    /// <summary>
    /// The nav is grouped by <see cref="CapabilityDescriptor.Group"/>, and this is what stops that
 /// being a second hand-maintained list.
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

        // Two pages at the same position is a nav whose order depends on how the file system enumerated them,
        // which is a nav that reorders itself between builds.
        var clashes = pages
            .GroupBy(page => page.Front["nav_order"], StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(" and ", group.Select(page => page.Relative))}")
            .ToArray();

        Assert.True(clashes.Length == 0, $"Two pages share a nav_order — {string.Join("; ", clashes)}");
    }

    /// <summary>The pages the site actually publishes.</summary>
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
        || relative.StartsWith("_", StringComparison.Ordinal);

    /// <summary>A page's front matter as key/value pairs.</summary>
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

    /// <summary>A card marked as a settings jump must land on rows that exist.</summary>
    [Fact]
    public void EverySettingsJumpNamesACapabilityThatHasSettings()
    {
        var withRows = Registry().All
            .Where(capability => capability.Descriptor.Settings.Count > 0)
            .Select(capability => capability.Descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        var dead = D47.Core.Help.HelpLibrary.Pages
            .Select(id => (Page: id, Article: D47.Core.Help.HelpLibrary.For(id)))
            .Where(page => page.Article is not null)
            .SelectMany(page => page.Article!.Links
                .Where(link => link.Settings is { Length: > 0 })
                .Select(link => (page.Page, Section: link.Settings!)))
            .Where(card => !withRows.Contains(card.Section))
            .Select(card => $"{card.Page} → {card.Section}")
            .ToArray();

        Assert.True(
            dead.Length == 0,
            $"Settings jumps naming a capability with no settings rows: {string.Join(", ", dead)}");
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

    /// <summary>
    /// Every settings row's documentation anchor resolves to a heading on that row's own capability
    /// page.
    /// </summary>
    [Fact]
    public void EverySettingsAnchorResolvesToAHeadingOnItsOwnPage()
    {
        var dangling = new List<string>();

        foreach (var capability in Registry().All)
        {
            var page = Path.Combine(RepositoryRoot(), CapabilityDocsFolder, $"{capability.Descriptor.Id}.md");

            if (!File.Exists(page))
            {
                // Its own gate above reports this; a second complaint here would be noise.
                continue;
            }

            var anchors = AnchorsIn(File.ReadAllText(page));

            foreach (var row in capability.Descriptor.Settings)
            {
                if (string.IsNullOrWhiteSpace(row.DocsAnchor) || anchors.Contains(row.DocsAnchor))
                {
                    continue;
                }

                dangling.Add($"{row.Key} -> {capability.Descriptor.Id}.md#{row.DocsAnchor}");
            }
        }

        Assert.True(
            dangling.Count == 0,
            "Settings rows whose documentation anchor names no heading on their own page: "
            + string.Join(", ", dangling));
    }

    /// <summary>
    /// Every anchor a page offers: the explicit <c>{#id}</c> markers, plus GitHub's slug of each
    /// heading.
    /// </summary>
    private static HashSet<string> AnchorsIn(string page)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match explicitId in ExplicitAnchor().Matches(page))
        {
            anchors.Add(explicitId.Groups[1].Value);
        }

        foreach (Match heading in Heading().Matches(page))
        {
            var text = ExplicitAnchor().Replace(heading.Groups[1].Value, string.Empty).Trim();
            var slug = new string(text
                .ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ' || c == '_' ? c : '\0')
                .Where(c => c != '\0')
                .ToArray())
                .Trim()
                .Replace(' ', '-');

            if (slug.Length > 0)
            {
                anchors.Add(slug);
            }
        }

        return anchors;
    }

    [GeneratedRegex(@"\{#([a-z0-9\-_]+)\}")]
    private static partial Regex ExplicitAnchor();

    [GeneratedRegex(@"(?m)^\#{1,6}[ \t]+(.+?)[ \t]*$")]
    private static partial Regex Heading();
    private static CapabilityRegistry Registry() => Surface().Registry;

    /// <summary>
    /// Every gesture a page offers as the out-of-the-box default is one a Commander will try, and one
    /// that no longer exists is worse than no documentation at all — they will conclude the feature is
    /// broken rather than that the page is.
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
    /// The gestures a fresh install actually starts with, in the spelling a Commander sees rather than
    /// the one the settings file stores.
    /// </summary>
    private static HashSet<string> ShippedGestures()
    {
        var settings = new D47Settings();

        // Every gesture on the hotkey record, read off the record rather than listed here.
        var hotkeys = typeof(HotkeySettings)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => (string?)property.GetValue(settings.Hotkeys));

        return
        [
            .. hotkeys
                .Concat([settings.Speech.ShutUpHotkey, settings.Listening.PushToTalkKey])
                .Where(gesture => !string.IsNullOrWhiteSpace(gesture))
                .Select(Readable!),
        ];
    }

    /// <summary>"Ctrl+OemComma" is Avalonia's spelling and "Ctrl+," is the Commander's.</summary>
    private static string Readable(string gesture) => gesture
        .Replace("OemComma", ",", StringComparison.Ordinal)
        .Replace("OemPeriod", ".", StringComparison.Ordinal)
        .Replace("OemQuestion", "/", StringComparison.Ordinal)
        .Replace("OemPlus", "=", StringComparison.Ordinal)
        .Replace("OemMinus", "-", StringComparison.Ordinal);

    /// <summary>
    /// Gestures a page names as the default, in either voice the pages use: `Ctrl+L` out of the box, or
    /// **Ctrl+Alt+R** out of the box.
    /// </summary>
    private static IEnumerable<string> DefaultGesturesIn(string page) =>
        DefaultGesturePattern()
            .Matches(page)
            .Select(match => match.Groups["gesture"].Value);

    [GeneratedRegex(@"[`*]{1,2}(?<gesture>[A-Za-z0-9+,./=\-]+)[`*]{1,2},? (?:out of the box|by default)")]
    private static partial Regex DefaultGesturePattern();

    /// <summary>The contents of every fenced block on a page, trimmed.</summary>
    private static IEnumerable<string> FencedBlocksIn(string page) =>
        FencePattern()
            .Matches(page)
            .Select(match => match.Groups["body"].Value.Trim());

    [GeneratedRegex(@"^```[A-Za-z]*[ \t]*\r?\n(?<body>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencePattern();

    /// <summary>
    /// A throwaway install: the gate cares about identity, schemas and settings rows, none of which
    /// depends on where the app happens to be installed.
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
