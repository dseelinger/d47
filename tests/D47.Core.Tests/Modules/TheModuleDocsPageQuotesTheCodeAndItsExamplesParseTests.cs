using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

[Trait("Category", "Gate")]
public partial class TheModuleDocsPageQuotesTheCodeAndItsExamplesParseTests
{
    private static readonly HashSet<string> Tools = ["set_view", "set_flag", "refresh", "clear"];

    private static readonly HashSet<string> Pages = ["main", "detail"];

    private static string Page() => File.ReadAllText(Path.Combine(RepositoryFolder.Root(), "docs", "modules.md"));

    private static List<string> Blocks(string language) =>
        [.. FencePattern().Matches(Page()).Where(m => m.Groups["lang"].Value == language).Select(m => m.Groups["body"].Value)];

    [GeneratedRegex(@"^```(?<lang>[A-Za-z]*)[ \t]*\r?\n(?<body>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencePattern();

    private static JsonElement Parsed(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void TheManifestExampleIsReadyWithTheCatalogsOwnReader()
    {
        var example = Assert.Single(Blocks("json"), block => Parsed(block).TryGetProperty("contract", out _));
        var install = new MemoryInstall();
        var folder = Path.Combine(install.Paths.Modules, "ledger");

        install.Files.WriteText(Path.Combine(folder, "module.json"), example);
        install.Files.WriteBytes(Path.Combine(folder, "ledger.exe"), [1]);

        var module = Assert.IsType<ReadyModule>(Assert.Single(new ModuleCatalog(install.Files, install.Paths.Modules).Reload()));

        Assert.Equal(Pages, module.Manifest.Pages.Select(page => page.Id).ToHashSet());
    }

    [Fact]
    public void ThePageExampleParsesWithThePageParser()
    {
        var example = Assert.Single(Blocks("json"), block => Parsed(block).TryGetProperty("parts", out _));
        var parse = ModulePageParser.Parse(example, Tools, Pages);

        Assert.Null(parse.Refusal);
        Assert.Equal(10, parse.Page!.Parts.Count);
    }

    [Fact]
    public void TheSayExampleHasTheKeyAndTextTheModuleHostReads()
    {
        var example = Assert.Single(Blocks("json"), block => Parsed(block).TryGetProperty("method", out _));
        var root = Parsed(example);
        var parameters = root.GetProperty("params");

        Assert.Equal("notifications/d47/say", root.GetProperty("method").GetString());
        Assert.False(string.IsNullOrEmpty(parameters.GetProperty("key").GetString()));
        Assert.InRange(parameters.GetProperty("text").GetString()!.Length, 1, ModuleLine.MaxText);
    }

    [Fact]
    public void TheToolExampleIsAnObjectSchemaWithPhrasesThatSurviveAsStrings()
    {
        var example = Assert.Single(Blocks("json"), block => Parsed(block).TryGetProperty("_meta", out _));
        var root = Parsed(example);

        Assert.Equal("object", root.GetProperty("inputSchema").GetProperty("type").GetString());
        Assert.All(root.GetProperty("_meta").GetProperty("d47/phrases").EnumerateArray(), phrase => Assert.Equal(JsonValueKind.String, phrase.ValueKind));
        Assert.True(("mod_ledger_" + root.GetProperty("name").GetString()).Length <= ModuleTools.MaxName);
    }

    [Fact]
    public void EveryQuotedCodeBlockIsLinesTheSourceHas()
    {
        var sources = Directory
            .EnumerateFiles(Path.Combine(RepositoryFolder.Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Lines(File.ReadAllText(path)))
            .ToList();

        var blocks = Blocks("csharp");
        Assert.NotEmpty(blocks);

        var missing = blocks.Where(block => !sources.Any(source => source.Contains(Lines(block), StringComparison.Ordinal))).ToList();

        Assert.True(missing.Count == 0, "Quoted blocks the source does not hold:\n" + string.Join("\n---\n", missing));
    }

    /// <summary>The non-blank lines, trimmed and joined, so indentation and blank lines do not matter.</summary>
    private static string Lines(string text) =>
        string.Join('\n', text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
}
