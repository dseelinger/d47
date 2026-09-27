using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A screen title is upper case, Saira 28/500 tracked 0.04em, in a block with an optional A context line
/// above it, an optional figure at its right and a 1px A rule 10px under; a group heading's rule is 6px
/// under (#514).
/// </summary>
public class AScreenTitleIsTheDesignsTitleBlockTests
{
    [AvaloniaFact]
    public void AScreenTitleIsUpperCaseAndTrackedAtFourHundredths()
    {
        var title = TitleText.Build("Sacred Fire", TypeScale.Title, TitleRank.Screen);

        Assert.Equal("SACRED FIRE", title.Text);
        Assert.Equal(28, title.FontSize);
        Assert.Equal(FontWeight.Medium, title.FontWeight);
        Assert.Equal(28 * 0.04, title.LetterSpacing, 6);
    }

    [AvaloniaFact]
    public void TheBlockPutsTheContextAboveTheTitleTheFigureRightAndTheRuleTenPixelsUnder()
    {
        var title = TitleText.Build("Sacred Fire", TypeScale.Title, TitleRank.Screen);
        var context = TitleText.Context("Fleet carrier");
        var block = TitleText.Block(title, context, TitleText.Figure("Carrier balance", "990,302,661 cr"));

        var window = new Window { Content = block, Width = 800, Height = 200 };
        window.Show();

        Assert.Equal("FLEET CARRIER", context.Text);
        Assert.Equal(13, context.FontSize);
        Assert.Equal(FontWeight.Medium, context.FontWeight);
        Assert.True(context.Bounds.Bottom <= title.Bounds.Top);

        var texts = block.GetVisualDescendants().OfType<TextBlock>().ToList();
        var label = texts.Single(text => text.Text == "CARRIER BALANCE");
        var value = texts.Single(text => text.Text == "990,302,661 cr");

        Assert.Equal(12, label.FontSize);
        Assert.Equal(18, value.FontSize);
        Assert.True(value.TranslatePoint(default, block)!.Value.X > title.TranslatePoint(default, block)!.Value.X);

        var rule = block.GetVisualDescendants().OfType<Border>().Single(border => border.Height == 1);
        Assert.Equal(10, rule.Margin.Top);

        window.Close();
    }

    [AvaloniaFact]
    public void AGroupHeadingsRuleIsSixPixelsUnder()
    {
        var row = TitleText.GroupRow(TitleText.Build("Tritium", TypeScale.Section, TitleRank.Group));

        var rule = row.GetVisualDescendants().OfType<Border>().Single(border => border.Height == 1);

        Assert.Equal(6, rule.Margin.Top);
    }
}
