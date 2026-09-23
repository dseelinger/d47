using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using D47.App.Theming;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Upper-case chrome is tracked at 0.04–0.08 of its size; sentences and row labels are not tracked; a
/// screen title does not glow (#391).
/// </summary>
public class UpperCaseChromeIsTrackedLightlyTests
{
    private const double Least = 0.04;
    private const double Most = 0.08;

    private static void LoadThemes()
    {
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });
        Application.Current.Resources.MergedDictionaries.Add(
            new Avalonia.Markup.Xaml.Styling.ResourceInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
            });
    }

    private static double Setter(ControlTheme theme, string property)
    {
        for (var current = theme; current is not null; current = current.BasedOn)
        {
            if (current.Setters.OfType<Setter>().FirstOrDefault(s => s.Property?.Name == property) is { } found)
            {
                return Convert.ToDouble(found.Value, CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException($"{property} is not set.");
    }

    public static TheoryData<string> Themes => ["Button", "CheckBox", "D47.Segment", "D47.TextChoice", "D47.Tab"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void AControlThemeTracksItsLabelByItsSize(string key)
    {
        LoadThemes();

        object resourceKey = key switch
        {
            "Button" => typeof(Button),
            "CheckBox" => typeof(CheckBox),
            _ => key,
        };
        var theme = Assert.IsType<ControlTheme>(Application.Current!.FindResource(resourceKey));

        Assert.InRange(Setter(theme, "LetterSpacing") / Setter(theme, "FontSize"), Least, Most);
    }

    [AvaloniaTheory]
    [InlineData(TitleRank.Window)]
    [InlineData(TitleRank.Screen)]
    [InlineData(TitleRank.Group)]
    [InlineData(TitleRank.Subgroup)]
    public void AnUpperCaseTitleIsTrackedByItsSize(TitleRank rank)
    {
        var block = TitleText.Build("heading", TypeScale.Section, rank);

        Assert.InRange(block.LetterSpacing / block.FontSize, Least, Most);
    }

    [AvaloniaTheory]
    [InlineData(TitleRank.Screen)]
    [InlineData(TitleRank.Group)]
    public void ASentenceIsNotTracked(TitleRank rank)
    {
        Assert.Equal(0, TitleText.Build("Where to next?", TypeScale.Section, rank, sentence: true).LetterSpacing);
    }

    [AvaloniaFact]
    public void ARowLabelIsNotTracked()
    {
        Assert.Equal(0, TitleText.Build("Row label", TypeScale.Body, TitleRank.Row, sentence: true).LetterSpacing);
    }

    [AvaloniaFact]
    public void AScreenTitleDoesNotGlow()
    {
        var title = TitleText.Screen("Screen title");
        var window = new Window { Content = title };
        window.Show();

        Assert.IsNotType<BloomStack>(title);
        Assert.Empty(title.GetVisualDescendants().OfType<BloomStack>());

        window.Close();
    }
}
