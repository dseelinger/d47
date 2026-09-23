#if DEBUG
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Controls;

/// <summary>
/// The control system handoff's sign-off gate (#358), laid out as the reference's Control Kit view
/// (<c>docs/spikes/reference/D47 Panel v2.dc.html</c>, #374). Every control is drawn from the real
/// control themes and resource keys rather than copies. Debug-only — <c>Ctrl+Shift+K</c> opens it
/// from the panel.
/// </summary>
public sealed class ControlKitWindow : Window
{
    private const double ContentMaxWidth = 1120;
    private const double PanelPadding = 32;
    private const double SectionGap = 48;
    private const double HeadingGap = 24;
    private const double GridGap = 32;
    private const double GridMinColumn = 310;
    private const double CardGap = 12;
    private const double CardMinColumn = 280;
    private const double BarGap = 2;
    private const double BarMinWidth = 150;
    private const double CappedControlWidth = 400;

    private readonly ThemeManager _themeManager = new(Application.Current!, NullLogger<ThemeManager>.Instance);
    private readonly TextBox _field = new() { Text = "Diaguandri" };

    public ControlKitWindow()
    {
        Title = "Control Kit";
        Width = 1180;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        // D47.Tab is scoped to wherever merges PanelTabs.axaml; a desktop-only window carries no merge
        // of it on its own.
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://d47/"))
        {
            Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
        });

        var body = new StackPanel
        {
            Spacing = SectionGap,
            Children =
            {
                Header(),
                ControlsSection(),
                ChoosingSection(),
                ThemeSection(),
                StatusSection(),
                RampSection(),
                HeadingRanksSection(),
                SettingsRowsSection(),
                NavigationSection(),
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

    // -- Header --

    private static Control Header()
    {
        var intro = Prose(
            "Every control says what it is and what it is set to, without being read twice. Reverse video "
                + "carries state, not outlines — a filled block is the terminal idiom and it is the thing that "
                + "still reads at arm's length in a headset.",
            TypeScale.Body,
            ThemeManager.TextMutedKey);
        intro.MaxWidth = 640;
        intro.Margin = new Thickness(0, 8, 0, 0);

        var cards = Reflow(
            [
                SpecCard("SPACING", "4 · 8 · 12 · 16 · 24 · 32 · 48. Nothing else."),
                SpecCard("ROW", "52px settings · 44px minimum target"),
                SpecCard("COLUMNS", "Label 300px · control next to it · reset gutter fixed"),
            ],
            CardMinColumn,
            CardGap,
            CardGap,
            maxColumns: 3);
        cards.Margin = new Thickness(0, 32, 0, 0);

        return new StackPanel { Children = { TitleText.Screen("CONTROL KIT"), intro, cards } };
    }

    private static Control SpecCard(string caption, string body)
    {
        var label = Caption(caption);
        label.Margin = new Thickness(0, 0, 0, 6);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16),
            Child = new StackPanel { Children = { label, Prose(body, TypeScale.Secondary, ThemeManager.TextKey) } },
        };
        Themed(card, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return card;
    }

    // -- Telling things apart --

    private Control ControlsSection()
    {
        var (report, reportText) = SettingsView.Report();
        reportText.Text = "11 ships, the oldest last seen about a day ago.";

        var (sentence, _) = LabeledCheckBox.Build("Include journal history", labelFirst: false);
        var (disabled, _) = LabeledCheckBox.Build("Unavailable here", labelFirst: false);
        disabled.IsChecked = true;
        disabled.IsEnabled = false;

        var checkboxes = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new CheckBox { Content = "Raw", IsChecked = true, HorizontalAlignment = HorizontalAlignment.Left },
                new CheckBox { Content = "Keyboard", IsChecked = false, HorizontalAlignment = HorizontalAlignment.Left },
                sentence,
                disabled,
                new CheckBox { IsChecked = true, Classes = { "bare" }, HorizontalAlignment = HorizontalAlignment.Left },
            },
        };
        sentence.HorizontalAlignment = HorizontalAlignment.Left;
        disabled.HorizontalAlignment = HorizontalAlignment.Left;

        var cells = new Control[]
        {
            Cell("REPORT — READ ONLY", report, Note("No box at all. A box is a promise you can type in it.")),
            Cell("FIELD — EDITABLE", _field, Note("Inset ground, one lit edge, block caret.")),
            Cell("ACTIONS — EVERY STATE", Actions(), Note("Every class draws the same tile. Focus fills it; delete is red.")),
            Cell("CHECKBOX — TWO STATE", checkboxes, Note("A name in capitals, a sentence in prose. The row is the target; hover or focus lights it.")),
            Cell(
                "CHOICE — FEW OPTIONS",
                new Segment
                {
                    ItemsSource = ["NONE", "WARN", "INFO", "DEBUG"],
                    SelectedIndex = 2,
                    HorizontalAlignment = HorizontalAlignment.Left,
                },
                Note($"Up to {Choice.SegmentLimit}: show them all. No ComboBox needed, and nothing is hidden behind arrows.")),
            Cell(
                "CHOICE — MANY OPTIONS",
                Capped(new Stepper
                {
                    ItemsSource = ["Tiny", "Small", "Medium (English only)", "Medium", "Large", "Large (turbo)"],
                    Consequences = [null, null, "1.5 GB · runs on the GPU", null, null, null],
                    SelectedIndex = 2,
                }),
                Note("Position shown, consequence shown. The old spinner told you neither."),
                noteGap: 8),
            Cell(
                "AMOUNT — NUMBER + UNIT",
                new NumericUpDown
                {
                    Width = 216,
                    Value = 500,
                    InnerRightContent = UnitChip("ms"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                },
                Note("The unit lives in the control, so the label stops saying \"in milliseconds\".")),
            Cell(
                "LEVEL — SETTABLE",
                Capped(new Level { Minimum = 0, Maximum = 1.5, Value = 0.85, ReadoutFormat = "0.00" }),
                Note("A handle that overhangs the track, and the number always present.")),
            Cell(
                "GAUGE — REPORTED, NOT SETTABLE",
                Capped(LoadoutPages.Gauge(new LoadoutGauge("Power", "25.04 / 22.93 MW · 109%", 1.0, LoadoutTone.Danger))),
                Note("Hatched, capped, no handle — it can never be mistaken for the slider above.")),
            Cell(
                "BINDING",
                Binding("Ctrl+Alt+X", "button 9"),
                Note("Bindings are machine text, so they are mono — and they wrap instead of colliding.")),
        };

        return Section("Telling things apart", Reflow(cells, GridMinColumn, GridGap, GridGap, maxColumns: 3));
    }

    // -- Choosing among items --

    /// <summary>
    /// The list row, the segment and the stepper in every state. Hover is shown by setting the
    /// pseudo-class, so a pointer passing over one of these clears it.
    /// </summary>
    private static Control ChoosingSection()
    {
        var rows = new StackPanel
        {
            Spacing = Segment.Gap,
            Children =
            {
                KitRow("At rest", "tile · white · a"),
                Hovered(KitRow("Hover", "tile2")),
                KitRow("Selected", "a · knock · brown", selected: true),
                Disabled(KitRow("Disabled", "slab · grey")),
            },
        };

        var list = new ListBox
        {
            ItemsSource = new[] { "Diaguandri", "Shinrarta Dezhra", "Colonia" },
            SelectedIndex = 1,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };

        string[] options = ["NEAR", "SESSION", "ANYWHERE"];

        var segmentHover = new Segment { ItemsSource = options, SelectedIndex = 1 };
        Hovered(((Avalonia.Controls.Panel)segmentHover.Content!).Children[0]);

        var segments = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                segmentHover,
                Disabled(new Segment { ItemsSource = options, SelectedIndex = 1 }),
            },
        };

        string[] models = ["Tiny", "Small", "Medium", "Large", "Large (turbo)", "Base", "Base (English)", "Medium (English)"];

        var stepperHover = new Stepper { ItemsSource = models, SelectedIndex = 2 };
        Hovered(stepperHover.GetLogicalDescendants().OfType<RepeatButton>().Last());

        var steppers = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                Capped(stepperHover),
                Capped(Disabled(new Stepper { ItemsSource = models, SelectedIndex = 2 })),
            },
        };

        var cells = new Control[]
        {
            Cell("LIST ROW — THE d47-row CLASS", rows, Note("Rows sit 2px apart. No outline, no bar, no glow.")),
            Cell("LIST ROW — LISTBOX", list, Note("The same states on a ListBoxItem.")),
            Cell("SEGMENT — HOVER, CHOSEN, DISABLED", segments, Note("Equal-width tiles, 2px apart. The first option is hovered.")),
            Cell("STEPPER — HOVER, DISABLED", steppers, Note("The right arrow is hovered. The second stepper is disabled.")),
        };

        return Section("Choosing among items", Reflow(cells, GridMinColumn, GridGap, GridGap, maxColumns: 2));
    }

    private static Border KitRow(string name, string secondary, bool selected = false)
    {
        var nameText = ListRow.Name(new TextBlock { Text = name, FontFamily = Fonts.ProseFamily, FontSize = TypeScale.Body });
        var secondaryText = ListRow.Secondary(new TextBlock { Text = secondary, FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta });

        return ListRow.Dress(
            new Border
            {
                Padding = new Thickness(12, 6),
                Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { nameText, secondaryText } },
            },
            selected);
    }

    private static T Hovered<T>(T control) where T : Control
    {
        ((IPseudoClasses)control.Classes).Set(":pointerover", true);
        return control;
    }

    private static T Disabled<T>(T control) where T : Control
    {
        control.IsEnabled = false;
        return control;
    }

    /// <summary>Each button class at rest, hovered, focused and disabled. Hover and focus are shown by setting the pseudo-class.</summary>
    private static Control Actions()
    {
        var rows = new StackPanel { Spacing = Segment.Gap };

        foreach (var weight in new[] { "", "primary", "quiet", "destructive" })
        {
            var name = weight.Length == 0 ? "normal" : weight;
            rows.Children.Add(new WrapPanel
            {
                ItemSpacing = Segment.Gap,
                LineSpacing = Segment.Gap,
                Children =
                {
                    Weighted(new Button { Content = name }, weight),
                    Hovered(Weighted(new Button { Content = "hover" }, weight)),
                    Focused(Weighted(new Button { Content = "focus" }, weight)),
                    Disabled(Weighted(new Button { Content = "disabled" }, weight)),
                },
            });
        }

        return rows;
    }

    private static Button Weighted(Button button, string weight)
    {
        if (weight.Length > 0)
        {
            button.Classes.Add(weight);
        }

        return button;
    }

    private static T Focused<T>(T control) where T : Control
    {
        ((IPseudoClasses)control.Classes).Set(":focus-visible", true);
        return control;
    }

    private static Control Binding(params string[] keys)
    {
        var wrap = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };

        foreach (var key in keys)
        {
            var chip = SettingsView.BindingChip();
            chip.Content = key;
            wrap.Children.Add(chip);
        }

        var clear = new Button { Content = "CLEAR" };
        clear.Classes.Add("quiet");
        wrap.Children.Add(clear);

        return wrap;
    }

    /// <summary>The unit chip an Amount's <c>InnerRightContent</c> carries, the same shape <c>BuildNumber</c>
    /// draws for a real settings row (#351).</summary>
    private static Control UnitChip(string unit)
    {
        var text = new TextBlock
        {
            Text = unit,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var chip = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(11, 0), Child = text };
        Themed(chip, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return chip;
    }

    // -- Theme --

    private Control ThemeSection()
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
            PlaceholderText = Palettes.Elite.A.ToString(),
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

        return Section(
            "Theme",
            new StackPanel
            {
                Spacing = GridGap,
                Children = { Cell("THEME", picker), Cell("ACCENT — AS IF A HUD MATRIX SUPPLIED IT", accentRow) },
            });
    }

    /// <summary>
    /// A diagonal matrix that scales Elite's own <see cref="Palette.A"/> to <paramref name="target"/> one channel at a
    /// time — the same shape a Commander's HUD colour matrix takes, so typing an Accent exercises
    /// <see cref="ThemeManager.Apply"/>'s matrix path rather than a shortcut around it (#358).
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

    // -- Status --

    private static Control StatusSection()
    {
        var intro = Prose(
            "Each coloured token has one meaning. On the HUD-matrix theme all of them pass through the matrix; "
                + "the neutrals do not.",
            TypeScale.Body,
            ThemeManager.GreyKey);
        intro.MaxWidth = 680;

        var bars = Reflow(
            [
                StatusBar("A · values, rules", ThemeManager.AKey),
                StatusBar("CYAN · yours, ready", ThemeManager.CyanKey),
                StatusBar("BLUE · confirmed", ThemeManager.BlueKey),
                StatusBar("RED · hostile, error", ThemeManager.RedKey),
                StatusBar("YELLOW · stored", ThemeManager.YellowKey),
            ],
            BarMinWidth,
            BarGap,
            BarGap,
            maxColumns: 5);

        return Section("Status", new StackPanel { Spacing = 16, Children = { intro, bars } });
    }

    private static Control StatusBar(string label, string fillKey)
    {
        var text = new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Meta,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.KnockKey);

        var bar = new Border { Padding = new Thickness(16, 14), Child = text };
        Themed(bar, Border.BackgroundProperty, fillKey);

        return bar;
    }

    // -- Ramp --

    private static Control RampSection()
    {
        var ramp = new WrapPanel { ItemSpacing = 20, LineSpacing = 20 };

        foreach (var key in ThemeManager.Tokens)
        {
            ramp.Children.Add(Swatch(key["D47.".Length..].ToLowerInvariant(), key));
        }

        return Section("Ramp", ramp);
    }

    private static Control Swatch(string token, string resourceKey)
    {
        var box = new Border { Width = 72, Height = 72, BorderThickness = new Thickness(1) };
        Themed(box, Border.BackgroundProperty, resourceKey);
        Themed(box, Border.BorderBrushProperty, ThemeManager.BorderKey);

        var name = new TextBlock
        {
            Text = token,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var key = new TextBlock
        {
            Text = resourceKey,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
        };
        Themed(key, TextBlock.ForegroundProperty, ThemeManager.TextFaintKey);

        var hex = new TextBlock { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Caption };
        Themed(hex, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);
        box.GetResourceObservable(resourceKey).Subscribe(new Avalonia.Reactive.AnonymousObserver<object?>(value =>
            hex.Text = value is SolidColorBrush { Color: var c } ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : null));

        return new StackPanel { Spacing = 6, Width = 130, Children = { box, name, hex, key } };
    }

    // -- Four ranks of heading --

    private static Control HeadingRanksSection()
    {
        var group = TitleText.Build("GROUP, WITH A RULE", TypeScale.Section, TitleRank.Group);
        group.Margin = new Thickness(0, 16, 0, 0);

        var subgroup = TitleText.Build("SUBGROUP", TypeScale.Caption, TitleRank.Subgroup);
        subgroup.Margin = new Thickness(0, 16, 0, 0);

        var row = TitleText.Build("Row label, sentence case", TypeScale.Body, TitleRank.Row, sentence: true);
        row.Margin = new Thickness(0, 12, 0, 0);

        var block = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(24, 0, 0, 0),
            Child = new StackPanel { Children = { TitleText.Screen("SCREEN TITLE"), group, subgroup, row } },
        };
        Themed(block, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return Section("Four ranks of heading", block);
    }

    // -- A settings row: normal, protected, with a reset shown --

    private static Control SettingsRowsSection()
    {
        var legend = Prose(SettingsView.ProtectedLegend, TypeScale.Secondary, ThemeManager.TextMutedKey);

        var rows = new StackPanel
        {
            Children =
            {
                SettingsRowDemo(
                    "When it assumes you mean it",
                    new Segment { ItemsSource = ["NAMED ONLY", "CAUTIOUS", "BALANCED", "EAGER"], SelectedIndex = 2 }),
                SettingsRowDemo(
                    "Push to talk",
                    new CheckBox { IsChecked = true, Classes = { "bare" } },
                    protectedRow: true),
                SettingsRowDemo(
                    "Capture before the key",
                    new NumericUpDown { Value = 500, InnerRightContent = UnitChip("ms") },
                    showReset: true),
            },
        };

        return Section("Settings rows", new StackPanel { Spacing = 16, Children = { legend, rows } });
    }

    /// <summary>The row layout <c>BuildRow</c> draws — label column, control, reserved reset gutter, the
    /// protected bar — built here from the same sizes and resource keys rather than a settings row (#333).</summary>
    private static Control SettingsRowDemo(
        string label, Control control, bool showReset = false, bool protectedRow = false)
    {
        const double RowMinHeight = 52;
        const double RowVerticalPadding = 8;
        const double LabelColumnMaxWidth = 300;
        const double ResetGutterWidth = 44;
        const double ProtectedBarWidth = 3;
        const double ProtectedBarPadding = 12;

        var caption = new TextBlock
        {
            Text = label,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(caption, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto) { MaxWidth = LabelColumnMaxWidth },
                new ColumnDefinition(16, GridUnitType.Pixel),
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(16, GridUnitType.Pixel),
                new ColumnDefinition(ResetGutterWidth, GridUnitType.Pixel),
            ],
        };

        Grid.SetColumn(caption, 0);
        Grid.SetColumn(control, 2);
        grid.Children.Add(caption);
        grid.Children.Add(control);

        if (showReset)
        {
            var reset = new Button
            {
                Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme,
                Content = Glyphs.Text(Glyphs.ResetText, TypeScale.Secondary),
                Width = TypeScale.MinimumTarget,
                Height = TypeScale.MinimumTarget,
            };

            AutomationProperties.SetName(reset, "Reset to default");
            Grid.SetColumn(reset, 4);
            grid.Children.Add(reset);
        }

        var rowShape = new Border
        {
            MinHeight = RowMinHeight,
            Padding = new Thickness(0, RowVerticalPadding),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid,
        };
        Themed(rowShape, Border.BorderBrushProperty, ThemeManager.BorderKey);

        Control line = rowShape;

        if (protectedRow)
        {
            var bar = new Border
            {
                BorderThickness = new Thickness(ProtectedBarWidth, 0, 0, 0),
                Padding = new Thickness(ProtectedBarPadding, 0, 0, 0),
                Child = line,
            };
            Themed(bar, Border.BorderBrushProperty, ThemeManager.WarnKey);
            line = bar;
        }

        return line;
    }

    // -- Level-1 tabs and the level-2 text row --

    private Control NavigationSection()
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

        return Section(
            "Navigation",
            new StackPanel
            {
                Spacing = GridGap,
                Children =
                {
                    Cell("LEVEL 1 — TABS", level1Strip, Note("Only the selected tab glows.")),
                    Cell("LEVEL 2 — TEXT ROW", level2),
                },
            });
    }

    // -- Shared pieces --

    private static Control Section(string heading, Control content) => new StackPanel
    {
        Spacing = HeadingGap,
        Children = { TitleText.GroupRow(TitleText.Build(heading, TypeScale.Section, TitleRank.Group)), content },
    };

    /// <summary>A grid cell: the caption, the control, and an optional note beneath it.</summary>
    private static Control Cell(string caption, Control control, TextBlock? note = null, double noteGap = 12)
    {
        var label = Caption(caption);
        label.Margin = new Thickness(0, 0, 0, 12);

        var cell = new StackPanel { Children = { label, control } };

        if (note is not null)
        {
            note.Margin = new Thickness(0, noteGap, 0, 0);
            cell.Children.Add(note);
        }

        return cell;
    }

    /// <summary>A tracked mono caption at caption size. The text is authored in capitals, since the
    /// tracking is set for them.</summary>
    private static TextBlock Caption(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextFaintKey);
        return label;
    }

    private static TextBlock Note(string? text) => Prose(text, TypeScale.Tip, ThemeManager.TextFaintKey);

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

    /// <summary>Fills the column up to <see cref="CappedControlWidth"/>, left-aligned.</summary>
    private static Control Capped(Control control) => new Grid
    {
        ColumnDefinitions = [new ColumnDefinition(1, GridUnitType.Star) { MaxWidth = CappedControlWidth }],
        Children = { control },
    };

    /// <summary>
    /// Lays <paramref name="cells"/> in reading order in as many equal columns, up to
    /// <paramref name="maxColumns"/>, as fit at <paramref name="minColumn"/> each, re-laying them when
    /// the width changes.
    /// </summary>
    private static Grid Reflow(
        IReadOnlyList<Control> cells, double minColumn, double columnGap, double rowGap, int maxColumns)
    {
        var grid = new Grid { ColumnSpacing = columnGap, RowSpacing = rowGap };

        foreach (var cell in cells)
        {
            cell.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(cell);
        }

        var laid = 0;

        void Lay(double width)
        {
            var columns = Math.Clamp((int)Math.Floor((width + columnGap) / (minColumn + columnGap)), 1, maxColumns);

            if (columns == laid)
            {
                return;
            }

            laid = columns;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            for (var c = 0; c < columns; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }

            for (var r = 0; r < (cells.Count + columns - 1) / columns; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (var i = 0; i < cells.Count; i++)
            {
                Grid.SetColumn(cells[i], i % columns);
                Grid.SetRow(cells[i], i / columns);
            }
        }

        Lay(minColumn * maxColumns + columnGap * (maxColumns - 1));
        grid.SizeChanged += (_, e) => Lay(e.NewSize.Width);

        return grid;
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
#endif
