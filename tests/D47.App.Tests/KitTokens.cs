using Xunit;

namespace D47.App.Tests;

/// <summary>Which optional checks a page's kit-token gate applies.</summary>
public readonly record struct KitTokenRules(bool RefuseBorderThickness, bool CheckResourcePattern, bool Warn);

public static class KitTokens
{
    private const string Names =
        "Background|Surface|SurfaceAlt|Border|Text|TextMuted|TextFaint|Accent|AccentMuted|Danger|{0}Good|Info|Rule|FillLow|FillHigh|FillHigher|AccentBorder|AccentInk|CardFill|CardFillSelected|RowFill|TagBorder|PaneFill|PaneBorder|TagInk";

    public static void AssertDrawsOnlyInTheNewTokens(string source, KitTokenRules rules)
    {
        var names = string.Format(System.Globalization.CultureInfo.InvariantCulture, Names, rules.Warn ? "Warn|" : "");

        Assert.DoesNotContain("CornerRadius", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardChrome", source, StringComparison.Ordinal);
        if (rules.RefuseBorderThickness)
        {
            Assert.DoesNotContain("BorderThickness", source, StringComparison.Ordinal);
        }

        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}\b", source);
        Assert.DoesNotMatch($@"ThemeManager\.({names})Key\b", source);
        if (rules.CheckResourcePattern)
        {
            Assert.DoesNotMatch($@"D47\.({names})\}}", source);
        }
    }
}
