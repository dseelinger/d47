using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Every hidden entry decodes and has a public card, and no hidden sentence appears in any text file in the
/// repository. Failures name the story, the field and the file; they never print a decoded field.
/// </summary>
public sealed class NoSealedTextAppearsInTheTreeTests
{
    private static readonly string[] TextFiles =
        [".cs", ".md", ".json", ".txt", ".py", ".ps1", ".axaml", ".html", ".tsv", ".yml", ".yaml", ".iss", ".js", ".css", ".xml", ".props", ".csproj"];

    private static readonly string[] Skipped = ["bin", "obj", ".git", "node_modules", "dev-install", "TestResults", ".vs"];

    [Fact]
    public void EveryHiddenEntryDecodesAndHasACard()
    {
        var catalog = StoryCatalog.Default;

        Assert.Equal(catalog.Cards.Count, catalog.Secrets.Count);

        foreach (var secret in catalog.Secrets)
        {
            Assert.True(catalog.Find(secret.Id) is not null, $"The hidden entry {secret.Id} has no public card.");
        }

        Assert.All(catalog.Cards, card => Assert.True(catalog.Secret(card.Id) is not null, $"The card {card.Id} has no hidden entry."));
    }

    [Fact]
    public void NoHiddenSentenceIsWrittenAnywhereInTheTree()
    {
        var sentences = StoryCatalog.Default.Secrets
            .SelectMany(secret => secret.Texts().SelectMany(field => Sentences(field.Text).Select(sentence => (secret.Id, field.Field, sentence))))
            .ToList();

        var root = RepositoryRoot();
        var leaks = new List<string>();

        foreach (var file in Files(root))
        {
            var text = File.ReadAllText(file);

            foreach (var (id, field, sentence) in sentences)
            {
                if (text.Contains(sentence, StringComparison.OrdinalIgnoreCase))
                {
                    leaks.Add($"{Path.GetRelativePath(root, file)} holds text from {id} ({field})");
                }
            }
        }

        Assert.True(leaks.Count == 0, string.Join(Environment.NewLine, leaks.Distinct()));
    }

    /// <summary>Each sentence long enough to be distinctive, so a quote split across lines is still found.</summary>
    private static IEnumerable<string> Sentences(string text) =>
        text.Split(['.', '?', '!', ';', ':', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => sentence.Length >= 24);

    private static IEnumerable<string> Files(string root)
    {
        var pending = new Stack<string>([root]);

        while (pending.TryPop(out var directory))
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!Skipped.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (TextFiles.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException($"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
