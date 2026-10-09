#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using D47.App.Panel;
using D47.Core.Storage;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Controls;

/// <summary>
/// One card per component in <c>design/system/components/</c>, each drawn with the app's own controls.
/// Debug-only — <c>Ctrl+Shift+K</c> opens it from the panel.
/// </summary>
public sealed class ControlKitWindow : Window
{
    /// <summary>A card's name: this prefix and the component's folder name.</summary>
    public const string CardPrefix = "KitCard.";

    private const double ContentMaxWidth = 1120;
    private const double PanelPadding = 32;
    private const double GroupGap = 48;
    private const double CardGap = 16;
    private const double StateGap = 24;
    private const double CappedControlWidth = 400;
    private const double PanelHeight = 560;
    private const double FootHeight = 120;

    private readonly ThemeManager _themeManager = new(Application.Current!, NullLogger<ThemeManager>.Instance);
    private readonly TextBox _field = new() { Text = "Diaguandri" };
    private readonly string _secretsRoot = Path.Combine(Path.GetTempPath(), $"d47-control-kit-{Guid.NewGuid():N}");

    public ControlKitWindow()
    {
        Title = "Control Kit";
        Width = 1180;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BgKey);

        // D47.Tab is scoped to wherever merges PanelTabs.axaml; a desktop-only window carries no merge
        // of it on its own.
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://d47/"))
        {
            Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
        });

        var body = new StackPanel
        {
            Spacing = GroupGap,
            Children =
            {
                Header(),
                Group("Foundations", Palette(), Surfaces(), TypeScaleCard()),
                Group("Layout", GroupHead(), SettingsRow()),
                Group("Navigation", Tab()),
                Group(
                    "Controls",
                    ApiKey(),
                    CheckboxTile(),
                    Dropdown(),
                    GlyphButton(),
                    KeyBinding(),
                    LevelBar(),
                    ListBoxItem(),
                    MixerTable(),
                    NumberStepper(),
                    SearchField(),
                    Segmented(),
                    StepperCard(),
                    TextBoxCard(),
                    TileButton()),
                Group("Data", DataBlock(), Message(), ModalCard(), NoticeCard(), StatusRow()),
                Group("Chrome", Card()),
            },
        };

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                MaxWidth = ContentMaxWidth,
                Padding = new Thickness(PanelPadding),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = body,
            },
        };

        Content = new Grid { Children = { scroller, Scanlines() } };

        Closed += (_, _) =>
        {
            try
            {
                Directory.Delete(_secretsRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _field.Focus();
        _field.CaretIndex = _field.Text?.Length ?? 0;
    }

    /// <summary>The panel's scanline layer over the whole window, drawn only on a theme that glows.</summary>
    private Border Scanlines()
    {
        var scanlines = new Border { Opacity = ThemeManager.ScanlinesOpacity, IsHitTestVisible = false };
        RenderOptions.SetBitmapInterpolationMode(scanlines, BitmapInterpolationMode.None);

        void Show() => scanlines.Background =
            this.TryFindResource(ThemeManager.ScanlinesKey, out var brush) && brush is not null
                ? ThemeManager.Scanlines(RenderScaling)
                : null;

        scanlines.GetResourceObservable(ThemeManager.ScanlinesKey)
            .Subscribe(new Avalonia.Reactive.AnonymousObserver<object?>(_ => Show()));
        ScalingChanged += (_, _) => Show();

        return scanlines;
    }

    // -- Header and the theme --

    private Control Header()
    {
        var intro = Prose(
            "One card for each component in the design system, drawn with the controls the app uses.",
            TypeScale.Body,
            ThemeManager.GreyKey);
        intro.Margin = new Thickness(0, 8, 0, 24);

        return new StackPanel { Children = { TitleText.Screen("CONTROL KIT"), intro, ThemeTools() } };
    }

    private Control ThemeTools()
    {
        var picker = new Segment
        {
            ItemsSource = [.. ThemeCatalog.All.Select(t => t.Name)],
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex >= 0)
            {
                _themeManager.Apply(ThemeCatalog.All[picker.SelectedIndex].Id);
            }
        };

        var accentBox = new TextBox
        {
            Width = 160,
            PlaceholderText = "#RRGGBB",
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        var apply = new Button
        {
            Content = "APPLY",
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        void ApplyAccent()
        {
            if (Color.TryParse(accentBox.Text, out var target))
            {
                _themeManager.Apply(ThemeCatalog.ElitePaletteId, MatrixFor(target));
            }
        }

        apply.Click += (_, _) => ApplyAccent();
        accentBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyAccent();
            }
        };

        var accentRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { accentBox, apply },
        };

        return States(("theme", picker), ("accent, as if a HUD matrix supplied it", accentRow));
    }

    /// <summary>
    /// A diagonal matrix that scales Elite's own <see cref="Theming.Palette.A"/> to <paramref name="target"/>
    /// one channel at a time, the shape a Commander's HUD colour matrix takes, so an accent typed here goes
    /// through <see cref="ThemeManager.Apply"/>'s matrix path.
    /// </summary>
    private static GuiColourMatrix MatrixFor(Color target)
    {
        var source = Palettes.Elite.A;

        double Scale(byte from, byte to) => from == 0 ? 0 : to / (double)from;

        return new GuiColourMatrix(
            Scale(source.R, target.R), 0, 0,
            0, Scale(source.G, target.G), 0,
            0, 0, Scale(source.B, target.B));
    }

    // -- Foundations --

    private static Control Palette()
    {
        var ramp = new WrapPanel { ItemSpacing = 20, LineSpacing = 20 };

        foreach (var key in ThemeManager.Tokens)
        {
            var box = new Border { Width = 72, Height = 72, BorderThickness = new Thickness(1) };
            Themed(box, Border.BackgroundProperty, key);
            Themed(box, Border.BorderBrushProperty, ThemeManager.Line2Key);

            ramp.Children.Add(new StackPanel { Spacing = 6, Children = { box, Caption(key["D47.".Length..].ToLowerInvariant()) } });
        }

        return Card("Palette", "One palette per theme. Colour carries relationship and state, never decoration.", ramp);
    }

    private static Control Surfaces()
    {
        var block = TitleText.Block(
            TitleText.Build("Sacred Fire", TypeScale.Title, TitleRank.Screen),
            TitleText.Context("Fleet carrier · BNH-T2F"),
            TitleText.Figure("Carrier balance", "990,302,661 CR"));

        var head = TitleText.GroupRow(TitleText.Build("Unlock prerequisites", TypeScale.Section, TitleRank.Group));

        var ground = new Border { Padding = new Thickness(20), Child = new StackPanel { Spacing = 24, Children = { block, head } } };
        Themed(ground, Border.BackgroundProperty, ThemeManager.BgKey);

        return Card(
            "Surfaces",
            "The page ground, a screen's title block and a section head on its rule. Scanlines lie over this whole window on a theme that has them.",
            States(("title block and section head", ground)));
    }

    private static Control TypeScaleCard()
    {
        var mono = new TextBlock
        {
            Text = "$0.0412 · 19:42 · CTRL+ALT+X",
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Body,
        };
        Themed(mono, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return Card(
            "TypeScale",
            "Chrome, headings and tiles in Saira capitals; prose in Sintony; numbers, keys, times and costs in mono.",
            States(
                ("screen title", TitleText.Build("Screen title", TypeScale.Title, TitleRank.Screen)),
                ("group", TitleText.Build("Group", TypeScale.Section, TitleRank.Group)),
                ("subgroup", TitleText.Build("Subgroup", TypeScale.Caption, TitleRank.Subgroup)),
                ("row label", TitleText.Build("Row label, sentence case", TypeScale.Body, TitleRank.Row, sentence: true)),
                ("body", Prose("Functioning within tolerance, Commander.", TypeScale.Body, ThemeManager.WhiteKey)),
                ("mono", mono)));
    }

    // -- Layout --

    private static Control GroupHead()
    {
        var legend = Prose(SettingsView.ProtectedLegend, TypeScale.Secondary, ThemeManager.GreyKey);

        var (changed, _, _) = SettingsView.GroupHead(
            "Microphone",
            "The input device, and how D47 knows you're talking to it.",
            SettingsView.ResetGlyph("KitGroupReset", "Reset Microphone"));

        var resetless = SettingsView.ResetGlyph("KitGroupResetHidden", "Reset Corrections");
        resetless.IsVisible = false;
        var (untouched, _, _) = SettingsView.GroupHead("Corrections", "Names D47 has learned to hear correctly.", resetless);

        return Card(
            "GroupHead",
            "A group's name, one line saying what it holds, and the group reset, on an accent rule. The page legend sits above the groups.",
            States(("page legend", legend), ("with a reset", changed), ("nothing to reset", untouched)));
    }

    private static Control SettingsRow()
    {
        var (dropdown, value, _) = SettingsView.DropdownTile("Microphone");
        value.Text = "System default · Logi 4K Stream Edition";
        Themed(value, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        dropdown.Width = Stepper.MaximumWidth;

        var reset = SettingsView.ResetGlyph("KitRowReset", "Reset Push-to-talk");

        var rows = new StackPanel
        {
            Children =
            {
                Row("Microphone", dropdown),
                Row("Push-to-talk", Binding("BUTTON 11"), reset, protectedRow: true),
                Row("Capture before the key", new Amount { Value = 500, Unit = "ms" }),
            },
        };

        return Card(
            "SettingsRow",
            "Label, control and a reserved reset column. A protected row draws its bar in the accent; the reset shows only once the value has changed.",
            Capped(rows, SettingsView.RowsMaxWidth));
    }

    private static Control Row(string label, Control control, Button? reset = null, bool protectedRow = false)
    {
        var caption = SettingsView.RowLabel(label);
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Center;

        return SettingsView.RowFrame(SettingsView.RowColumns(caption, control, reset), protectedRow);
    }

    // -- Navigation --

    private Control Tab()
    {
        var tabTheme = this.TryFindResource("D47.Tab", out var resource) ? resource as ControlTheme : null;
        var group = $"D47KitTabs{Guid.NewGuid():N}";
        var level1 = new WrapPanel { LineSpacing = Segment.Gap };

        var tabs = new[] { "SELECTED", "REST", "HOVER", "FOCUS" };
        for (var i = 0; i < tabs.Length; i++)
        {
            var tab = new RadioButton
            {
                Theme = tabTheme,
                GroupName = group,
                Content = tabs[i],
                IsChecked = i == 0,
            };

            level1.Children.Add(i switch
            {
                2 => Hovered(tab),
                3 => Focused(tab),
                _ => tab,
            });
        }

        var level1Strip = new Border { BorderThickness = new Thickness(0, 0, 0, 2), Child = level1 };
        Themed(level1Strip, Border.BorderBrushProperty, ThemeManager.AKey);

        var level2 = new TextChoice { ItemsSource = ["Selected", "Rest", "Hover", "Focus", "Disabled"], SelectedIndex = 0 };
        var level2Items = level2.GetLogicalDescendants().OfType<RadioButton>().ToList();
        Hovered(level2Items[2]);
        Focused(level2Items[3]);
        Disabled(level2Items[4]);

        return Card(
            "Tab",
            "Tabs are tiles on an accent rule; only the selected one glows. Sub-tabs are text, the selected one underlined.",
            States(("tabs", level1Strip), ("sub-tabs", level2)));
    }

    // -- Controls --

    /// <summary>A key editor over a secret store of its own in a temporary folder, holding a key that is not real.</summary>
    private Control ApiKey()
    {
        const string SecretName = "kit.example";

        var paths = new AppPaths(_secretsRoot);
        paths.EnsureCreated();

        var secrets = new SecretStore(paths, new DpapiSecretProtector(), new DiskFileSystem(), NullLogger<SecretStore>.Instance);
        secrets.Set(SecretName, "kit-not-a-real-key-a91C");

        var settings = new SettingsService(
            new SettingsStore(paths, new DiskFileSystem(), NullLogger<SettingsStore>.Instance),
            secrets,
            new D47Settings(),
            NullLogger<SettingsService>.Instance);

        SettingRow Key(string secretName) => new()
        {
            Key = $"kit.{secretName}",
            Label = "Example key",
            Help = "A key held only by this window.",
            Kind = SettingKind.Secret,
            SecretName = secretName,
            Verify = _ => Task.FromResult(SecretCheck.Works("The example key is accepted.")),
        };

        return Card(
            "ApiKey",
            "A stored key is a masked block with REPLACE and VERIFY; the field appears only after REPLACE, or when no key is stored.",
            States(
                ("stored", new SecretEditor(Key(SecretName), settings)),
                ("missing", new SecretEditor(Key("kit.missing"), settings))));
    }

    private static Control CheckboxTile()
    {
        static CheckBox Tile(string label, bool isChecked = false, bool enabled = true)
        {
            var (box, text) = LabeledCheckBox.Build(label);
            text.TextWrapping = TextWrapping.Wrap;
            box.IsChecked = isChecked;
            box.IsEnabled = enabled;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            return box;
        }

        var two = new TileGrid
        {
            Columns = 2,
            Children =
            {
                Tile("Cancel D47's own voice out of the mic", isChecked: true),
                Tile("Take the room out of what D47 hears"),
            },
        };

        var four = new TileGrid
        {
            Columns = 4,
            Children =
            {
                Tile("Cylon"),
                Tile("Pitch down"),
                Tile("Chorus", isChecked: true),
                Tile("Reverb", enabled: false),
            },
        };

        var hidden = LabeledCheckBox.Caps("Hide the Colonia eight");
        hidden.IsChecked = true;

        var caps = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Segment.Gap,
            Children = { hidden, LabeledCheckBox.Caps("Show every setting") },
        };

        return Card(
            "CheckboxTile",
            "Every two-state control: a square box inside a tile the whole of which is the target, grouped in a grid.",
            StatesOf(("grid of 2", two), ("grid of 4, one disabled", four), ("capitals (filters)", caps)),
            stacked: true);
    }

    private static Control Dropdown()
    {
        var (tile, value, status) = SettingsView.DropdownTile("Voice");
        value.Text = "Rachel · ElevenLabs";
        Themed(value, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        status.Text = "PAID · KEY STORED";
        status.IsVisible = true;
        status.Bind(TextBlock.ForegroundProperty, Stepper.Ink(ChoiceTone.Yellow));
        tile.Width = Stepper.MaximumWidth;

        var (unset, unsetValue, _) = SettingsView.DropdownTile("Output device");
        unsetValue.Text = "(System default)";
        Themed(unsetValue, TextBlock.ForegroundProperty, ThemeManager.Grey2Key);
        unset.Width = Stepper.MaximumWidth;

        return Card(
            "Dropdown",
            "For long lists whose length varies: audio devices and voices. A tile that opens a list of choices.",
            States(("chosen, with status", tile), ("not set", unset)));
    }

    private static Control GlyphButton()
    {
        static Button Copy() => CopyGlyph.For("Diaguandri", _ => Task.FromResult(true));

        return Card(
            "GlyphButton",
            "A glyph tile in a full-size target. On hover or keyboard focus the tile fills and names itself beside it.",
            States(
                ("copy — hover it for its label", Copy()),
                ("pressed", Pressed(Copy())),
                ("reset", SettingsView.ResetGlyph("KitReset", "Reset to default")),
                ("disabled", Disabled(Copy()))));
    }

    private static Control KeyBinding() => Card(
        "KeyBinding",
        "The bound key in a mono chip, then BIND and CLEAR.",
        States(("bound", Binding("BUTTON 11")), ("unbound", Binding(null))));

    private static Control Binding(string? key)
    {
        var (chip, text) = SettingsView.BindingChip(key ?? "NONE");

        if (key is null)
        {
            Themed(text, TextBlock.ForegroundProperty, ThemeManager.Grey2Key);
        }

        var wrap = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
        wrap.Children.Add(chip);
        wrap.Children.Add(new Button { Content = "BIND" });
        wrap.Children.Add(new Button { Content = "CLEAR", IsEnabled = key is not null });
        return wrap;
    }

    private static Control LevelBar() => Card(
        "LevelBar",
        "A level as clickable segments with its value beside it. Segments under the minimum are dim; a muted channel draws its level in grey.",
        StatesOf(
            ("level", Capped(new Level { Minimum = 0.1, Value = 0.85 })),
            ("muted", Capped(new Level { Value = 1, Muted = true }))),
        stacked: true);

    /// <summary>The row states are shown by setting the pseudo-class, so a pointer passing over one clears it.</summary>
    private static Control ListBoxItem()
    {
        var rows = new StackPanel
        {
            Spacing = Segment.Gap,
            Children =
            {
                ListRow.Head("Systems"),
                KitRow("At rest", "Muang · 12.4 ly"),
                Hovered(KitRow("Hover", "Giryak · 30.1 ly")),
                KitRow("Selected", "Leesti · 44.0 ly", selected: true),
                Disabled(KitRow("Disabled", "Colonia · 22,000 ly")),
            },
        };

        var list = new ListBox
        {
            ItemsSource = new[] { "Diaguandri", "Shinrarta Dezhra", "Colonia" },
            SelectedIndex = 1,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        return Card(
            "ListBoxItem",
            "Rows are filled tiles apart from one another, under a list head. Selection is a solid fill.",
            States(("rows under a list head", Capped(rows)), ("a ListBox", Capped(list))));
    }

    private static Border KitRow(string name, string secondary, bool selected = false)
    {
        var nameText = ListRow.Name(new TextBlock { Text = name });
        var secondaryText = ListRow.Sub(new TextBlock { Text = secondary });

        return ListRow.Dress(
            new Border
            {
                Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameText, secondaryText } },
            },
            selected);
    }

    private static Control MixerTable()
    {
        string[] columns = ["Level", "Mute", "Duck others"];
        bool[] checks = [false, true, false];

        var headings = columns.Select((label, c) => new SettingRow
        {
            Key = $"kit.mixer.{c}",
            Label = label,
            Help = string.Empty,
            Kind = checks[c] ? SettingKind.Toggle : SettingKind.Number,
        }).ToList();

        static CheckBox Mute(bool muted) => new() { IsChecked = muted, Classes = { "bare" } };

        Grid Channel(string name, Control?[] cells, bool changed)
        {
            var reset = SettingsView.ResetGlyph(SettingsView.RowResetName, $"Reset {name}");
            reset.IsVisible = changed;
            return SettingsView.MixerChannelRow(name, null, columns, cells, checks, new StatusLine(), reset).Row;
        }

        var table = new StackPanel
        {
            Name = SettingsView.MixerName,
            Spacing = Gaps.Tile,
            Children =
            {
                SettingsView.BuildMixerHeader(headings, columns, checks),
                Channel("D47", [new Level { Value = 1 }, Mute(false), new Level { Value = 0.35 }], changed: false),
                Channel("Game audio", [new Level { Value = 0.5, Muted = true }, Mute(true), new Level { Value = 1 }], changed: true),
                Channel("Cues", [new Level { Value = 1 }, Mute(false), null], changed: false),
            },
        };

        return Card(
            "MixerTable",
            "One row per channel: its name, a cell per setting, and the reset column. A dash where a channel has no such setting.",
            Capped(table, SettingsView.RowsMaxWidth));
    }

    private static Control NumberStepper()
    {
        var price = new Amount { Value = 0.05m, Step = 0.01m, Format = "0.00", Unit = "$" };
        Pressed(price.GetLogicalDescendants().OfType<RepeatButton>().Last());

        return Card(
            "NumberStepper",
            "A number with its unit and arrows either side; click the value to type one.",
            States(("milliseconds", new Amount { Value = 500, Unit = "ms" }), ("price, arrow pressed", price)));
    }

    private static Control SearchField()
    {
        static TextBox Search(string? text = null)
        {
            var box = new TextBox
            {
                Width = PanelView.TranscriptSearchWidth,
                PlaceholderText = "Search this page",
                Text = text,
            };
            box.Classes.Add(FieldMessage.SearchClass);

            if (text is not null)
            {
                var count = new TextBlock
                {
                    Text = "2 of 5",
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily)Application.Current!.FindResource("D47.Font.Mono")!,
                    FontSize = TypeScale.Small,
                };
                Themed(count, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

                var clear = new Button
                {
                    Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
                    MinHeight = 0,
                    Content = Glyphs.Text("✕", TypeScale.Glyph),
                };
                Avalonia.Automation.AutomationProperties.SetName(clear, "Clear the search");

                box.InnerRightContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { count, clear },
                };
            }

            return box;
        }

        return Card(
            "SearchField",
            "A search field that turns to the accent on focus, and never outweighs the message input.",
            States(("rest", Search()), ("with value", Search("Giryak"))));
    }

    private static Control Segmented()
    {
        string[] options = ["NONE", "EDGE", "ELEVENLABS", "OPENAI"];

        var withStatus = new Segment
        {
            ItemsSource = options,
            SelectedIndex = 2,
            Statuses =
            [
                null,
                new ChoiceStatus("FREE", ChoiceTone.Grey),
                new ChoiceStatus("PAID · KEY STORED", ChoiceTone.Yellow),
                new ChoiceStatus("PAID · NO KEY", ChoiceTone.Grey),
            ],
        };

        var hover = new Segment { ItemsSource = ["NEAR", "SESSION", "ANYWHERE"], SelectedIndex = 1 };
        Hovered(((Avalonia.Controls.Panel)hover.Content!).Children[0]);

        var longLabels = new Segment
        {
            ItemsSource = ["Press to talk (PTT)", "Toggle on and off", "Listen whenever I speak", "Listen when I say its name"],
            SelectedIndex = 0,
        };

        return Card(
            "Segmented",
            $"For {Choice.SegmentLimit} or fewer options: equal tiles, the chosen one filled. A status goes on a second line.",
            StatesOf(
                ("four, with status", withStatus),
                ("first option hovered", hover),
                ("long labels", longLabels),
                ("disabled", Disabled(new Segment { ItemsSource = ["NEAR", "SESSION", "ANYWHERE"], SelectedIndex = 1 }))),
            stacked: true);
    }

    private static Control StepperCard()
    {
        string[] models = ["Tiny", "Small", "Medium (English only)", "Medium", "Large", "Large (turbo)"];

        var hover = new Stepper { ItemsSource = models, SelectedIndex = 2 };
        Hovered(hover.GetLogicalDescendants().OfType<RepeatButton>().Last());

        return Card(
            "Stepper",
            "For a handful of fixed options: arrows either side of the value, its position counted, its consequence under it.",
            StatesOf(
                ("with a consequence", Capped(new Stepper
                {
                    ItemsSource = models,
                    Consequences = [null, null, "1.5 GB · runs on the GPU", null, null, null],
                    SelectedIndex = 2,
                })),
                ("right arrow hovered", Capped(hover)),
                ("disabled", Capped(Disabled(new Stepper { ItemsSource = models, SelectedIndex = 2 })))),
            stacked: true);
    }

    private Control TextBoxCard()
    {
        var error = new TextBox { Text = "sk-12" };
        FieldMessage.ShowError(error, "That key was rejected. Paste it again.");

        var warning = new TextBox { Text = "Jameson Memorial" };
        FieldMessage.ShowWarning(warning, "This name isn't in the journal yet. D47 will learn it.");

        return Card(
            "TextBox",
            "An outlined field with a block caret; the outline turns cyan on focus. A message under it says what is wrong.",
            StatesOf(
                ("rest", Capped(new TextBox { PlaceholderText = "Commander name" })),
                ("focus", Capped(_field)),
                ("error", Capped(error)),
                ("warning", Capped(warning)),
                ("disabled", Capped(Disabled(new TextBox { PlaceholderText = "Not available" })))),
            stacked: true);
    }

    /// <summary>Hover, press and focus are shown by setting the pseudo-class.</summary>
    private static Control TileButton()
    {
        static Button Tile(string text, string? weight = null)
        {
            var button = new Button { Content = text };

            if (weight is not null)
            {
                button.Classes.Add(weight);
            }

            return button;
        }

        const string Destructive = SettingsView.DestructiveClass;

        return Card(
            "TileButton",
            "Every button is a flat tile, default or destructive. SEND is a default tile.",
            States(
                ("rest", Tile("Add to checklist")),
                ("hover", Hovered(Tile("Add to checklist"))),
                ("pressed", Pressed(Tile("Add to checklist"))),
                ("focus", Focused(Tile("Add to checklist"))),
                ("disabled", Disabled(Tile("Add to checklist"))),
                ("destructive", Tile("Forget them all", Destructive)),
                ("destructive hover", Hovered(Tile("Forget them all", Destructive))),
                ("send", Tile("SEND"))));
    }

    // -- Data --

    private static Control DataBlock()
    {
        var grid = StatTile.Grid(
            [
                StatTile.Build("Current system", "Giryak", StatInk.Here),
                StatTile.Build("Tritium in tank", "967 t"),
                StatTile.Build("Where you stand", "Known · no invitation", StatInk.Name),
                StatTile.Build("Carrier", "Nautilus Deep", StatInk.Name),
                StatTile.Build("Range", "500 ly"),
                StatTile.Build("Docked at", "Ray Gateway", StatInk.Name),
            ]);
        grid.Name = "KitStatGrid";

        var gauges = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Gauge.Build("Delivered", "1,860 / 3,000 t · 62%", 0.62),
                Gauge.Build("Capacity", "1,110 / 25,000 t", 1110.0 / 25000, GaugeFill.Capacity),
                Gauge.Build("Power", "25.04 / 22.93 MW · 109%", 1.0, GaugeFill.Over),
            },
        };

        var ladder = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                EngineersPages.CriterionLine(new UnlockCriterion("Visit the engineer's base", Met: true)),
                EngineersPages.CriterionLine(new UnlockCriterion("Reach Friendly with the Pilots Federation", Met: false)
                {
                    Reading = "Cordial, 40% of the way",
                    Measure = new UnlockMeasure(40, 100, IsCeiling: false),
                }),
                EngineersPages.CriterionLine(new UnlockCriterion("Hold an invitation", Met: null)),
                EngineersPages.CriterionLine(new UnlockCriterion("Provide 25 units of Modular Terminals", Met: false)),
            },
        };

        return Card(
            "DataBlock",
            "Read-only data on a neutral slab. A value in the accent; a name in white; where you are in cyan. Capacity gauges are yellow; the prerequisite ladder reads met, in progress, unknown and not met.",
            StatesOf(("stat grid", grid), ("gauges", Capped(gauges)), ("prerequisite ladder", ladder)),
            stacked: true);
    }

    /// <summary>The panel itself, drawn over a model holding a short exchange.</summary>
    private static Control Message()
    {
        var model = new PanelViewModel();
        var at = new DateTimeOffset(2026, 9, 26, 19, 41, 0, TimeSpan.Zero);
        model.Append("Good evening, Commander. Ready to go.", time: at);
        model.Append("How are you this evening?", voice: TranscriptVoice.Commander, time: at);
        model.Append(
            "Functioning within tolerance, Commander. Docked at Sacred Fire, Giryak, systems quiet.",
            time: at.AddMinutes(1));

        var panel = new PanelView { DataContext = model, Height = PanelHeight };

        return Card(
            "Message",
            "The transcript: D47 on the left with an accent bar, the Commander on the right in cyan.",
            panel);
    }

    /// <summary>The modal as a dialog drawn over the panel shows it: the scrim, and the modal inside its frame.</summary>
    private static Control ModalCard()
    {
        var question = Prose(
            "Its loadout is removed from the logbook. The ship itself is not touched.",
            TypeScale.Body,
            ThemeManager.WhiteKey);

        var figure = new TextBlock
        {
            Text = "$0.0412",
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Subheading,
        };
        Themed(figure, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var modal = Modal.Build(
            "Confirm",
            "Forget this ship?",
            question,
            [new Button { Content = "Keep", MinWidth = 110 }, new Button { Content = "Forget", MinWidth = 110 }],
            figure);

        var frame = new Border
        {
            MaxWidth = 520,
            BorderThickness = new Thickness(1),
            Child = modal,
        };
        Themed(frame, Border.BorderBrushProperty, ThemeManager.AKey);

        var scrim = new Border { Name = "KitModal", Padding = new Thickness(24, 40), Child = frame };
        Themed(scrim, Border.BackgroundProperty, ThemeManager.ScrimKey);

        var ground = new Border { Child = scrim };
        Themed(ground, Border.BackgroundProperty, ThemeManager.SlabKey);

        return Card(
            "Modal",
            "A framed dialog over a dark scrim: context, title, a key figure at the right, a scrolling body, and the buttons on a rule. Esc closes it.",
            ground);
    }

    private static Control NoticeCard()
    {
        var retry = new Button { Content = "Retry" };

        return Card(
            "Notice",
            "Red when something failed or is blocked; amber for a caution the Commander can act on. One notice per problem.",
            StatesOf(
                ("error, with an action", new Notice
                {
                    Label = "Speech engine offline",
                    Text = "Groq rejected the key. D47 is using Whisper on this computer until you replace it.",
                    Detail = "HTTP 401 · Groq",
                    Actions = { retry },
                }),
                ("warning", new Notice(NoticeLevel.Warning)
                {
                    Label = "Journal not found",
                    Text = "D47 can't see the game yet. Start Elite, or point D47 at the journal folder.",
                }),
                ("inline", new Notice(inline: true) { Text = "Inline, for use inside a row or under a control." })),
            stacked: true);
    }

    /// <summary>The foot of the panel itself: the microphone line and the session's spend over the message input.</summary>
    private static Control StatusRow()
    {
        var model = new PanelViewModel
        {
            Microphone = D47.Core.Listening.MicrophoneState.Idle,
            MicrophoneDetail = "Hold BUTTON 11 to talk.",
        };

        var panel = new PanelView { DataContext = model };
        panel.EnableTurnDetails(() => Task.CompletedTask, () => 0.1432m);

        return Card(
            "StatusRow",
            "Above the message input: the microphone's state, the session's spend and SPEND at the far right.",
            PanelFoot(panel));
    }

    /// <summary>A panel cut to its foot.</summary>
    private static Control PanelFoot(PanelView panel)
    {
        panel.Height = PanelHeight;
        panel.VerticalAlignment = VerticalAlignment.Bottom;

        var slice = new Border { Height = FootHeight, ClipToBounds = true, Child = panel };
        Themed(slice, Border.BackgroundProperty, ThemeManager.BgKey);

        return slice;
    }

    // -- Chrome --

    private static Control Card()
    {
        static Control Ship(string name, string hull, string where, bool selected) =>
            LoadoutPages.Card(
                $"{name} ({hull})", $"{hull}\n{where}", marked: false, () => { }, LoadoutStanding.Owned, showing: selected, headline: name);

        var cards = Reflow.Grid(
            [
                Stated("rest", Ship("Campaigner", "Panther Clipper MkII", "Muang · docked", selected: false)),
                Stated("hover", Hovered(Ship("Wanderer", "Mandalay", "Stored · Giryak", selected: false))),
                Stated("selected", Ship("Hauler", "Type-9 Heavy", "Stored · Leesti", selected: true)),
            ],
            200,
            StateGap,
            StateGap,
            maxColumns: 3);

        return Card("Card", "A selectable tile for a ship or carrier. Filled, not outlined; selection is a solid fill.", cards);
    }

    // -- Shared pieces --

    private static Control Group(string heading, params Control[] cards) => new StackPanel
    {
        Spacing = CardGap,
        Children = { TitleText.GroupRow(TitleText.Build(heading, TypeScale.Section, TitleRank.Group)), Stack(CardGap, cards) },
    };

    /// <summary>A component's card: its design name, what it is for, and the component drawn beneath.</summary>
    private static Control Card(string component, string summary, Control content)
    {
        var name = TitleText.Build(component, TypeScale.Secondary, TitleRank.Subgroup, sentence: true);

        var about = Prose(summary, TypeScale.Tip, ThemeManager.GreyKey);
        about.Margin = new Thickness(0, 4, 0, 16);

        var card = new Border
        {
            Name = CardPrefix + component,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 16),
            Child = new StackPanel { Children = { name, about, content } },
        };
        Themed(card, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return card;
    }

    private static Control Card(string component, string summary, IEnumerable<Control> states, bool stacked) =>
        Card(component, summary, stacked ? Stack(StateGap, states) : Across(states));

    private static StackPanel Stack(double spacing, IEnumerable<Control> children)
    {
        var stack = new StackPanel { Spacing = spacing };
        stack.Children.AddRange(children);
        return stack;
    }

    /// <summary>Each state under its caption, side by side and wrapping.</summary>
    private static Control States(params (string Caption, Control Control)[] states) =>
        Across(states.Select(state => Stated(state.Caption, state.Control)));

    private static IEnumerable<Control> StatesOf(params (string Caption, Control Control)[] states) =>
        states.Select(state => Stated(state.Caption, state.Control));

    private static Control Across(IEnumerable<Control> states)
    {
        var wrap = new WrapPanel { ItemSpacing = StateGap, LineSpacing = StateGap };
        wrap.Children.AddRange(states);
        return wrap;
    }

    private static Control Stated(string caption, Control control) =>
        new StackPanel { Spacing = 6, Children = { Caption(caption), control } };

    /// <summary>A mono caption naming a state, in lower case as the design writes it.</summary>
    private static TextBlock Caption(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.Grey2Key);
        return label;
    }

    private static TextBlock Prose(string? text, double size, string inkKey)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, inkKey);
        return block;
    }

    /// <summary>Fills the column up to <paramref name="width"/>, left-aligned.</summary>
    private static Control Capped(Control control, double width = CappedControlWidth) => new Grid
    {
        ColumnDefinitions = [new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = width }],
        MinWidth = Math.Min(width, CappedControlWidth),
        Children = { control },
    };

    private static T Hovered<T>(T control) where T : Control
    {
        ((IPseudoClasses)control.Classes).Set(":pointerover", true);
        return control;
    }

    private static T Focused<T>(T control) where T : Control
    {
        ((IPseudoClasses)control.Classes).Set(":focus-visible", true);
        return control;
    }

    /// <summary>Set once the button is in the tree, since a button clears <c>:pressed</c> as it is attached.</summary>
    private static T Pressed<T>(T control) where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ((IPseudoClasses)control.Classes).Set(":pressed", true));
        return control;
    }

    private static T Disabled<T>(T control) where T : Control
    {
        control.IsEnabled = false;
        return control;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
#endif
