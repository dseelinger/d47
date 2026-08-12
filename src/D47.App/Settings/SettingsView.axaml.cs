using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// The settings surface. Every row on it is generated from a capability descriptor — there is
/// no list of controls here to keep in step with the registry, which is the whole point of
/// descriptors declaring their own rows (architecture.md §5 D5).
/// <para>
/// There is no save button. A control that changes calls <see cref="SettingsService.Apply"/>
/// as <see cref="SettingsCaller.Panel"/>, which validates, persists and announces; every open
/// surface then refreshes from the announcement. That is also why a rejected value snaps back
/// rather than sitting on screen looking accepted.
/// </para>
/// <para>
/// Control choice per row kind: a short closed vocabulary is a ComboBox, because a dialog for
/// seven fixed values is ceremony; the searchable picker is reserved for the long and open
/// lists it was built for — models now, voices and devices in later phases (list.md Phase 4).
/// Free text is only used where the value genuinely is free text.
/// </para>
/// </summary>
public partial class SettingsView : UserControl
{
    private const string DocsBaseUrl = "https://dseelinger.github.io/d47/capabilities";

    private readonly List<SectionView> _sections = [];
    private readonly List<RowView> _rows = [];

    private SettingsService? _settings;
    private ViewStateStore? _viewStateStore;
    private ViewState _viewState = new();
    private AppPaths? _paths;

    /// <summary>True while controls are being written from settings rather than read from.</summary>
    private bool _refreshing;

    private int _activeSection = -1;

    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Binds the view to a live settings service. Called once; the view then follows the
    /// service rather than being told when to update.
    /// </summary>
    public void Attach(SettingsService settings, ViewStateStore viewState, AppPaths paths)
    {
        _settings = settings;
        _viewStateStore = viewState;
        _viewState = viewState.Load();
        _paths = paths;

        StorageLine.Text =
            $"Saved as you go, to {paths.SettingsFile}. Keys are encrypted separately in secrets.json, "
            + "and how this panel is left is remembered in view-state.json.";

        Build();

        settings.Changed += OnSettingsChanged;
        DetachedFromVisualTree += (_, _) => settings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged(SettingsChanged change) => Dispatcher.UIThread.Post(Refresh);

    /// <summary>A brush fetched at call time, so state changes pick up the current theme.</summary>
    private IBrush? Res(string key) => this.FindResource(key) as IBrush;

    /// <summary>
    /// Binds a brush property to a theme resource, so a theme switch repaints controls built
    /// in code the same way DynamicResource repaints the ones built in markup.
    /// </summary>
    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    private void Build()
    {
        var settings = _settings ?? throw new InvalidOperationException("Attach() has not been called.");

        Cards.Children.Clear();
        NavItems.Children.Clear();
        _sections.Clear();
        _rows.Clear();
        _activeSection = -1;

        foreach (var section in settings.Sections)
        {
            var title = section.Capability.Display.PanelTitle ?? section.Capability.Name;
            var card = BuildCard(section, title);

            Cards.Children.Add(card);

            var nav = BuildNavItem(_sections.Count, title);
            NavItems.Children.Add(nav.Item);

            _sections.Add(new SectionView(section.Capability.Id, title, card, nav.Item, nav.Bar, nav.Text));
        }

        SetActiveSection(_sections.Count > 0 ? 0 : -1);
        Refresh();
    }

    private Border BuildCard(SettingsSection section, string title)
    {
        var content = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(18, 4, 18, 18),
            // Applied while building, not after painting: a card that flashes open and then
            // collapses is worse than one that never remembered (list.md Phase 4).
            IsVisible = _viewState.IsExpanded(section.Capability.Id, section.Capability.Display.StartCollapsed),
        };

        string? currentGroup = null;

        foreach (var row in section.Rows)
        {
            // A group heading, stated once, in place of the same sentence on every row.
            if (row.Group is { } group && group != currentGroup)
            {
                content.Children.Add(BuildGroupHeading(group, row.GroupHelp));
                currentGroup = group;
            }
            else if (row.Group is null)
            {
                currentGroup = null;
            }

            var view = BuildRow(section.Capability, row);
            _rows.Add(view);
            content.Children.Add(view.Container);
        }

        var chevron = new TextBlock
        {
            Text = content.IsVisible ? "▾" : "▸",
            FontSize = 11,
            Width = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var heading = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        headerRow.Children.Add(chevron);
        headerRow.Children.Add(heading);

        var header = new Border
        {
            Padding = new Thickness(14, 11),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerRow,
        };

        header.PointerPressed += (_, _) =>
        {
            content.IsVisible = !content.IsVisible;
            chevron.Text = content.IsVisible ? "▾" : "▸";
            RememberCollapse(section.Capability.Id, expanded: content.IsVisible);
        };

        header.PointerEntered += (_, _) => header.Background = Res(ThemeManager.SurfaceAltKey);
        header.PointerExited += (_, _) => header.Background = Brushes.Transparent;

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(content);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = body,
        };

        Themed(card, Border.BackgroundProperty, ThemeManager.SurfaceKey);
        Themed(card, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return card;
    }

    private Control BuildGroupHeading(string group, string? help)
    {
        var heading = new TextBlock
        {
            Text = group,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
        };
        Themed(heading, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        stack.Children.Add(heading);

        if (!string.IsNullOrWhiteSpace(help))
        {
            var note = new TextBlock { Text = help, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
            stack.Children.Add(note);
        }

        var rule = new Border { Height = 1, Margin = new Thickness(0, 8, 0, 0) };
        Themed(rule, Border.BackgroundProperty, ThemeManager.BorderKey);
        stack.Children.Add(rule);

        return stack;
    }

    private (Border Item, Border Bar, TextBlock Text) BuildNavItem(int index, string title)
    {
        var bar = new Border
        {
            Width = 2.5,
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 2),
            Opacity = 0,
        };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AccentKey);

        var text = new TextBlock
        {
            Text = title,
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(text);

        var item = new Border
        {
            Padding = new Thickness(8, 7),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = layout,
        };

        item.PointerPressed += (_, _) => ScrollTo(index);
        item.PointerEntered += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Res(ThemeManager.SurfaceKey);
            }
        };
        item.PointerExited += (_, _) =>
        {
            if (index != _activeSection)
            {
                item.Background = Brushes.Transparent;
            }
        };

        return (item, bar, text);
    }

    private void SetActiveSection(int index)
    {
        if (_activeSection == index)
        {
            return;
        }

        _activeSection = index;
        UpdateNavVisuals();
    }

    /// <summary>
    /// Also re-run by <see cref="Refresh"/>: these brushes are fetched, not bound, so a theme
    /// switch has to repaint them or the active item keeps the old theme's colours.
    /// </summary>
    private void UpdateNavVisuals()
    {
        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            var active = i == _activeSection;

            section.NavBar.Opacity = active ? 1 : 0;
            section.NavItem.Background = active ? Res(ThemeManager.SurfaceAltKey) : Brushes.Transparent;
            section.NavText.Foreground = Res(active ? ThemeManager.TextKey : ThemeManager.TextMutedKey);
            section.NavText.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;
        }
    }

    private void ScrollTo(int index)
    {
        if (index < 0 || index >= _sections.Count)
        {
            return;
        }

        SetActiveSection(index);
        Scroller.Offset = new Vector(0, CardTop(_sections[index].Card));
    }

    /// <summary>The card's position in the scroller's content space, margins included.</summary>
    private double CardTop(Border card) => card.Bounds.Y + Cards.Bounds.Y - 8;

    /// <summary>
    /// Highlights the section the panel is actually showing — the topmost card still in view —
    /// rather than the last one clicked (list.md Phase 4, "Settings Nav Menu").
    /// </summary>
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_sections.Count == 0)
        {
            return;
        }

        // At the very bottom, the last card is the answer even when it is too short to ever
        // become topmost — the classic scroll-spy edge.
        if (Scroller.Offset.Y >= Scroller.Extent.Height - Scroller.Viewport.Height - 2)
        {
            SetActiveSection(_sections.Count - 1);
            return;
        }

        var offset = Scroller.Offset.Y;
        var topmost = 0;

        for (var i = 0; i < _sections.Count; i++)
        {
            // Topmost once its head has passed the top edge, with a little tolerance so a card
            // sitting exactly at the edge does not flicker between two answers.
            if (CardTop(_sections[i].Card) <= offset + 16)
            {
                topmost = i;
            }
            else
            {
                break;
            }
        }

        SetActiveSection(topmost);
    }

    private void RememberCollapse(string capabilityId, bool expanded)
    {
        _viewState = _viewState.With(capabilityId, expanded);
        _viewStateStore?.Save(_viewState);
    }

    private void OnOpenDataFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_paths is not null)
        {
            Process.Start(new ProcessStartInfo(_paths.Data) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// Re-reads every row from settings. Cheaper than rebuilding and, more importantly, it does
    /// not pull the control out from under whatever has focus.
    /// </summary>
    private void Refresh()
    {
        if (_settings is null)
        {
            return;
        }

        UpdateNavVisuals();

        _refreshing = true;
        try
        {
            foreach (var row in _rows)
            {
                // A row that does not apply is absent, not disabled: a greyed-out control still
                // asserts that the setting exists (list.md Phase 4).
                row.Container.IsVisible = row.Row.Applies(_settings.Current);
                row.Refresh();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private RowView BuildRow(CapabilityDescriptor capability, SettingRow row)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var label = new TextBlock
        {
            Text = row.Label,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextKey);
        header.Children.Add(label);

        if (row.Protected)
        {
            // Said on the row rather than only in the docs: a Commander who asks d47 to change
            // this and gets refused should already know why.
            var tag = new TextBlock { Text = "protected", FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            Themed(tag, TextBlock.ForegroundProperty, ThemeManager.AccentMutedKey);

            var pill = new Border
            {
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tag,
            };
            Themed(pill, Border.BorderBrushProperty, ThemeManager.AccentMutedKey);

            header.Children.Add(pill);
        }

        // One link per row, to that row's own section of that capability's page. In-app help is
        // the short form; the page is the long form (list.md Phase 4).
        var docs = new Button
        {
            Content = "?",
            FontSize = 10,
            Padding = new Thickness(5, 0),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        Themed(docs, Button.ForegroundProperty, ThemeManager.TextMutedKey);
        ToolTip.SetTip(docs, "Open the setup guide for this setting");
        docs.Click += (_, _) => OpenDocs(capability, row);
        header.Children.Add(docs);

        var help = new TextBlock
        {
            Text = row.Help,
            FontSize = 11,
            Margin = new Thickness(0, 2, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = !string.IsNullOrWhiteSpace(row.Help),
        };
        Themed(help, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var message = new TextBlock
        {
            FontSize = 11,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(message, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        var (control, refresh, compact) = BuildControl(row, message);

        var caption = new StackPanel { Spacing = 0 };
        caption.Children.Add(header);
        caption.Children.Add(help);

        Control body;
        if (compact)
        {
            // Label and help on the left, the control on the right — the layout every settings
            // surface a Commander already knows uses for one-glance rows.
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,Auto") };
            Grid.SetColumn(caption, 0);
            Grid.SetColumn(control, 2);
            control.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(caption);
            grid.Children.Add(control);
            body = grid;
        }
        else
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(caption);
            stack.Children.Add(control);
            body = stack;
        }

        var container = new StackPanel();
        container.Children.Add(body);
        container.Children.Add(message);

        return new RowView(row, container, refresh);
    }

    /// <summary>
    /// The control for a row, its refresh action, and whether it is compact enough to sit to
    /// the right of its own caption.
    /// </summary>
    private (Control Control, Action Refresh, bool Compact) BuildControl(SettingRow row, TextBlock message)
    {
        switch (row.Kind)
        {
            case SettingKind.Info:
                return BuildInfo(row);

            case SettingKind.Toggle:
                return BuildToggle(row, message);

            case SettingKind.Choice when row.AllowsFreeText || row.ChoiceSource is not null:
                // Long or open vocabulary: the searchable picker, which stays usable when the
                // list is empty because the value can be typed (list.md Phase 4).
                return BuildPickerButton(row, message);

            case SettingKind.Choice:
                return BuildComboBox(row, message);

            case SettingKind.Number:
                return BuildNumber(row, message);

            case SettingKind.Secret:
                return BuildSecret(row, message);

            case SettingKind.Hotkey:
                return BuildHotkey(row, message);

            default:
                return BuildText(row, message);
        }
    }

    private (Control, Action, bool) BuildInfo(SettingRow row)
    {
        var text = new SelectableTextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap };
        Themed(text, SelectableTextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var inset = new Border
        {
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(4),
            Child = text,
        };
        Themed(inset, Border.BackgroundProperty, ThemeManager.SurfaceAltKey);

        return (inset, () => text.Text = row.Binding!.Read(_settings!.Current), false);
    }

    private (Control, Action, bool) BuildToggle(SettingRow row, TextBlock message)
    {
        var toggle = new ToggleSwitch
        {
            OnContent = null,
            OffContent = null,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
        };

        toggle.IsCheckedChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(row, toggle.IsChecked == true ? "true" : "false", message);
            }
        };

        return (toggle, () => toggle.IsChecked = _settings!.Read(row.Key) is "true", true);
    }

    private (Control, Action, bool) BuildComboBox(SettingRow row, TextBlock message)
    {
        var combo = new ComboBox { MinWidth = 190, HorizontalAlignment = HorizontalAlignment.Right };

        var choices = row.Choices;

        // A clear item only where clearing means something. Provider and theme always hold a
        // value, so "(default: anthropic)" above "anthropic" would be the same answer twice.
        var clearable = row.IsClearable;

        var items = new List<string>();
        if (clearable)
        {
            items.Add(row.DefaultDisplay is null ? "(default)" : $"(default: {row.DefaultDisplay})");
        }

        items.AddRange(choices.Select(row.LabelForChoice));
        combo.ItemsSource = items;

        var offset = clearable ? 1 : 0;

        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedIndex < 0)
            {
                return;
            }

            Apply(row, clearable && combo.SelectedIndex == 0 ? null : choices[combo.SelectedIndex - offset], message);
        };

        return (combo, () =>
        {
            var value = _settings!.Read(row.Key);
            var found = value is null
                ? -1
                : choices.Select((choice, i) => (choice, i))
                    .Where(pair => string.Equals(pair.choice, value, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => (int?)pair.i)
                    .FirstOrDefault() ?? -1;

            combo.SelectedIndex = found < 0 ? (clearable ? 0 : -1) : found + offset;
        }, true);
    }

    private (Control, Action, bool) BuildPickerButton(SettingRow row, TextBlock message)
    {
        var value = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

        var chevron = new TextBlock { Text = "⌄", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Themed(chevron, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var layout = new DockPanel { MinWidth = 190 };
        DockPanel.SetDock(chevron, Dock.Right);
        layout.Children.Add(chevron);
        layout.Children.Add(value);

        var button = new Button
        {
            Content = layout,
            Padding = new Thickness(11, 6),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        // Dressed as the combo box beside it, because it does the same job. The default button
        // chrome reads as disabled next to a real combo, which is the opposite of the truth.
        Themed(button, Button.BackgroundProperty, ThemeManager.SurfaceAltKey);
        Themed(button, Button.BorderBrushProperty, ThemeManager.BorderKey);

        button.Click += async (_, _) => await ChooseAsync(row, button, message);

        return (button, () =>
        {
            var current = _settings!.Read(row.Key);
            value.Text = current is null
                ? $"{row.DefaultDisplayFor(_settings.Current) ?? "not set"} (default)"
                : row.LabelForChoice(current);
            Themed(value, TextBlock.ForegroundProperty, current is null ? ThemeManager.TextMutedKey : ThemeManager.TextKey);
        }, true);
    }

    private (Control, Action, bool) BuildNumber(SettingRow row, TextBlock message)
    {
        var number = new NumericUpDown
        {
            Increment = 1,
            FormatString = "0",
            MinWidth = 130,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        number.ValueChanged += (_, e) =>
        {
            if (!_refreshing)
            {
                Apply(row, e.NewValue?.ToString("0"), message);
            }
        };

        return (number, () =>
        {
            number.Value = decimal.TryParse(_settings!.Read(row.Key), out var parsed) ? parsed : null;
            number.PlaceholderText = row.DefaultDisplayFor(_settings.Current);
        }, true);
    }

    private (Control, Action, bool) BuildText(SettingRow row, TextBlock message)
    {
        var box = new TextBox
        {
            AcceptsReturn = row.Multiline,
            TextWrapping = row.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 460,
        };

        if (row.Multiline)
        {
            box.MinHeight = 90;
        }

        // Applied on leaving the box rather than on every keystroke: a setting that persists per
        // character would write a file per character, and would reject half-typed URLs as it went.
        box.LostFocus += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(row, box.Text, message);
            }
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !row.Multiline)
            {
                e.Handled = true;
                Apply(row, box.Text, message);
            }
        };

        return (box, () =>
        {
            box.Text = _settings!.Read(row.Key) ?? string.Empty;

            // The default is a placeholder, never a value, so "I have not chosen" stays
            // distinguishable from "I chose the default" (list.md Phase 4).
            box.PlaceholderText = row.DefaultDisplayFor(_settings.Current) ?? string.Empty;
        }, false);
    }

    private (Control, Action, bool) BuildSecret(SettingRow row, TextBlock message)
    {
        var box = new TextBox
        {
            PasswordChar = '•',
            PlaceholderText = "Paste a key to store it",
            Width = 280,
        };

        var state = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Themed(state, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var store = new Button { Content = "Store" };
        var clear = new Button { Content = "Clear" };

        store.Click += (_, _) =>
        {
            if (Apply(row, box.Text, message))
            {
                // Never held in a control after it is stored. The store is write-only and so is
                // the box that fed it.
                box.Text = string.Empty;
            }
        };

        clear.Click += (_, _) =>
        {
            box.Text = string.Empty;
            Apply(row, null, message);
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(box);
        panel.Children.Add(store);
        panel.Children.Add(clear);
        panel.Children.Add(state);

        return (panel, () =>
            state.Text = _settings!.HasSecret(row.SecretName) ? "A key is stored." : "No key stored.", false);
    }

    private (Control, Action, bool) BuildHotkey(SettingRow row, TextBlock message)
    {
        var button = new Button { MinWidth = 150, HorizontalContentAlignment = HorizontalAlignment.Center };
        var clear = new Button { Content = "Unbind" };

        button.Click += async (_, _) => await CaptureHotkeyAsync(row, button, message);
        clear.Click += (_, _) => Apply(row, null, message);

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        panel.Children.Add(button);
        panel.Children.Add(clear);

        return (panel, () =>
        {
            var bound = _settings!.Read(row.Key);
            button.Content = bound is null ? "Press to bind" : Gestures.Describe(bound);
        }, true);
    }

    private async Task ChooseAsync(SettingRow row, Button button, TextBlock message)
    {
        if (_settings is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var result = await PickerWindow.ShowAsync(owner, new PickerRequest
        {
            Prompt = row.Label,
            Help = row.Help,
            Choices = row.ChoicesFor(_settings.Current),
            Describe = row.ChoiceLabel,
            Current = _settings.Read(row.Key),
            DefaultDisplay = row.IsClearable ? row.DefaultDisplayFor(_settings.Current) : null,
            AllowsFreeText = row.AllowsFreeText,
        });

        if (result is not null)
        {
            Apply(row, result.Value, message);
        }

        button.Focus();
    }

    /// <summary>
    /// Binds a gesture by listening for one, rather than by offering a list of key names. There
    /// is no way to type a key that does not exist, and no list to keep in step with a keyboard
    /// layout (list.md Phase 4, "Hotkey Binding").
    /// </summary>
    private async Task CaptureHotkeyAsync(SettingRow row, Button button, TextBlock message)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var previous = button.Content;
        button.Content = "Press a key…";

        var captured = new TaskCompletionSource<KeyGesture?>();

        void OnKey(object? sender, KeyEventArgs e)
        {
            // A modifier on its own is someone still assembling the chord, not a binding.
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            {
                return;
            }

            e.Handled = true;
            captured.TrySetResult(e.Key == Key.Escape ? null : new KeyGesture(e.Key, e.KeyModifiers));
        }

        // Tunnelling: the gesture belongs to the binding, not to whatever control the click left
        // focused, so it has to be seen on the way down.
        top.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);

        try
        {
            var gesture = await captured.Task;

            if (gesture is not null)
            {
                Apply(row, gesture.ToString(), message);
            }
        }
        finally
        {
            top.RemoveHandler(KeyDownEvent, OnKey);
            button.Content = previous;
            Refresh();
        }
    }

    private bool Apply(SettingRow row, string? value, TextBlock message)
    {
        if (_settings is null)
        {
            return false;
        }

        var result = _settings.Apply(row.Key, value, SettingsCaller.Panel);

        // Only failures are worth saying. A change that worked is visible in the control that
        // made it, and the panel is not a log.
        message.IsVisible = !result.Ok;
        message.Text = result.Message;

        if (!result.Ok)
        {
            // A rejected value never reached the store, so the control has to stop showing it.
            Refresh();
        }

        return result.Ok;
    }

    private static void OpenDocs(CapabilityDescriptor capability, SettingRow row)
    {
        var anchor = row.DocsAnchor is { } value ? $"#{value}" : string.Empty;

        Process.Start(new ProcessStartInfo($"{DocsBaseUrl}/{capability.Id}.html{anchor}")
        {
            UseShellExecute = true,
        });
    }

    private sealed record SectionView(
        string CapabilityId,
        string Title,
        Border Card,
        Border NavItem,
        Border NavBar,
        TextBlock NavText);

    private sealed record RowView(SettingRow Row, Control Container, Action Refresh);
}
