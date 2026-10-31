using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The gate for #505: <c>BloomStack</c> is instantiated only at the caption strip's diamond and
/// name, the PTT dot, and the active tab (top strip and rail edge) — a fifth site is a design
/// question, not a drive-by addition.
/// </summary>
[Trait("Category", "Gate")]
public sealed class BloomGlowsOnlyTheFourNamedElementsTests
{
    private static readonly (string File, int Instantiations)[] Expected =
    [
        ("Windowing/CaptionStrip.cs", 2),
        ("Panel/PanelTabs.axaml", 2),
        ("Panel/PanelView.axaml", 1),
    ];

    private static readonly Regex Instantiation = new(@"new BloomStack\b|<theming:BloomStack\b", RegexOptions.Compiled);

    [Fact]
    public void BloomStackAppearsOnlyAtItsFourGlowingElements()
    {
        var root = System.IO.Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App");

        var found = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".axaml", StringComparison.Ordinal))
            .Where(path => System.IO.Path.GetFileName(path) != "BloomStack.cs")
            .Select(path => (
                File: System.IO.Path.GetRelativePath(root, path).Replace('\\', '/'),
                Instantiations: Instantiation.Matches(File.ReadAllText(path)).Count))
            .Where(sighting => sighting.Instantiations > 0)
            .OrderBy(sighting => sighting.File, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Expected.OrderBy(e => e.File, StringComparer.Ordinal).ToArray(), found);
    }
}
