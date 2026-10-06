using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Commanders: every Commander in the journals, with a switch to each.</summary>
public sealed class CommandersPage : UserControl
{
    public const string RootKey = "commanders";

    public const string Helper =
        "D47 starts with the commander it last detected in the journal. Switch to another and D47 stays with them until you switch again.";

    public const string NoneYet = "No commander has been found in the journals yet.";

    private const string Columns = "1.4*,*,*,170,140";

    private readonly CommanderRoster _roster;
    private readonly StackPanel _body = new();
    private (object? Sightings, string? Shown, int Files) _seen;

    public CommandersPage(CommanderRoster roster)
    {
        _roster = roster;

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                LoadoutPages.Scrolling(new Grid
                {
                    ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 1100 }, new ColumnDefinition(GridLength.Auto)],
                    Children = { _body },
                }),
            },
        };

        Draw();
    }

    /// <summary>Redraws when the list or the shown Commander has changed since the last draw.</summary>
    public bool Tick()
    {
        if (_roster.Stamp.Equals(_seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>The line under the list: <c>3 commanders · found in 41 journal files</c>.</summary>
    public static string Tally(int commanders, int files) => string.Create(
        CultureInfo.InvariantCulture,
        $"{commanders:N0} {(commanders == 1 ? "commander" : "commanders")} · found in {files:N0} journal {(files == 1 ? "file" : "files")}");

    private void Draw()
    {
        _seen = _roster.Stamp;
        _body.Children.Clear();

        _body.Children.Add(TitleText.Screen("Commanders"));

        var helper = Text(Helper, Fonts.ProseFamily, TypeScale.Body, ThemeManager.GreyKey);
        helper.TextWrapping = TextWrapping.Wrap;
        helper.Margin = new Thickness(0, 14, 0, 14);
        _body.Children.Add(helper);

        var commanders = _roster.Commanders;

        if (commanders.Count == 0)
        {
            _body.Children.Add(Text(NoneYet, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
            return;
        }

        var rows = new StackPanel { Spacing = 2 };
        rows.Children.Add(Header());

        foreach (var sighting in commanders)
        {
            rows.Children.Add(Row(sighting, string.Equals(sighting.FrontierId, _roster.ShownFrontierId, StringComparison.Ordinal)));
        }

        _body.Children.Add(rows);

        var tally = Text(Tally(commanders.Count, _roster.FilesExamined), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        tally.Margin = new Thickness(0, 14, 0, 0);
        _body.Children.Add(tally);
    }

    private static Grid Header()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(Columns),
            ColumnSpacing = 16,
            Margin = new Thickness(14, 0, 4, 6),
        };

        string[] names = ["NAME", "SHIP", "LAST LOCATION", "LAST DETECTED"];

        for (var i = 0; i < names.Length; i++)
        {
            var label = Text(names[i], Fonts.ChromeFamily, TypeScale.MetaSmall, ThemeManager.GreyKey);
            label.FontWeight = FontWeight.SemiBold;
            label.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;
            Grid.SetColumn(label, i);
            grid.Children.Add(label);
        }

        return grid;
    }

    private Border Row(CommanderSighting sighting, bool current)
    {
        var name = Text(CommanderRoster.Called(sighting.Name), Fonts.ChromeFamily, TypeScale.ControlLarge, current ? ThemeManager.CyanKey : ThemeManager.WhiteKey);
        name.FontWeight = FontWeight.SemiBold;

        var ship = Text(sighting.Ship ?? "—", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);
        Grid.SetColumn(ship, 1);

        var location = Text(sighting.StarSystem ?? "—", Fonts.ChromeFamily, TypeScale.ControlLarge, current ? ThemeManager.CyanKey : ThemeManager.AKey);
        Grid.SetColumn(location, 2);

        var detected = Text(CommanderRoster.Detected(sighting.LastSeen), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey);
        Grid.SetColumn(detected, 3);

        Control action;

        if (current)
        {
            var mark = Text("✓ CURRENT", Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.CyanKey);
            mark.FontWeight = FontWeight.SemiBold;
            mark.LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking;
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            action = mark;
        }
        else
        {
            var button = new Button
            {
                Name = "CommanderSwitch",
                Content = "SWITCH",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            AutomationProperties.SetName(button, $"Switch to {sighting.Name}");
            button.Click += (_, _) => _roster.Pick(sighting);
            action = button;
        }

        Grid.SetColumn(action, 4);

        var row = new Border
        {
            MinHeight = 52,
            Padding = new Thickness(14, 4, 4, 4),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(Columns),
                ColumnSpacing = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { name, ship, location, detected, action },
            },
        };
        Themed(row, Border.BackgroundProperty, current ? ThemeManager.CyanGroundKey : ThemeManager.SlabKey);

        return row;
    }

    private static TextBlock Text(string text, string family, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
