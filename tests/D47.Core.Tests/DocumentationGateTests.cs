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
