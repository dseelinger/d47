using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The d47-row class gives a hand-built row the list-row states and its text their inks (#394).</summary>
public class ASelectedRowNamesItselfInKnockTests
{
    private static object? Resource(string key) => Application.Current!.Resources[key];

    private static (Window Window, Border Row, TextBlock Name, TextBlock Secondary) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var name = ListRow.Name(new TextBlock { Text = "Anaconda" });
        var secondary = ListRow.Secondary(new TextBlock { Text = "Jameson Memorial" });
        var row = ListRow.Dress(new Border { Child = new StackPanel { Children = { name, secondary } } });

        var window = new Window { Content = row, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, row, name, secondary);
    }

    [AvaloniaFact]
    public void ARowAtRestIsTileWithAWhiteNameAndASecondaryLine()
    {
        var (window, row, name, secondary) = Open();

        Assert.Equal(Resource(ThemeManager.TileKey), row.Background);
        Assert.Equal(Resource(ThemeManager.WhiteKey), name.Foreground);
        Assert.Equal(Resource(ThemeManager.AKey), secondary.Foreground);
        Assert.True(row.Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectingARowTurnsItsNameKnockAndItsSecondaryLineBrown()
    {
        var (window, row, name, secondary) = Open();

        row.Classes.Add(ListRow.SelectedClass);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Resource(ThemeManager.AKey), row.Background);
        Assert.Equal(Resource(ThemeManager.KnockKey), name.Foreground);
        Assert.Equal(Resource(ThemeManager.BrownKey), secondary.Foreground);

        window.Close();
    }

    [AvaloniaFact]
    public void ADisabledRowIsSlabWithGreyText()
    {
        var (window, row, name, secondary) = Open();

        row.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Resource(ThemeManager.SlabKey), row.Background);
        Assert.Equal(Resource(ThemeManager.GreyKey), name.Foreground);
        Assert.Equal(Resource(ThemeManager.GreyKey), secondary.Foreground);

        window.Close();
    }
}
