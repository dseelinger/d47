using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>Three families, named by the job the text is doing, and one file that says so.</summary>
public partial class FontsTests
{
    [GeneratedRegex(@"avares://d47/Assets/Fonts/")]
    private static partial Regex RawUri();

    /// <summary>The gate: Fonts.cs and Fonts.axaml are the only places a font is named by URI.</summary>
    [Fact]
    public void NoOtherSurfaceNamesAFontByUri()
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "D47.App"));

        Assert.True(Directory.Exists(root), $"Could not find the app sources at {root}.");

        var offenders = new List<string>();

        foreach (var file in Directory
                     .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                                    || path.EndsWith(".axaml", StringComparison.Ordinal))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            if (file.EndsWith("Fonts.cs", StringComparison.Ordinal) || file.EndsWith("Fonts.axaml", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (RawUri().IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A font must be named through Fonts.cs or Fonts.axaml, not a URI of its own: "
            + string.Join(", ", offenders));
    }

    [AvaloniaFact]
    public void TheMarkupFamiliesAgreeWithTheCodeFamilies()
    {
        var expected = new Dictionary<string, string>
        {
            ["D47.Font.Chrome"] = Fonts.ChromeFamily,
            ["D47.Font.Prose"] = Fonts.ProseFamily,
            ["D47.Font.Mono"] = Fonts.MonoFamily,
        };

        foreach (var (key, uri) in expected)
        {
            Assert.True(
                Application.Current!.Resources.TryGetResource(key, null, out var found),
                $"{key} is not in the application's resources.");

            Assert.Equal(new FontFamily(uri), Assert.IsType<FontFamily>(found));
        }
    }
}
