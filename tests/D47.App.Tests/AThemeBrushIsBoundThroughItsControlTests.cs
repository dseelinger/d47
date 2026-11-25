using System.Text.RegularExpressions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A theme brush is followed through the control it colours, so the subscription goes with the control. One followed
/// through the application, or through a longer-lived control such as the panel, is held by that, and every page
/// rebuilt keeps the old page alive.
/// </summary>
[Trait("Category", "Gate")]
public partial class AThemeBrushIsBoundThroughItsControlTests
{
    /// <summary>Files allowed to observe the application's resources, and why.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine("Theming", "ThemeInk.cs")] = "the fallback for a target that is not a resource host",
        [Path.Combine("Panel", "HullViewer.cs")] = "subscribes while attached and disposes on detach",
    };

    private static readonly string[] ApplicationObservables =
    [
        "Resources.GetResourceObservable(",
        "Current!.GetResourceObservable(",
        "Current?.GetResourceObservable(",
    ];

    /// <summary>A bind whose observable comes from a named control, within one statement.</summary>
    [GeneratedRegex(@"([\w\.\[\]]+)\.Bind\(\s*[^;]*?([\w]+)\.GetResourceObservable\(", RegexOptions.Singleline)]
    private static partial Regex BindThroughAControl();

    [Fact]
    [Trait("Category", "Gate")]
    public void NoSourceObservesTheApplicationsResourcesOutsideThemeInk()
    {
        var found = new List<string>();

        foreach (var (relative, source) in Sources())
        {
            if (Allowed.ContainsKey(relative))
            {
                continue;
            }

            var lines = source.Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                if (ApplicationObservables.Any(text => lines[index].Contains(text, StringComparison.Ordinal)))
                {
                    found.Add($"{relative}:{index + 1}");
                }
            }
        }

        Assert.True(found.Count == 0, "Bind through ThemeInk.Bind instead: " + string.Join(", ", found));
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void NoControlIsBoundThroughAnotherControlsResources()
    {
        var found = new List<string>();

        foreach (var (relative, source) in Sources())
        {
            foreach (Match match in BindThroughAControl().Matches(source))
            {
                var target = match.Groups[1].Value.Split('.')[^1];

                if (!string.Equals(target, match.Groups[2].Value, StringComparison.Ordinal))
                {
                    found.Add($"{relative}:{source[..match.Index].Count(c => c == '\n') + 1}");
                }
            }
        }

        Assert.True(found.Count == 0, "Bind through ThemeInk.Bind instead: " + string.Join(", ", found));
    }

    private static IEnumerable<(string Relative, string Source)> Sources()
    {
        var folder = Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App");

        foreach (var path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, path);

            if (relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(path));
        }
    }
}
