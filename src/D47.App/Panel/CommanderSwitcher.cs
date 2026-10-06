using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>
/// The title bar's <c>CMDR NAME ▼</c>, which opens a menu of every Commander and a way to the COMMANDERS
/// page. Hidden while fewer than two Commanders are known.
/// </summary>
public sealed class CommanderSwitcher : Avalonia.Controls.Panel
{
    public const double MenuWidth = 400;

    public const string Manage = "MANAGE COMMANDERS ›";

    private readonly CommanderRoster _roster;
    private readonly Action _manage;
    private readonly Button _button;
    private readonly TextBlock _name;
    private readonly StackPanel _items = new() { Spacing = 2 };
    private readonly Popup _menu;
    private (object? Sightings, string? Shown, int Files) _seen;

    /// <param name="manage">Opens the COMMANDERS page.</param>
    /// <param name="height">The title bar's height.</param>
    public CommanderSwitcher(CommanderRoster roster, Action manage, double height)
    {
        _roster = roster;
        _manage = manage;

        _name = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var chevron = new TextBlock { Text = "▼", FontSize = TypeScale.MetaSmall, VerticalAlignment = VerticalAlignment.Center };

        _button = new Button
        {
            Name = "CommanderSwitcher",
            Theme = Application.Current?.FindResource("D47.CaptionButton") as ControlTheme,
            Height = height,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Margin = new Thickness(14, 0),
                Children = { _name, chevron },
            },
        };
        AutomationProperties.SetName(_button, "Switch Commander");
        Ink(_button, TemplatedControl.ForegroundProperty, ThemeManager.AKey);
        _name.Bind(TextBlock.ForegroundProperty, _button.GetObservable(TemplatedControl.ForegroundProperty));
        chevron.Bind(TextBlock.ForegroundProperty, _button.GetObservable(TemplatedControl.ForegroundProperty));

        var frame = new Border
        {
            Width = MenuWidth,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            Child = _items,
        };
        Ink(frame, Border.BackgroundProperty, ThemeManager.BarKey);
        Ink(frame, Border.BorderBrushProperty, ThemeManager.LineKey);

        _menu = new Popup
        {
            Name = "CommanderMenu",
            PlacementTarget = _button,
            Placement = PlacementMode.BottomEdgeAlignedRight,
            IsLightDismissEnabled = true,
            Child = frame,
        };

        _button.Click += (_, _) =>
        {
            if (!_menu.IsOpen)
            {
                Fill();
            }

            _menu.IsOpen = !_menu.IsOpen;
        };

        _menu.Opened += (_, _) => Opened(true);
        _menu.Closed += (_, _) => Opened(false);

        Children.Add(_button);
        Children.Add(_menu);

        Refresh();
    }

    /// <summary>The label, as shown.</summary>
    public string Label => _name.Text ?? "";

    /// <summary>The menu, for tests.</summary>
    internal Popup Menu => _menu;

    /// <summary>Redraws the label and visibility when the roster has changed. Call on the UI thread.</summary>
    public void Tick()
    {
        if (!_roster.Stamp.Equals(_seen))
        {
            Refresh();
        }
    }

    /// <summary>Opens the menu. Call on the UI thread.</summary>
    public void Open()
    {
        Fill();
        _menu.IsOpen = true;
    }

    private void Refresh()
    {
        _seen = _roster.Stamp;

        var shown = _roster.Commanders.FirstOrDefault(sighting =>
            string.Equals(sighting.FrontierId, _roster.ShownFrontierId, StringComparison.Ordinal));

        _name.Text = shown is null ? "CMDR" : CommanderRoster.Called(shown.Name);
        IsVisible = _roster.CanSwitch;

        if (!IsVisible)
        {
            _menu.IsOpen = false;
        }
        else if (_menu.IsOpen)
        {
            Fill();
        }
    }

    private void Opened(bool open)
    {
        if (open)
        {
            Ink(_button, Button.BackgroundProperty, ThemeManager.AKey);
            Ink(_button, TemplatedControl.ForegroundProperty, ThemeManager.KnockKey);
        }
        else
        {
            _button.ClearValue(Button.BackgroundProperty);
            Ink(_button, TemplatedControl.ForegroundProperty, ThemeManager.AKey);
        }
    }

    private void Fill()
    {
        _items.Children.Clear();

        foreach (var sighting in _roster.Commanders)
        {
            _items.Children.Add(Item(sighting, string.Equals(sighting.FrontierId, _roster.ShownFrontierId, StringComparison.Ordinal)));
        }

        var rule = new Border { Height = 1, Margin = new Thickness(0, 2) };
        Ink(rule, Border.BackgroundProperty, ThemeManager.Line2Key);
        _items.Children.Add(rule);

        var manage = new Button
        {
            Name = "ManageCommanders",
            Content = Manage,
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(40, 0, 14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        manage.Click += (_, _) =>
        {
            _menu.IsOpen = false;
            _manage();
        };
        _items.Children.Add(manage);
    }

    private Button Item(CommanderSighting sighting, bool current)
    {
        var ink = current ? ThemeManager.CyanKey : ThemeManager.WhiteKey;

        var check = Text(current ? "✓" : "", Fonts.ChromeFamily, ThemeManager.CyanKey);
        check.Width = 14;

        var name = Text(CommanderRoster.Called(sighting.Name), Fonts.ChromeFamily, ink);
        name.FontWeight = FontWeight.SemiBold;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Margin = new Thickness(12, 0);

        var when = Text(CommanderRoster.Detected(sighting.LastSeen), Fonts.MonoFamily, current ? ThemeManager.CyanKey : ThemeManager.GreyKey);
        when.FontSize = TypeScale.MetaSmall;
        when.FontWeight = FontWeight.Normal;

        var layout = new DockPanel();
        DockPanel.SetDock(check, Dock.Left);
        DockPanel.SetDock(when, Dock.Right);
        layout.Children.Add(check);
        layout.Children.Add(when);
        layout.Children.Add(name);

        var item = new Button
        {
            Name = current ? "CurrentCommander" : "OtherCommander",
            Content = layout,
            Height = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(item, current ? $"{sighting.Name}, current" : $"Switch to {sighting.Name}");

        if (current)
        {
            Ink(item, Button.BackgroundProperty, ThemeManager.CyanGroundKey);
        }

        item.Click += (_, _) =>
        {
            _menu.IsOpen = false;

            if (!current)
            {
                _roster.Pick(sighting);
            }
        };

        return item;
    }

    private static TextBlock Text(string text, string family, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = TypeScale.Control,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ink(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Ink(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
