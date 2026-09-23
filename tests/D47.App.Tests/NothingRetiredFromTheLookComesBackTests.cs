using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The gate for #407: the app names no ToggleSwitch, ComboBox or CornerRadius, writes no colour outside
/// Palette.cs, and nothing in src/ reads a retired ThemeManager key. Comments are not uses.
/// </summary>
public sealed class NothingRetiredFromTheLookComesBackTests
{
    private static readonly string[] RetiredControls = ["ToggleSwitch", "ComboBox", "CornerRadius"];

    private static readonly string[] RetiredKeys =
    [
        "Background", "Surface", "SurfaceAlt", "Border", "Text", "TextMuted", "TextFaint", "Accent",
        "AccentMuted", "Danger", "Warn", "Good", "Info", "Rule", "FillLow", "FillHigh", "FillHigher",
        "AccentBorder", "AccentInk", "CardFill", "CardFillSelected", "RowFill", "TagBorder", "PaneFill",
        "PaneBorder", "TagInk",
    ];

    private static readonly HashSet<string> RetiredConstants = [.. RetiredKeys.Select(key => key + "Key")];

    private static readonly HashSet<string> RetiredResources = [.. RetiredKeys.Select(key => "D47." + key)];

    private static readonly HashSet<string> ColourFactories = ["FromArgb", "FromRgb", "FromUInt32", "Parse", "TryParse"];

    private static readonly Regex HexColour =
        new(@"(?<![\w&])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\b", RegexOptions.Compiled);

    private static readonly Regex MarkupComment = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex MarkupControl =
        new($@"\b(?:{string.Join('|', RetiredControls)})\b", RegexOptions.Compiled);

    private static readonly Regex MarkupKey =
        new($@"\bD47\.(?:{string.Join('|', RetiredKeys)})(?![\w.])", RegexOptions.Compiled);

    private static readonly Regex MarkupHexColour =
        new(@"=\s*""\s*#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\s*""", RegexOptions.Compiled);

    [Fact]
    public void NothingUnderSrcNamesARetiredControlColourOrKey()
    {
        var root = RepositoryRoot();
        var sightings = new List<string>();

        foreach (var path in SourceFiles(Path.Combine(root, "src")))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            sightings.AddRange(Sightings(relative, File.ReadAllText(path)));
        }

        Assert.True(
            sightings.Count == 0,
            "A retired control, a colour outside Palette.cs or a retired theme key is back (#407):\n"
            + string.Join(Environment.NewLine, sightings));
    }

    [Theory]
    [InlineData("src/D47.App/Settings/New.cs", "var toggle = new ToggleSwitch();")]
    [InlineData("src/D47.App/Settings/New.axaml", "<ToggleSwitch IsChecked=\"True\" />")]
    [InlineData("src/D47.App/Settings/New.cs", "var pick = new ComboBox();")]
    [InlineData("src/D47.App/Settings/New.axaml", "<Border CornerRadius=\"4\" />")]
    [InlineData("src/D47.App/Settings/New.cs", "var chip = new Border { CornerRadius = new(4) };")]
    [InlineData("src/D47.App/Settings/New.cs", "var ink = Color.Parse(\"#FF8C0D\");")]
    [InlineData("src/D47.App/Settings/New.cs", "var ink = Color.FromRgb(0xFF, 0x8C, 0x0D);")]
    [InlineData("src/D47.App/Settings/New.axaml", "<TextBlock Foreground=\"#FF8C0D\" />")]
    [InlineData("src/D47.App/Settings/New.cs", "var key = ThemeManager.TextKey;")]
    [InlineData("src/D47.App/Settings/New.cs", "var key = Theming.ThemeManager.TextKey;")]
    [InlineData("src/D47.App/Settings/New.cs", "var key = \"D47.AccentInk\";")]
    [InlineData("src/D47.App/Settings/New.axaml", "<TextBlock Foreground=\"{DynamicResource D47.TextMuted}\" />")]
    [InlineData("src/D47.Vr/New.cs", "const string Key = \"D47.Accent\";")]
    public void EachRetiredUseIsCaught(string path, string source)
    {
        Assert.NotEmpty(Sightings(path, Wrap(path, source)));
    }

    [Theory]
    [InlineData("src/D47.App/Settings/New.cs", "// A ToggleSwitch drew this before; D47.Text too.")]
    [InlineData("src/D47.App/Settings/New.cs", "var note = \"No ComboBox needed (#274).\";")]
    [InlineData("src/D47.App/Settings/New.axaml", "<!-- <ToggleSwitch Foreground=\"#FF8C0D\" /> -->")]
    [InlineData("src/D47.App/Settings/New.axaml", "<TextBlock Foreground=\"{DynamicResource D47.Grey}\" Text=\"&#8250;\" />")]
    [InlineData("src/D47.App/Theming/Palette.cs", "var ink = Color.Parse(\"#FF8C0D\");")]
    [InlineData("src/D47.Core/New.cs", "var pick = new ComboBox();")]
    [InlineData("src/D47.Core/New.cs", "var key = CalloutCapability.DangerKey;")]
    public void WhatIsNotAUseIsLeftAlone(string path, string source)
    {
        Assert.Empty(Sightings(path, Wrap(path, source)));
    }

    private static string Wrap(string path, string source) =>
        path.EndsWith(".cs", StringComparison.Ordinal) ? $"class C {{ void M() {{ {source} }} }}" : source;

    private static IEnumerable<string> SourceFiles(string src) =>
        Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "vendor"));

    /// <summary>Each retired use in one file, as <c>path:line what</c>. <paramref name="path"/> is repository-relative with forward slashes.</summary>
    private static List<string> Sightings(string path, string text)
    {
        var inApp = path.StartsWith("src/D47.App/", StringComparison.Ordinal);
        var colourExempt = path == "src/D47.App/Theming/Palette.cs";

        return path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
            ? MarkupSightings(path, text, inApp, colourExempt)
            : CodeSightings(path, text, inApp, colourExempt);
    }

    private static List<string> CodeSightings(string path, string text, bool inApp, bool colourExempt)
    {
        var sightings = new List<string>();
        var tree = CSharpSyntaxTree.ParseText(text, path: path, cancellationToken: TestContext.Current.CancellationToken);
        var syntaxRoot = tree.GetRoot(TestContext.Current.CancellationToken);

        void Add(SyntaxToken token, string what) =>
            sightings.Add($"{path}:{token.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {what}");

        foreach (var token in syntaxRoot.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                var name = token.ValueText;

                if (inApp && RetiredControls.Contains(name))
                {
                    Add(token, name);
                }
                else if (RetiredConstants.Contains(name) && OnThemeManager(token))
                {
                    Add(token, $"retired key {name}");
                }
                else if (inApp && !colourExempt && ColourFactories.Contains(name) && token.Parent is IdentifierNameSyntax
                         {
                             Parent: MemberAccessExpressionSyntax
                             {
                                 Expression: IdentifierNameSyntax { Identifier.ValueText: "Color" or "Brush" },
                             },
                         })
                {
                    Add(token, $"colour written as {token.Parent.Parent}");
                }
            }
            else if (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken))
            {
                if (RetiredResources.Contains(token.ValueText))
                {
                    Add(token, $"retired key \"{token.ValueText}\"");
                }
                else if (inApp && !colourExempt && HexColour.IsMatch(token.ValueText))
                {
                    Add(token, $"hex colour {HexColour.Match(token.ValueText).Value}");
                }
            }
        }

        return sightings;
    }

    /// <summary>Whether <paramref name="token"/> is read as <c>ThemeManager.X</c> or declared inside ThemeManager.</summary>
    private static bool OnThemeManager(SyntaxToken token) =>
        token.Parent switch
        {
            IdentifierNameSyntax { Parent: MemberAccessExpressionSyntax access } when access.Name == token.Parent =>
                access.Expression is IdentifierNameSyntax { Identifier.ValueText: "ThemeManager" }
                    or MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ThemeManager" },
            VariableDeclaratorSyntax declarator =>
                declarator.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "ThemeManager"),
            _ => false,
        };

    private static List<string> MarkupSightings(string path, string text, bool inApp, bool colourExempt)
    {
        var sightings = new List<string>();

        // Blanked rather than removed, so line numbers still match the file.
        var lines = MarkupComment.Replace(text, comment => Regex.Replace(comment.Value, @"[^\n]", " ")).Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var where = $"{path}:{i + 1}";

            if (inApp && MarkupControl.Match(lines[i]) is { Success: true } control)
            {
                sightings.Add($"{where} {control.Value}");
            }

            if (MarkupKey.Match(lines[i]) is { Success: true } key)
            {
                sightings.Add($"{where} retired key {key.Value}");
            }

            if (inApp && !colourExempt && MarkupHexColour.Match(lines[i]) is { Success: true } hex)
            {
                sightings.Add($"{where} hex colour {hex.Value}");
            }
        }

        return sightings;
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
