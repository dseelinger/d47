using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>One scale, named by role, and nowhere left to write a number.</summary>
public partial class TypeScaleTests
{
    [GeneratedRegex(@"FontSize\s*=\s*""?\d")]
    private static partial Regex Literal();

    /// <summary>The gate.</summary>
    [Fact]
    public void NoSurfaceWritesAFontSizeAsANumber()
    {
        var offenders = new List<string>();

        foreach (var file in Sources())
        {
            // The caption quad is deliberately outside the scale: it is read at 1.6 metres on a 1600x340
            // texture, where 52 is the middle setting and 14 would be invisible.
            if (file.EndsWith("CaptionViewModel.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (Literal().IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Font sizes must name a role from TypeScale rather than a number: "
            + string.Join(", ", offenders));
    }

    [AvaloniaFact]
    public void TheMarkupScaleAgreesWithTheCodeScale()
    {
        var expected = new Dictionary<string, double>
        {
            ["D47.Type.Title"] = TypeScale.Title,
            ["D47.Type.Heading"] = TypeScale.Heading,
            ["D47.Type.Subheading"] = TypeScale.Subheading,
            ["D47.Type.Body"] = TypeScale.Body,
            ["D47.Type.Secondary"] = TypeScale.Secondary,
            ["D47.Type.Tip"] = TypeScale.Tip,
            ["D47.Type.Small"] = TypeScale.Small,
            ["D47.Type.Meta"] = TypeScale.Meta,
            ["D47.Type.Caption"] = TypeScale.Caption,
        };

        foreach (var (key, size) in expected)
        {
            Assert.True(
                Application.Current!.Resources.TryGetResource(key, null, out var found),
                $"{key} is not in the application's resources.");

            Assert.Equal(size, Assert.IsType<double>(found));
        }
    }

    /// <summary>The scale is ordered, so a role's name and its size cannot disagree.</summary>
    [Fact]
    public void TheRolesDescendInTheOrderTheyAreNamed()
    {
        Assert.True(TypeScale.Title > TypeScale.Heading);
        Assert.True(TypeScale.Heading > TypeScale.Subheading);
        Assert.True(TypeScale.Subheading > TypeScale.Body);
        Assert.True(TypeScale.Body > TypeScale.Secondary);
        Assert.True(TypeScale.Secondary > TypeScale.Small);
        Assert.True(TypeScale.Small > TypeScale.Meta);
        Assert.True(TypeScale.Meta > TypeScale.Caption);
    }

    private static IEnumerable<string> Sources()
    {
        var root = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "D47.App"));

        Assert.True(Directory.Exists(root), $"Could not find the app sources at {root}.");

        return Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal)
                           || path.EndsWith(".axaml", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
