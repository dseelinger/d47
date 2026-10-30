using System.Text.RegularExpressions;
using Xunit;

namespace D47.Core.Tests;

[Trait("Category", "Gate")]
public static class DocumentationLinkGateTests
{
    public static TheoryData<string> Pages
    {
        get
        {
            var docs = Path.Combine(RepositoryRoot(), "docs");
            var data = new TheoryData<string>();
            foreach (var path in Directory.EnumerateFiles(docs, "*.md")
                         .Concat(Directory.EnumerateFiles(Path.Combine(docs, "capabilities"), "*.md")))
            {
                data.Add(Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/'));
            }

            return data;
        }
    }

    [Trait("Category", "Gate")]
    [Theory]
    [MemberData(nameof(Pages))]
    public static void EveryLinkOnAPageNamesAFileTheTreeHas(string page)
    {
        var root = RepositoryRoot();
        var text = File.ReadAllText(Path.Combine(root, page));
        var folder = page[..page.LastIndexOf('/')];

        var dead = DocumentationLinks.Links(text)
            .Select(link => (link, problem: DocumentationLinks.Problem(link, folder, path => PathExists(root, path))))
            .Where(x => x.problem is not null)
            .Select(x => $"{x.link}: {x.problem}")
            .ToList();

        Assert.True(dead.Count == 0, $"{page} links to files the tree does not have:\n{string.Join('\n', dead)}");
    }

    [Fact]
    public static void TheSiteDoesNotPublishTheSpikeNotes()
    {
        var config = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "_config.yml"));
        Assert.Matches(new Regex(@"^exclude:\s*\r?\n\s+- spikes\s*$", RegexOptions.Multiline), config);
    }

    private static bool PathExists(string root, string path)
    {
        var full = Path.Combine(root, path);
        return File.Exists(full) || Directory.Exists(full);
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

/// <summary>Reads the links out of a docs page and says whether each one resolves.</summary>
internal static partial class DocumentationLinks
{
    internal const string BlobPrefix = "https://github.com/dseelinger/d47/";
    internal const string SitePrefix = "https://dseelinger.github.io/d47/";

    /// <summary>Link targets in a page, leaving out fenced code blocks and HTML comments.</summary>
    internal static List<string> Links(string markdown)
    {
        var text = CommentPattern().Replace(FencePattern().Replace(markdown, ""), "");
        var found = new List<string>();
        foreach (Match m in MarkdownLinkPattern().Matches(text))
        {
            found.Add(m.Groups["t"].Value);
        }

        foreach (Match m in HrefPattern().Matches(text))
        {
            found.Add(m.Groups["t"].Value);
        }

        foreach (Match m in BareUrlPattern().Matches(text))
        {
            found.Add(m.Value.TrimEnd('.', ',', ';', ':'));
        }

        return found.Distinct().ToList();
    }

    /// <summary>Why a link does not resolve, or null. <paramref name="folder"/> is the page's folder from the repository root.</summary>
    internal static string? Problem(string link, string folder, Func<string, bool> exists)
    {
        var target = link;
        var hash = target.IndexOfAny(['#', '?']);
        if (hash >= 0)
        {
            target = target[..hash];
        }

        if (target.Length == 0)
        {
            return null;
        }

        if (target.StartsWith(BlobPrefix, StringComparison.Ordinal))
        {
            var rest = target[BlobPrefix.Length..];
            foreach (var kind in new[] { "blob/main/", "tree/main/" })
            {
                if (rest.StartsWith(kind, StringComparison.Ordinal))
                {
                    var path = rest[kind.Length..].TrimEnd('/');
                    return path.Length == 0 || exists(path) ? null : $"{path} is not in the tree";
                }
            }

            return null;
        }

        if (target.StartsWith(SitePrefix, StringComparison.Ordinal))
        {
            return SitePage(target[SitePrefix.Length..], exists);
        }

        if (target.Contains("://", StringComparison.Ordinal) || target.StartsWith("mailto:", StringComparison.Ordinal))
        {
            return null;
        }

        var resolved = Normalise(target.StartsWith('/') ? "docs" + target : folder + "/" + target);
        if (resolved is null)
        {
            return "leaves the repository";
        }

        if (resolved.StartsWith("docs/spikes", StringComparison.Ordinal))
        {
            return "spikes are not published";
        }

        if (resolved.EndsWith(".html", StringComparison.Ordinal))
        {
            var md = resolved[..^".html".Length] + ".md";
            return exists(md) || exists(resolved) ? null : $"{md} is not in the tree";
        }

        return exists(resolved.TrimEnd('/')) ? null : $"{resolved} is not in the tree";
    }

    private static string? SitePage(string path, Func<string, bool> exists)
    {
        path = path.TrimEnd('/');
        if (path.Length == 0)
        {
            return null;
        }

        if (path.EndsWith(".html", StringComparison.Ordinal))
        {
            var md = "docs/" + path[..^".html".Length] + ".md";
            return exists(md) ? null : $"{md} is not in the tree";
        }

        return exists("docs/" + path) || exists("docs/" + path + ".md") || exists("docs/" + path + "/index.md")
            ? null
            : $"docs/{path} is not in the tree";
    }

    private static string? Normalise(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    [GeneratedRegex(@"^```[^\n]*\n.*?^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencePattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"\]\((?<t>[^)\s]+)")]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"href=""(?<t>[^""]+)""")]
    private static partial Regex HrefPattern();

    [GeneratedRegex(@"https://(?:github\.com/dseelinger/d47|dseelinger\.github\.io/d47)/[^\s)<>""'\]]*")]
    private static partial Regex BareUrlPattern();
}

public static class DocumentationLinkReaderTests
{
    [Fact]
    public static void TheLinkReaderFindsMarkdownHtmlAndBareLinksOutsideCodeAndComments()
    {
        const string text = """
            See [one](one.html) and [two](../spikes/two.md#top) or <a href="three.html">3</a>.
            Bare: https://github.com/dseelinger/d47/blob/main/docs/four.md, and https://example.com/x.

            ```json
            { "description": "[no](nothing.md) https://github.com/dseelinger/d47/blob/main/gone.md" }
            ```

            <!-- [hidden](hidden.md)
            https://github.com/dseelinger/d47/blob/main/hidden2.md -->
            """;

        Assert.Equal(
            [
                "../spikes/two.md#top",
                "https://github.com/dseelinger/d47/blob/main/docs/four.md",
                "one.html",
                "three.html",
            ],
            DocumentationLinks.Links(text).Order().ToList());
    }

    [Fact]
    public static void APlantedDeadLinkIsReported()
    {
        var present = new HashSet<string>
        {
            "docs/exists.md",
            "docs/capabilities/real.md",
            "src/Real.cs",
        };

        bool Has(string path) => present.Contains(path);

        Assert.Null(DocumentationLinks.Problem("exists.html", "docs", Has));
        Assert.Null(DocumentationLinks.Problem("capabilities/real.html#top", "docs", Has));
        Assert.Null(DocumentationLinks.Problem("../real.html", "docs/capabilities", p => p == "docs/real.md"));
        Assert.Null(DocumentationLinks.Problem("#section", "docs", Has));
        Assert.Null(DocumentationLinks.Problem("https://example.com/x", "docs", Has));
        Assert.Null(DocumentationLinks.Problem(DocumentationLinks.BlobPrefix + "blob/main/src/Real.cs", "docs", Has));
        Assert.Null(DocumentationLinks.Problem(DocumentationLinks.SitePrefix + "exists.html", "docs", Has));

        Assert.NotNull(DocumentationLinks.Problem("gone.html", "docs", Has));
        Assert.NotNull(DocumentationLinks.Problem("../spikes/x.md", "docs/capabilities", _ => true));
        Assert.NotNull(DocumentationLinks.Problem("spikes/x.md", "docs", _ => true));
        Assert.NotNull(DocumentationLinks.Problem(DocumentationLinks.BlobPrefix + "blob/main/docs/gone.md", "docs", Has));
        Assert.NotNull(DocumentationLinks.Problem(DocumentationLinks.BlobPrefix + "tree/main/docs/gone", "docs", Has));
        Assert.NotNull(DocumentationLinks.Problem(DocumentationLinks.SitePrefix + "gone.html", "docs", Has));
    }
}
