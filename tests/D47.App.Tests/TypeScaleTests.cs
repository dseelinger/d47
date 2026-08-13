using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One scale, named by role, and nowhere left to write a number.
/// <para>
/// Reported from a screenshot: a row's label read smaller than the combo box it labelled.
/// Avalonia's controls render at 14 and D47 set its own text by hand at 10 to 13, so a caption
/// came out dwarfed by its own value. Forty-six size literals across ten files with nothing
/// naming them is how they drifted apart, which is why the fix is a scale and a gate rather
/// than forty-six corrections.
/// </para>
/// </summary>
public partial class TypeScaleTests
{
    [GeneratedRegex(@"FontSize\s*=\s*""?\d")]
    private static partial Regex Literal();

    /// <summary>
    /// The gate. A size written as a number is how this happened, so the number is what is
    /// refused — a role has to be named, and naming one is what makes it comparable to the
    /// control beside it.
    /// </summary>
    [Fact]
    public void NoSurfaceWritesAFontSizeAsANumber()
    {
        var offenders = new List<string>();

        foreach (var file in Sources())
        {
            // The caption quad is deliberately outside the scale: it is read at 1.6 metres on a
            // 1600x340 texture, where 52 is the middle setting and 14 would be invisible.
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

    /// <summary>
    /// Body is whatever an unstyled control draws at. This is the assertion the whole scale
    /// rests on: raise it and the labels stop matching their controls again, in the direction
    /// that started this.
    /// </summary>
    [AvaloniaFact]
    public void BodyIsWhatAControlAlreadyRendersAt()
    {
        // The theme's own number for control content, rather than a control instance: a
        // ComboBox reports the framework default on itself and takes this through its template,
        // so the instance is the wrong thing to ask.
        Assert.True(
            Application.Current!.TryGetResource("ControlContentThemeFontSize", null, out var control),
            "The Fluent theme did not supply ControlContentThemeFontSize.");

        Assert.Equal(TypeScale.Body, Assert.IsType<double>(control));
    }

    /// <summary>
    /// The markup copy of the scale agrees with the code one. Two declarations of one scale is
    /// one more than ideal, and this is what stops them drifting the way the literals did.
    /// </summary>
    [AvaloniaFact]
    public void TheMarkupScaleAgreesWithTheCodeScale()
    {
        var expected = new Dictionary<string, double>
        {
            ["D47.Type.Heading"] = TypeScale.Heading,
            ["D47.Type.Subheading"] = TypeScale.Subheading,
            ["D47.Type.Body"] = TypeScale.Body,
            ["D47.Type.Secondary"] = TypeScale.Secondary,
            ["D47.Type.Small"] = TypeScale.Small,
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
        Assert.True(TypeScale.Heading > TypeScale.Subheading);
        Assert.True(TypeScale.Subheading > TypeScale.Body);
        Assert.True(TypeScale.Body > TypeScale.Secondary);
        Assert.True(TypeScale.Secondary > TypeScale.Small);
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
