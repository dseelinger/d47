using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A Fleet row's and card's note, gear and plan dot follow the list-row secondary ink (#410).</summary>
public class AShowingFleetRowInksItsMarksBrownTests
{
    private static object? Resource(string key) => Application.Current!.Resources[key];

    private static Window Show(Control content)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var window = new Window { Content = content, Width = 600, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Button FleetRow(bool showing) =>
        (Button)LoadoutPages.Row("Large Hardpoint 1", "Long range", marked: true, () => { }, engineered: true, showing: showing);

    private static Button FleetCard(bool showing) =>
        (Button)LoadoutPages.Card("Bad Idea (Python)", "Python\nJameson Memorial", marked: true, () => { }, LoadoutStanding.Owned, showing: showing, headline: "Bad Idea");

    private static TextBlock Block(Button button, Func<TextBlock, bool> match) =>
        button.GetVisualDescendants().OfType<TextBlock>().First(match);

    private static Run Mark(Button button, string glyph) =>
        button.GetVisualDescendants().OfType<TextBlock>()
            .SelectMany(block => block.Inlines ?? [])
            .OfType<Run>()
            .First(run => run.Text == glyph);

    [AvaloniaTheory]
    [InlineData(false, ThemeManager.WhiteKey, ThemeManager.AKey)]
    [InlineData(true, ThemeManager.KnockKey, ThemeManager.BrownKey)]
    public void ARowsNoteGearAndDotTakeTheSecondaryInk(bool showing, string nameKey, string secondaryKey)
    {
        var row = FleetRow(showing);
        var window = Show(row);

        var name = Block(row, block => block.Inlines is { Count: > 0 });
        var note = Block(row, block => block.Text == "Long range");
        var dot = Block(row, block => block.Text == "●");

        Assert.Equal(Resource(nameKey), name.Foreground);
        Assert.Equal(Resource(secondaryKey), note.Foreground);
        Assert.Equal(Resource(secondaryKey), dot.Foreground);
        Assert.Equal(Resource(secondaryKey), Mark(row, " ⚙").Foreground);
        Assert.Equal(default, row.BorderThickness);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false, ThemeManager.TileKey, ThemeManager.WhiteKey, ThemeManager.AKey)]
    [InlineData(true, ThemeManager.AKey, ThemeManager.KnockKey, ThemeManager.BrownKey)]
    public void ACardsLinesAndDotTakeTheSecondaryInk(bool showing, string groundKey, string nameKey, string secondaryKey)
    {
        var card = FleetCard(showing);
        var window = Show(card);

        var name = Block(card, block => block.Inlines is { Count: > 0 });
        var hull = Block(card, block => block.Text == "Python");

        Assert.Equal(Resource(groundKey), card.Background);
        Assert.Equal(Resource(nameKey), name.Foreground);
        Assert.Equal(Resource(secondaryKey), hull.Foreground);
        Assert.Equal(Resource(secondaryKey), Mark(card, " ●").Foreground);
        Assert.Equal(default, card.BorderThickness);

        window.Close();
    }

    [AvaloniaFact]
    public void ADisabledRowInksItsMarksGrey()
    {
        var row = FleetRow(showing: false);
        row.IsEnabled = false;
        var window = Show(row);

        Assert.Equal(Resource(ThemeManager.SlabKey), row.Background);
        Assert.Equal(Resource(ThemeManager.GreyKey), Mark(row, " ⚙").Foreground);
        Assert.Equal(Resource(ThemeManager.GreyKey), Block(row, block => block.Text == "●").Foreground);

        window.Close();
    }
}
