using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Modules;

namespace D47.App.Panel;

/// <summary>Draws a parsed module page from d47's own components; every press calls the page's module, as the Commander.</summary>
/// <param name="reload">Reads the page again; called on the UI thread after a press fails, so no control keeps a value the module refused.</param>
internal sealed class ModulePageDrawing(string moduleId, string moduleName, ModulePageServices services, Action<string> openPage, Action reload)
{
    public const string PartName = "ModulePart";
    public const string ButtonName = "ModulePageButton";
    public const string CheckboxName = "ModulePageCheckbox";
    public const string ChoiceName = "ModulePageChoice";
    public const string RowName = "ModulePageRow";
    public const string TableName = "ModulePageTable";
    public const string MessageName = "ModulePageMessage";

    /// <summary>The space between groups.</summary>
    public const double GroupGap = 28;

    /// <summary>The page's groups: each GroupHead opens one, and parts before the first head make a group of their own.</summary>
    public StackPanel Draw(ModulePageContent page)
    {
        var groups = new StackPanel { Spacing = GroupGap };
        StackPanel? group = null;
        var afterHead = false;

        foreach (var part in page.Parts)
        {
            if (part is ModuleGroupHead head)
            {
                group = new StackPanel { Spacing = 2 };
                group.Children.Add(Head(head));
                groups.Children.Add(group);
                afterHead = true;
                continue;
            }

            if (group is null)
            {
                group = new StackPanel { Spacing = 2 };
                groups.Children.Add(group);
            }

            var control = Part(part);
            control.Name ??= PartName;

            if (afterHead)
            {
                control.Margin = new Thickness(0, 10, 0, 0);
                afterHead = false;
            }

            group.Children.Add(control);
        }

        return groups;
    }

    public static string InkOf(ModuleTone? tone, string fallback) => tone switch
    {
        ModuleTone.Value => ThemeManager.AKey,
        ModuleTone.Name => ThemeManager.WhiteKey,
        ModuleTone.Here => ThemeManager.CyanKey,
        ModuleTone.Stored => ThemeManager.YellowKey,
        ModuleTone.Warning => ThemeManager.WarnKey,
        ModuleTone.Error => ThemeManager.RedKey,
        _ => fallback,
    };

    private Control Part(ModulePart part) => part switch
    {
        ModuleDataBlock block => DataBlock(block),
        ModulePageNotice notice => Notice(notice),
        ModuleSettingsRow row => SettingsRow(row),
        ModuleCheckboxTiles tiles => CheckboxTiles(tiles),
        ModuleList list => List(list),
        ModuleTileButtons buttons => TileButtons(buttons),
        ModuleTable table => Table(table),
        ModuleMessage message => Message(message),
        _ => new Border(),
    };

    private static Control Head(ModuleGroupHead head)
    {
        var reset = SettingsView.ResetGlyph("ModulePageReset", head.Title);
        reset.IsVisible = false;

        return SettingsView.GroupHead(head.Title, head.Description, reset).Head;
    }

    private static Control DataBlock(ModuleDataBlock block)
    {
        if (block.Prose is { } prose)
        {
            var text = ModuleKit.Text(prose, Fonts.ProseFamily, 14, InkOf(block.Tone, ThemeManager.WhiteKey));
            var border = new Border { Padding = new Thickness(14, 10), Child = text };
            ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);
            return border;
        }

        var tiles = block.Cells.Select(cell =>
        {
            var value = ModuleKit.Text(cell.Value, Fonts.ChromeFamily, StatTile.ValueSize, InkOf(cell.Tone, ThemeManager.AKey), FontWeight.Medium);
            return (Control)StatTile.Tile(cell.Label, value);
        }).ToList();

        return StatTile.Grid(tiles, maxColumns: 3);
    }

    private static Notice Notice(ModulePageNotice notice) =>
        new(notice.Tone == ModuleTone.Warning ? NoticeLevel.Warning : NoticeLevel.Error, inline: true)
        {
            Label = notice.Label.ToUpperInvariant(),
            Text = notice.Text,
        };

    private Control SettingsRow(ModuleSettingsRow row)
    {
        var control = row.Control switch
        {
            ModuleStatusBlock status => Status(status),
            ModuleChoice choice => Choice(row.Label, choice),
            _ => new Border(),
        };

        return SettingsView.RowFrame(SettingsView.RowColumns(SettingsView.RowLabel(row.Label), control, reset: null), protectedRow: false);
    }

    private Control Status(ModuleStatusBlock status)
    {
        var (inset, text) = SettingsView.Report();
        text.Text = status.Text;

        if (status.Tone is { } tone)
        {
            ThemeInk.Bind(text, TextBlock.ForegroundProperty, InkOf(tone, ThemeManager.WhiteKey));
        }

        if (status.Button is not { } button)
        {
            return inset;
        }

        var tile = Tile(button);
        tile.Margin = new Thickness(2, 0, 0, 0);
        tile.VerticalAlignment = VerticalAlignment.Stretch;

        var dock = new DockPanel();
        DockPanel.SetDock(tile, Dock.Right);
        dock.Children.Add(tile);
        dock.Children.Add(inset);

        return dock;
    }

    private Control Choice(string label, ModuleChoice choice)
    {
        var (view, control) = Controls.Choice.Build([.. choice.Options.Select(o => o.Label)], choice.Selected, alwaysStepper: choice.Stepper);

        view.Name = ChoiceName + "." + choice.Sends.Tool;
        AutomationProperties.SetName(view, label);

        if (!choice.Stepper)
        {
            view.HorizontalAlignment = HorizontalAlignment.Stretch;
            SettingsView.DressAsAChoice(view);
        }

        var sent = choice.Selected;

        control.SelectionChanged += (_, _) =>
        {
            var index = control.SelectedIndex;

            if (index >= 0 && index < choice.Options.Count && index != sent)
            {
                sent = index;
                Send(choice.Sends, choice.Options[index].ValueJson);
            }
        };

        return view;
    }

    private Control CheckboxTiles(ModuleCheckboxTiles tiles)
    {
        var grid = new TileGrid { Columns = tiles.Columns };

        foreach (var tile in tiles.Tiles)
        {
            var (box, text) = LabeledCheckBox.Build(tile.Label);
            text.TextWrapping = TextWrapping.Wrap;
            box.Name = CheckboxName + "." + tile.Sends.Tool;
            box.IsChecked = tile.Checked;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetName(box, tile.Label);

            var sent = tile.Checked;

            box.IsCheckedChanged += (_, _) =>
            {
                var on = box.IsChecked == true;

                if (on != sent)
                {
                    sent = on;
                    Send(tile.Sends, on ? "true" : "false");
                }
            };

            grid.Children.Add(box);
        }

        return grid;
    }

    private Control List(ModuleList list)
    {
        var rows = new StackPanel { Spacing = 2 };

        if (list.Head is { } head)
        {
            var caption = StatTile.Caption(head, ThemeManager.GreyKey);
            caption.Margin = new Thickness(0, 0, 0, 4);
            rows.Children.Add(caption);
        }

        foreach (var row in list.Rows)
        {
            rows.Children.Add(ListRow(row));
        }

        return rows;
    }

    private Control ListRow(ModuleListRow row)
    {
        var main = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { ModuleKit.Text(row.Name, Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey, FontWeight.SemiBold) },
        };

        if (row.Sub is { } sub)
        {
            main.Children.Add(ModuleKit.Text(sub, Fonts.ProseFamily, TypeScale.Secondary, ThemeManager.GreyKey));
        }

        var line = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
        line.Children.Add(main);

        if (row.Aside is { } aside)
        {
            var text = ModuleKit.Text(aside, Fonts.MonoFamily, TypeScale.Control, ThemeManager.AKey);
            text.Margin = new Thickness(12, 0, 0, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            line.Children.Add(text);
        }

        if (row.Opens is not { } page)
        {
            var border = new Border { Name = RowName, MinHeight = 52, Padding = new Thickness(14, 6), Child = line };
            ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);
            return border;
        }

        var button = new Button
        {
            Name = RowName + "." + page,
            MinHeight = 52,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = line,
        };
        AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => openPage(page);

        return button;
    }

    private Control TileButtons(ModuleTileButtons buttons)
    {
        var row = new WrapPanel { ItemSpacing = 2, LineSpacing = 2 };

        foreach (var button in buttons.Buttons)
        {
            row.Children.Add(Tile(button));
        }

        return row;
    }

    private Button Tile(ModuleTileButton spec)
    {
        var button = new Button
        {
            Name = ButtonName + "." + (spec.Calls?.Tool ?? spec.Opens),
            Content = spec.Label,
            MinHeight = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (spec.Destructive)
        {
            button.Classes.Add(SettingsView.DestructiveClass);
        }

        AutomationProperties.SetName(button, spec.Label);

        button.Click += (_, _) =>
        {
            if (spec.Calls is { } call)
            {
                Run(call.Tool, cancellation => ModulePagePress.CallAsync(services.InvokeAsync, moduleId, call, cancellation));
            }
            else if (spec.Opens is { } page)
            {
                openPage(page);
            }
        };

        return button;
    }

    private Control Table(ModuleTable table)
    {
        var columns = table.Columns.Select((column, index) => column.Numeric
            ? SortColumn<IReadOnlyList<ModuleCell>>.Figure(column.Label, row => Figure(row[index].Text), SortDirection.Descending)
            : SortColumn<IReadOnlyList<ModuleCell>>.Text(column.Label, row => row[index].Text)).ToList();

        var head = new SortHeader<IReadOnlyList<ModuleCell>>(TableName, columns);
        var body = new StackPanel { Spacing = 2 };
        Grid.SetIsSharedSizeScope(body, true);

        void Fill()
        {
            body.Children.Clear();

            var header = Line(table, Enumerable.Range(0, columns.Count).Select(i => head.Cell(i, Alignment(table, i))).ToList());
            header.MinHeight = TypeScale.MinimumTarget;
            header.Margin = new Thickness(14, 0);
            body.Children.Add(header);

            foreach (var row in head.Sort(table.Rows))
            {
                var cells = row.Select((cell, i) => (Control)Cell(table.Columns[i], cell, i)).ToList();
                var border = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 6), Child = Line(table, cells) };
                ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.SlabKey);
                body.Children.Add(border);
            }
        }

        head.Changed += Fill;
        Fill();

        var scroll = new ScrollViewer
        {
            Name = TableName,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = body,
        };

        // The table fills the column, and scrolls sideways only when its cells need more than that.
        scroll.SizeChanged += (_, e) => body.MinWidth = e.NewSize.Width;

        return scroll;
    }

    private static HorizontalAlignment Alignment(ModuleTable table, int column) =>
        table.Columns[column].Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    /// <summary>A row of the table: the first column takes the room left, the others share their widths across every row.</summary>
    private static Grid Line(ModuleTable table, IReadOnlyList<Control> cells)
    {
        var grid = new Grid { ColumnSpacing = 16 };

        for (var i = 0; i < table.Columns.Count; i++)
        {
            grid.ColumnDefinitions.Add(i == 0
                ? new ColumnDefinition(GridLength.Star) { MinWidth = 150 }
                : new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "c" + i.ToString(CultureInfo.InvariantCulture) });

            var cell = cells[i];
            cell.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    private static TextBlock Cell(ModuleColumn column, ModuleCell cell, int index)
    {
        var fallback = index == 0 ? ThemeManager.WhiteKey : column.Numeric ? ThemeManager.AKey : ThemeManager.WhiteKey;
        var text = ModuleKit.Text(
            cell.Text,
            column.Numeric ? Fonts.MonoFamily : Fonts.ProseFamily,
            TypeScale.Control,
            InkOf(cell.Tone, fallback));
        text.TextWrapping = TextWrapping.NoWrap;
        text.HorizontalAlignment = column.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        return text;
    }

    /// <summary>The first number in a cell's text, ignoring its grouping commas and anything after it; null when there is none.</summary>
    private static double? Figure(string text)
    {
        var start = 0;

        while (start < text.Length && !char.IsAsciiDigit(text[start]))
        {
            start++;
        }

        var digits = new StringBuilder();

        if (start > 0 && text[start - 1] == '-')
        {
            digits.Append('-');
        }

        for (var i = start; i < text.Length && (char.IsAsciiDigit(text[i]) || text[i] is ',' or '.'); i++)
        {
            if (text[i] != ',')
            {
                digits.Append(text[i]);
            }
        }

        return double.TryParse(digits.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private Control Message(ModuleMessage message)
    {
        var who = ModuleKit.Text(services.SpeakerName(moduleId), Fonts.ChromeFamily, TypeScale.Small, ThemeManager.AKey, FontWeight.Bold);
        var tag = ModuleKit.Text(moduleName, Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.GreyKey);
        var head = new WrapPanel { ItemSpacing = 10, Children = { who, tag } };

        if (message.At is { } at)
        {
            head.Children.Add(ModuleKit.Text(
                at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), Fonts.MonoFamily, TypeScale.Meta, ThemeManager.GreyKey));
        }

        var body = ModuleKit.Text(message.Text, Fonts.ProseFamily, TypeScale.Body, ThemeManager.WhiteKey);
        body.LineHeight = TypeScale.Body * 1.45;

        var turn = new TurnBorder
        {
            Name = MessageName,
            Child = new StackPanel { Spacing = 5, Children = { head, body } },
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 10, 14, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 700,
        };
        ThemeInk.Bind(turn, Border.BorderBrushProperty, ThemeManager.AKey);

        return turn;
    }

    private void Send(ModuleSends sends, string valueJson) =>
        Run(sends.Tool, cancellation => ModulePagePress.SendAsync(services.InvokeAsync, moduleId, sends, valueJson, cancellation));

    /// <summary>Makes the call off the UI thread and logs a failure.</summary>
    private void Run(string tool, Func<CancellationToken, Task<ToolResult>> call) =>
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await call(CancellationToken.None).ConfigureAwait(false);

                if (!result.IsError)
                {
                    return;
                }

                services.Warn(moduleId, $"page press on {tool} failed", result.ShortForm ?? result.Content);
            }
            catch (Exception ex)
            {
                services.Warn(moduleId, $"page press on {tool} failed", ex.Message);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(reload);
        });
}
