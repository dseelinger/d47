using System.Reflection;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every affordance but the window caption's is a word or one of the text glyphs <c>◄ ► ▲ ▼ ↺ ▸ ▾ ▌ ↗</c> (#360).
/// </summary>
public class OnlyTheCaptionDrawsAMarkTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("d47.slnx not found");
    }

    [Fact]
    public void GlyphsHoldsOnlyTheCaptionPaths()
    {
        var paths = typeof(Glyphs).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.GetRawConstantValue() is string data
                            && data.StartsWith('M'))
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(["Close", "Maximize", "Minimize", "Restore"], paths);
    }

    [Fact]
    public void NothingButTheCaptionPutsAMarkOnAButton()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "D47.App");

        var offenders = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) is not ("Glyphs.cs" or "CaptionStrip.cs"))
            .Where(file => File.ReadAllText(file) is var source
                           && (source.Contains("Glyphs.Mark(", StringComparison.Ordinal)
                               || source.Contains("Glyphs.Draw(", StringComparison.Ordinal)
                               || source.Contains("\"ⓘ\"", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(app, file));

        Assert.Empty(offenders);
    }

    /// <summary>The panel's own markup draws no path: the send arrow is the word SEND.</summary>
    [Fact]
    public void ThePanelMarkupDrawsNoPath()
    {
        var markup = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "D47.App", "Panel", "PanelView.axaml"));

        Assert.DoesNotContain("<Path", markup, StringComparison.Ordinal);
    }
}
