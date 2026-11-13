using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Input;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Input;

namespace D47.App.Controls;

/// <summary>Every D47 key and button Elite binds too, each with REBIND and UNBIND, or OPEN THE SWITCH for a switch position.</summary>
public sealed class BindingConflictsPage : DialogPage
{
    public override string Crumb => "Binding conflicts";

    /// <summary>Marks one conflict's row.</summary>
    public const string RowClass = "binding-conflict";

    /// <summary>Starts a capture on a slot: the slot key, the slot drawn, the line under the row, and a callback told whether it listens.</summary>
    public delegate Task Rebind(string slotKey, Control slot, StatusLine message, Action<bool> listening);

    private const string Intro =
        "Elite binds these too. While Elite has the foreground, D47 takes a key first, so Elite's action on it never "
        + "happens. A shared stick button does both on every press.";

    private const string OwnSide = "D47 changes its own side only. To change Elite's, use Options › Controls in the game.";

    private const string Checked =
        "Only keys and buttons that work while Elite has the foreground are checked, against Elite's active preset. A "
        + "button on a controller that Windows shows as several devices is not listed, because D47 cannot tell which of "
        + "them Elite means.";

    private const string None =
        "None of D47's keys or buttons that work while Elite has the foreground are bound in Elite's active preset.";

    private const string ColumnSizes = "1.3*,*,1.3*,252";

    private readonly SettingsService _settings;
    private readonly BindingConflictEditing _editing;
    private readonly Rebind _rebind;
    private readonly Action<string, StatusLine> _unbind;
    private readonly Func<Control, Task<bool>> _openSwitches;
    private readonly StackPanel _body = new() { Spacing = 20 };

    private string? _drawn;
    private bool _listening;
    private bool _stale;
    private Action? _unwatch;

    public BindingConflictsPage(
        SettingsService settings,
        BindingConflictEditing editing,
        Rebind rebind,
        Action<string, StatusLine> unbind,
        Func<Control, Task<bool>> openSwitches)
    {
        _settings = settings;
        _editing = editing;
        _rebind = rebind;
        _unbind = unbind;
        _openSwitches = openSwitches;

        Title = "Binding conflicts";

        var back = new Button { Name = "Back", Content = "Back", MinWidth = 110 };
        back.Click += (_, _) => Close();

        Modal.Apply(this, "Settings", Title, _body, [back]);

        Redraw();

        AttachedToVisualTree += (_, _) =>
        {
            _settings.Changed -= OnSettingsChanged;
            _settings.Changed += OnSettingsChanged;
            _unwatch?.Invoke();
            _unwatch = _editing.Watch(() => Dispatcher.UIThread.Post(Redraw));
            Redraw();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            _settings.Changed -= OnSettingsChanged;
            _unwatch?.Invoke();
            _unwatch = null;
        };
    }

    private void OnSettingsChanged(SettingsChanged change) => Dispatcher.UIThread.Post(Redraw);

    /// <summary>Draws the conflicts again when they changed; waits while a slot listens, so the capture keeps its slot.</summary>
    private void Redraw()
    {
        if (_listening)
        {
            _stale = true;
            return;
        }

        _stale = false;

        var rows = _settings.Sections.SelectMany(section => section.Rows).ToList();
        var conflicts = BindingConflicts.Find(
            _settings.Current, rows, _editing.Switches(), _editing.Binds(), _editing.Readings(), Gestures.Describe);

        var drawn = string.Join(
            "\n",
            conflicts.Select(conflict => $"{conflict.Kind}|{conflict.RowKey}|{conflict.Switch}|{conflict.Gesture}|{string.Join(",", conflict.EliteActions)}"));

        if (drawn == _drawn)
        {
            return;
        }

        _drawn = drawn;
        _body.Children.Clear();

        _body.Children.Add(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Typeset.Prose(Intro, TypeScale.Body, ThemeManager.WhiteKey, wrap: true),
                Typeset.Prose(OwnSide, TypeScale.Small, ThemeManager.GreyKey, wrap: true),
            },
        });

        var reset = SettingsView.ResetGlyph("NoReset", "Reset Conflicts");
        reset.IsVisible = false;

        var (head, _, _) = SettingsView.GroupHead("Conflicts", conflicts.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), reset);
        var list = new StackPanel { Name = "Conflicts", Children = { head } };

        if (conflicts.Count == 0)
        {
            list.Children.Add(Typeset.Prose(None, TypeScale.Body, ThemeManager.WhiteKey, wrap: true, margin: new Thickness(0, 12, 0, 0)));
        }
        else
        {
            list.Children.Add(ColumnHeads());

            foreach (var conflict in conflicts)
            {
                list.Children.Add(Row(conflict, rows));
            }
        }

        _body.Children.Add(list);
        _body.Children.Add(Typeset.Prose(Checked, TypeScale.Small, ThemeManager.GreyKey, wrap: true));
    }

    private static Grid Columns() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(ColumnSizes),
        ColumnSpacing = 16,
    };

    private static Control ColumnHeads()
    {
        var grid = Columns();
        grid.Margin = new Thickness(12, 8, 12, 4);
        grid.IsHitTestVisible = false;

        string[] captions = ["D47 CONTROL", "KEY OR BUTTON", "ELITE BINDS IT TO"];

        for (var column = 0; column < captions.Length; column++)
        {
            var caption = Typeset.Chrome(captions[column], TypeScale.MetaSmall, ThemeManager.GreyKey, FontWeight.SemiBold);
            Grid.SetColumn(caption, column);
            grid.Children.Add(caption);
        }

        var rule = new Border { Height = 1 };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.LineKey);

        return new StackPanel { Children = { grid, rule } };
    }

    private Control Row(BindingConflict conflict, IReadOnlyList<SettingRow> rows)
    {
        var grid = Columns();
        grid.MinHeight = 56;

        var trail = conflict.Kind == BindingKind.SwitchPosition
            ? _settings.PlaceTrailOf(D47.Core.Capabilities.Builtin.SwitchCapability.ListKey)
            : conflict.RowKey is { } rowKey ? _settings.PlaceTrailOf(rowKey) : null;

        var control = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Typeset.Prose(BindingConflicts.ControlName(conflict, rows), TypeScale.Translation, ThemeManager.WhiteKey, wrap: true) },
        };

        if (trail is not null)
        {
            control.Children.Add(Typeset.Text(trail, TypeScale.Meta, ThemeManager.GreyKey, wrap: true));
        }

        grid.Children.Add(control);

        var (chip, said) = SettingsView.BindingChip(conflict.Gesture);
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        ThemeInk.Bind(chip, Border.BorderBrushProperty, ThemeManager.CyanKey);

        var press = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { chip } };

        if (conflict.Kind != BindingKind.Key)
        {
            press.Children.Add(Typeset.Text("on your stick", TypeScale.Meta, ThemeManager.GreyKey));
        }

        Grid.SetColumn(press, 1);
        grid.Children.Add(press);

        var elite = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        foreach (var action in conflict.EliteActions)
        {
            elite.Children.Add(Typeset.Mono(action, TypeScale.Small, ThemeManager.WhiteKey, wrap: true));
        }

        elite.Children.Add(Typeset.Text(Consequence(conflict.Kind), TypeScale.Meta, ThemeManager.GreyKey, wrap: true));
        Grid.SetColumn(elite, 2);
        grid.Children.Add(elite);

        var message = new StatusLine { Margin = new Thickness(0, 6, 0, 0) };
        var tiles = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gaps.Tile,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (conflict.Kind == BindingKind.SwitchPosition)
        {
            var open = new Button { Name = "OpenTheSwitch", Content = "Open the switch ›", MinWidth = 250 };
            open.Click += async (_, _) => await _openSwitches(open);
            tiles.Children.Add(open);
        }
        else
        {
            var slotKey = conflict.RowKey!;
            var button = conflict.Kind == BindingKind.Button;
            var rebind = new Button { Name = "Rebind", Content = "Rebind", MinWidth = 124 };
            var unbind = new Button { Name = "Unbind", Content = "Unbind", MinWidth = 124 };

            void Listening(bool heard)
            {
                rebind.Classes.Set("selected", heard);
                chip.Classes.Set(BindCapture.ListeningClass, heard);
                chip.BorderThickness = new Thickness(heard ? 1 : 0);
                chip.Padding = heard ? new Thickness(11, 7) : new Thickness(12, 8);
                said.Text = heard ? (button ? "PRESS A BUTTON…" : "PRESS A KEY…") : conflict.Gesture.ToUpperInvariant();
                ThemeInk.Bind(said, TextBlock.ForegroundProperty, heard ? ThemeManager.CyanKey : ThemeManager.WhiteKey);
            }

            rebind.Click += async (_, _) =>
            {
                if (_listening)
                {
                    return;
                }

                _listening = true;

                try
                {
                    await _rebind(slotKey, chip, message, Listening);
                }
                finally
                {
                    _listening = false;
                    Listening(false);

                    if (_stale)
                    {
                        Redraw();
                    }
                }
            };

            unbind.Click += (_, _) => _unbind(slotKey, message);

            tiles.Children.Add(rebind);
            tiles.Children.Add(unbind);
        }

        Grid.SetColumn(tiles, 3);
        grid.Children.Add(tiles);

        var frame = new Border
        {
            Padding = new Thickness(12, 6),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Classes = { RowClass },
            Child = new StackPanel { Children = { grid, message } },
        };
        ThemeInk.Bind(frame, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return frame;
    }

    private static string Consequence(BindingKind kind) => kind switch
    {
        BindingKind.Key => "Elite's action does not happen",
        BindingKind.Button => "Both act on every press",
        _ => "Both act on every flip",
    };
}
