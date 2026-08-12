using D47.Core.Capabilities;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The gate that makes "write the docs later" impossible rather than merely discouraged
/// (list.md Phase 1, "Every capability has a documentation page"). It lives as a test so CI
/// needs no separate step that could drift from the capability list.
/// </summary>
public class DocumentationGateTests
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

    private static CapabilityRegistry Registry()
    {
        // A throwaway install: the gate cares about identity and schemas, neither of which
        // depends on where the app happens to be installed.
        var paths = new AppPaths(Path.Combine(Path.GetTempPath(), "d47-docs-gate"));
        return CapabilityRegistry.Build(BuiltinCapabilities.All(paths, new FakeVerbosityControl(), "0.0.0"));
    }

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
