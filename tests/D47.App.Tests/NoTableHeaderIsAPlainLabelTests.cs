using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every labelled table header presses to sort: the source builds no plain-label header except the two named below (#2575).</summary>
[Trait("Category", "Gate")]
public sealed partial class NoTableHeaderIsAPlainLabelTests
{
    /// <summary>The control-column fallback in <c>ArrangedTable.HeaderColumn</c>, and Route market, which the Search issue brings in line.</summary>
    private static readonly string[] Exceptions = ["ArrangedTable.cs", "RouteMarketPage.cs"];

    [GeneratedRegex(@"SortColumn<[^;]*?>\.Plain\(")]
    private static partial Regex PlainCall();

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);

        return root.FullName;
    }

    private static string Argument(string text, int from)
    {
        var depth = 1;
        var at = from;

        while (at < text.Length && depth > 0)
        {
            depth += text[at] switch { '(' => 1, ')' => -1, _ => 0 };
            at++;
        }

        return text[from..(at - 1)].Trim();
    }

    [Fact]
    public void NoHeaderIsBuiltPlainUnlessItHasNoLabel()
    {
        var found = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src", "D47.App"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || Exceptions.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            foreach (var call in PlainCall().Matches(text).Cast<Match>())
            {
                if (Argument(text, call.Index + call.Length) != "string.Empty")
                {
                    found.Add($"{Path.GetFileName(file)}: {call.Value}{Argument(text, call.Index + call.Length)})");
                }
            }
        }

        Assert.Empty(found);
    }
}
