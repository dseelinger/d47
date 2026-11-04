using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Watches;

namespace D47.App.Panel;

/// <summary>Commander › Watches › one watch: adds a new one, or edits and deletes the one named.</summary>
public sealed class WatchFormPage : UserControl
{
    public const string WhereHelp = "A rare fills in its station and commodity. Any other station is looked up once, when you save.";

    public const string WhenHelp = "Market readings older than 8 hours don't count.";

    private readonly WatchesDesk _desk;
    private readonly PanelNavigator _nav;
    private readonly Watch? _existing;
    private readonly bool _stock;
    private readonly StackPanel _body = new() { Spacing = 20 };
    private readonly Segment _source = new() { ItemsSource = ["A rare", "Any station"] };
    private readonly Segment _comparison = new() { ItemsSource = ["Above", "Below"] };
    private readonly InlinePicker _rarePicker = new() { Label = "Rare" };
    private readonly InlinePicker _stationPicker = new() { Label = "Station" };
    private readonly FormField _system = new("System", "Type a system", FieldNeed.Required, width: 320);
    private readonly TextBox _commodity = Box("Commodity", "As the market spells it", 320);
    private readonly TextBox _units = Box("Units", "0 or more", 220);
    private readonly TextBox _name = Box("Name", string.Empty, 480);
    private readonly CheckBox _enabled = LabeledCheckBox.Caps("Enabled");
    private readonly Dictionary<WatchField, TextBlock> _messages = [];
    private readonly Control _rareRow;
    private readonly Control _systemRow;
    private readonly StackPanel _findRow = new();
    private readonly Control _stationRow;
    private readonly Control _commodityRow;
    private readonly Control _rareCommodity;
    private readonly TextBlock _rareCommodityText;
    private readonly Button _save = new() { Content = "SAVE" };
    private RareEntry? _rare;
    private string? _station;
    private Notice? _failure;
    private bool _saving;

    public WatchFormPage(WatchesDesk desk, PanelNavigator nav, Watch? existing, bool stock)
    {
        _desk = desk;
        _nav = nav;
        _existing = existing;
        _stock = stock;

        _enabled.IsChecked = existing?.Enabled ?? true;
        _name.Text = existing?.Name;
        _name.PlaceholderText = stock ? "Filled in as the commodity at the station" : WatchSetup.DefaultCommunityGoalName;

        _body.Children.Add(TitleText.Screen(existing?.Name ?? (stock ? "New stock watch" : "New Community Goal watch")));

        if (stock)
        {
            _rareCommodityText = WatchesPage.Text(string.Empty, Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);
            _rareRow = Row("Rare", _rarePicker);
            _systemRow = Row("System", _system.Control);
            _stationRow = Row("Station", _stationPicker);
            _commodityRow = Row("Commodity", _commodity);
            _rareCommodity = Row("Commodity", _rareCommodityText);
            Prefill();
            BuildStock();
        }
        else
        {
            _rareRow = _systemRow = _stationRow = _commodityRow = _rareCommodity = new Border();
            _rareCommodityText = new TextBlock();
            BuildGoal();
        }

        Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children = { LoadoutPages.Scrolling(new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = 900 }], Children = { _body } }) },
        };
    }

    private static TextBox Box(string name, string placeholder, double width)
    {
        var box = new TextBox { Width = width, HorizontalAlignment = HorizontalAlignment.Left, PlaceholderText = placeholder };
        AutomationProperties.SetName(box, name);

        return box;
    }

    private static Control Row(string label, Control control)
    {
        var caption = WatchesPage.Text(label.ToUpperInvariant(), Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.GreyKey);
        caption.FontWeight = FontWeight.SemiBold;
        caption.LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking;

        return new StackPanel { Spacing = 3, Children = { caption, control } };
    }

    private static Control Group(string name, string help, params Control[] rows)
    {
        var group = new StackPanel { Spacing = 10 };
        var head = WatchesPage.Text(name.ToUpperInvariant(), Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.AKey);
        head.FontWeight = FontWeight.SemiBold;
        group.Children.Add(head);

        var description = WatchesPage.Text(help, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);
        description.TextWrapping = TextWrapping.Wrap;
        group.Children.Add(description);

        foreach (var row in rows)
        {
            group.Children.Add(row);
        }

        return group;
    }

    private TextBlock Message(WatchField field)
    {
        var block = WatchesPage.Text(string.Empty, Fonts.ProseFamily, TypeScale.Small, ThemeManager.RedKey);
        block.TextWrapping = TextWrapping.Wrap;
        block.IsVisible = false;
        _messages[field] = block;

        return block;
    }

    private void Prefill()
    {
        if (_existing is not { } watch)
        {
            _source.SelectedIndex = 0;
            _comparison.SelectedIndex = 0;
            _units.Text = string.Empty;

            return;
        }

        _comparison.SelectedIndex = watch.Comparison == StockComparison.Below ? 1 : 0;
        _units.Text = watch.Threshold?.ToString(CultureInfo.InvariantCulture);

        var rare = RareCatalogue.All.FirstOrDefault(entry => entry.MarketId == watch.MarketId && entry.Name == watch.Commodity);
        _source.SelectedIndex = rare is null ? 1 : 0;
        _rare = rare;
        _system.SetText(watch.System);
        _commodity.Text = watch.Commodity;
        _station = watch.Station;
    }

    private void BuildStock()
    {
        _rarePicker.Mono = false;
        _stationPicker.Mono = false;
        AutomationProperties.SetName(_source, "Station from");
        AutomationProperties.SetName(_comparison, "Stock is");

        if (_desk.SystemNames is { } names)
        {
            _system.Suggest(names, () => true);
        }

        _rarePicker.Picked += (_, symbol) =>
        {
            _rare = RareCatalogue.Find(symbol);
            ShowRare();
            ShowMode();
        };

        _stationPicker.Picked += (_, name) =>
        {
            _station = name;
            ShowStations([name]);
        };

        _system.Box.LostFocus += async (_, _) => await FindStationsAsync();
        _source.SelectionChanged += (_, _) => ShowMode();

        var find = new Button { Content = "FIND STATIONS", HorizontalAlignment = HorizontalAlignment.Left };
        find.Click += async (_, _) => await FindStationsAsync();
        _findRow.Children.Add(find);

        if (_existing is { } watch)
        {
            _body.Children.Add(Group(
                "Last fired",
                "A change to where or when lets it fire again this tick.",
                Row("Fired for", WatchesPage.Text(WatchFired.Text(watch.FiredForTick, TimeZoneInfo.Local), Fonts.MonoFamily, TypeScale.Body, ThemeManager.WhiteKey))));
        }

        _body.Children.Add(Group(
            "Where",
            WhereHelp,
            Row("Station from", _source),
            _rareRow,
            _rareCommodity,
            _systemRow,
            _findRow,
            _stationRow,
            Message(WatchField.System),
            Message(WatchField.Station),
            _commodityRow,
            Message(WatchField.Commodity)));

        _units.FontFamily = new FontFamily(Fonts.MonoFamily);

        _body.Children.Add(Group(
            "When",
            WhenHelp,
            Row("Stock is", _comparison),
            Row("Units", _units),
            Message(WatchField.Units),
            Row("Name", _name),
            _enabled));

        _body.Children.Add(Footer());

        ShowRare();
        ShowStations(_station is null ? [] : [_station]);
        ShowMode();
    }

    private void BuildGoal()
    {
        var note = WatchesPage.Text(
            "This watch reads GalNet's wording of a launch article, so it can miss or misread an announcement.",
            Fonts.ProseFamily,
            TypeScale.Body,
            ThemeManager.GreyKey);
        note.TextWrapping = TextWrapping.Wrap;

        if (_existing is { } watch)
        {
            _body.Children.Add(Group(
                "Last fired",
                "A change to the name does not let it fire again this tick.",
                Row("Fired for", WatchesPage.Text(WatchFired.Text(watch.FiredForTick, TimeZoneInfo.Local), Fonts.MonoFamily, TypeScale.Body, ThemeManager.WhiteKey))));
        }

        _body.Children.Add(Group("Watch", string.Empty, Row("Name", _name), note, _enabled));
        _body.Children.Add(Footer());
    }

    private Control Footer()
    {
        var cancel = new Button { Content = "CANCEL" };
        cancel.Click += (_, _) => _nav.Back();

        _save.Click += async (_, _) => await SaveAsync();

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        if (_existing is { } watch)
        {
            var delete = new Button { Content = "DELETE" };
            delete.Classes.Add("destructive");
            delete.Click += (_, _) =>
            {
                _desk.Store.Delete(watch.Id);
                _nav.Back();
            };
            footer.Children.Add(delete);
        }

        footer.Children.Add(cancel);
        footer.Children.Add(_save);

        return footer;
    }

    private void ShowMode()
    {
        var rare = _source.SelectedIndex == 0;
        var any = !rare;

        _rareRow.IsVisible = rare;
        _rareCommodity.IsVisible = rare && _rare is not null;
        _systemRow.IsVisible = any;
        _findRow.IsVisible = any;
        _stationRow.IsVisible = any;
        _commodityRow.IsVisible = any;
    }

    private void ShowRare()
    {
        var entries = RareCatalogue.All
            .OrderBy(rare => rare.Name, StringComparer.OrdinalIgnoreCase)
            .Select(rare => (InlinePickerEntry)new InlinePickerOption(rare.Symbol, rare.Name, ThemeManager.WhiteKey, rare.Station))
            .ToList();

        _rarePicker.Show(
            entries,
            _rare?.Symbol ?? string.Empty,
            _rare is null ? "Pick a rare" : _rare.Name,
            _rare is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            _rare is null ? null : $" · {_rare.Station}, {_rare.System}");

        _rareCommodityText.Text = _rare?.Name ?? string.Empty;
    }

    private void ShowStations(IReadOnlyList<string> names)
    {
        var entries = names.Select(name => (InlinePickerEntry)new InlinePickerOption(name, name, ThemeManager.WhiteKey)).ToList();

        if (names.Count == 0)
        {
            entries.Add(new InlinePickerNote("Type a system, then press Find stations."));
        }

        _stationPicker.Show(
            entries,
            _station ?? string.Empty,
            _station ?? "Pick a station",
            _station is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            null);
    }

    private async Task FindStationsAsync()
    {
        var system = _system.Text?.Trim() ?? string.Empty;

        if (system.Length == 0)
        {
            return;
        }

        var names = await _desk.Setup.StationsInAsync(system, CancellationToken.None);

        if (_station is not null && !names.Contains(_station, StringComparer.OrdinalIgnoreCase))
        {
            _station = null;
        }

        ShowStations(names);
    }

    private async Task SaveAsync()
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        _save.IsEnabled = false;

        foreach (var message in _messages.Values)
        {
            message.IsVisible = false;
        }

        try
        {
            if (_stock && _source.SelectedIndex == 0 && _rare is null)
            {
                ShowFailure("Not saved: pick a rare.");

                return;
            }

            var enabled = _enabled.IsChecked == true;
            var name = _name.Text ?? string.Empty;

            var result = _stock
                ? await _desk.Setup.SaveStockAsync(
                    _existing,
                    new StockDraft(
                        name,
                        enabled,
                        _source.SelectedIndex == 0 ? _rare : null,
                        _system.Text ?? string.Empty,
                        _station ?? string.Empty,
                        _commodity.Text ?? string.Empty,
                        _comparison.SelectedIndex == 1 ? StockComparison.Below : StockComparison.Above,
                        _units.Text ?? string.Empty),
                    CancellationToken.None)
                : _desk.Setup.SaveCommunityGoal(_existing, name, enabled);

            if (result.Saved)
            {
                _nav.Back();

                return;
            }

            if (_messages.TryGetValue(result.Field, out var shown))
            {
                shown.Text = result.Message;
                shown.IsVisible = true;
            }
            else
            {
                ShowFailure(result.Message!);
            }
        }
        finally
        {
            _saving = false;
            _save.IsEnabled = true;
        }
    }

    private void ShowFailure(string text)
    {
        if (_failure is not null)
        {
            _body.Children.Remove(_failure);
        }

        _failure = new Notice(NoticeLevel.Error) { Label = "Not saved", Text = text };
        _body.Children.Add(_failure);
    }
}
