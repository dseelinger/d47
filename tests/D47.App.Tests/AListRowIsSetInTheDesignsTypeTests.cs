using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A list row's name, sub line, aside and list head are drawn in the design's type (#503).</summary>
public class AListRowIsSetInTheDesignsTypeTests
{
    private static object? Resource(string key) => Application.Current!.Resources[key];

    private static Window Show(Control content)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        });

        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void AssertType(TextBlock block, double size, FontWeight weight, double tracking)
    {
        Assert.Equal(Fonts.ChromeFamily, block.FontFamily.ToString());
        Assert.Equal(size, block.FontSize);
        Assert.Equal(weight, block.FontWeight);
        Assert.Equal(tracking, block.LetterSpacing, 3);
    }

    [AvaloniaFact]
    public void ANameAndASubLineAreSairaCapsInTheirInks()
    {
        var name = ListRow.Name(new TextBlock { Text = "Felicity Farseer" });
        var sub = ListRow.Sub(new TextBlock { Text = "Deciat" });
        var aside = ListRow.Aside(new TextBlock { Text = "3 short" });
        var row = ListRow.Dress(new Border { Child = new StackPanel { Children = { name, sub, aside } } });

        var window = Show(row);

        AssertType(name, 15, FontWeight.SemiBold, 0.45);
        Assert.Equal("FELICITY FARSEER", name.Text);
        Assert.Equal(Resource(ThemeManager.WhiteKey), name.Foreground);

        AssertType(sub, 12, FontWeight.Medium, 0.48);
        Assert.Equal("DECIAT", sub.Text);
        Assert.Equal(Resource(ThemeManager.AKey), sub.Foreground);

        AssertType(aside, 13, FontWeight.Medium, 0);
        Assert.Equal("3 SHORT", aside.Text);
        Assert.Equal(Resource(ThemeManager.AKey), aside.Foreground);

        Assert.Equal(new Thickness(12, 6), row.Padding);
        Assert.True(row.Bounds.Height >= 50);

        window.Close();
    }

    [AvaloniaFact]
    public void ASentenceKeepsItsCaseAndTakesOnlyTheInk()
    {
        var line = ListRow.NameInk(new TextBlock { Text = "Buy limpets", FontFamily = Fonts.ProseFamily });
        var row = ListRow.Dress(new Border { Child = line });

        var window = Show(row);

        Assert.Equal("Buy limpets", line.Text);
        Assert.Equal(Fonts.ProseFamily, line.FontFamily.ToString());
        Assert.Equal(Resource(ThemeManager.WhiteKey), line.Foreground);

        window.Close();
    }

    [AvaloniaFact]
    public void AListHeadIsSairaCapsInAOverALineRule()
    {
        var head = ListRow.Head("Ready for Unlock");

        var window = Show(new StackPanel { Children = { head } });

        var label = Assert.IsAssignableFrom<TextBlock>(head.Child);

        AssertType(label, 13, FontWeight.SemiBold, 1.04);
        Assert.Equal("READY FOR UNLOCK", label.Text);
        Assert.Equal(Resource(ThemeManager.AKey), label.Foreground);
        Assert.Equal(new Thickness(0, 0, 0, 1), head.BorderThickness);
        Assert.Equal(Resource(ThemeManager.LineKey), head.BorderBrush);
        Assert.Equal(new Thickness(0, 10, 0, 6), head.Padding);
        Assert.Equal(new Thickness(0, 0, 0, 2), head.Margin);

        window.Close();
    }

    [AvaloniaFact]
    public void AListItemIsFiftyTallWithTheRowsPadding()
    {
        var list = new ListBox { ItemsSource = new[] { "Anaconda" } };

        var window = Show(list);

        var item = Assert.Single(list.GetRealizedContainers().OfType<ListBoxItem>());

        Assert.Equal(50, item.MinHeight);
        Assert.Equal(new Thickness(12, 6), item.Padding);

        window.Close();
    }
}
