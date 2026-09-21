#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Controls;

/// <summary>
/// The control system handoff's sign-off gate (#358): every kit control, the derived ramp and the
/// four heading ranks, drawn from the real control themes and resource keys rather than copies.
/// Debug-only — <c>Ctrl+Shift+K</c> opens it from the panel.
/// </summary>
public sealed class ControlKitWindow : Window
{
    private const double ContentMaxWidth = 1120;
    private const double PanelPadding = 32;

    private readonly ThemeManager _themeManager = new(Application.Current!, NullLogger<ThemeManager>.Instance);

    public ControlKitWindow()
    {
        Title = "Control Kit";
        Width = 1180;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        // D47.Tab is scoped to wherever merges PanelTabs.axaml, the same reason D47.Segment's own
        // doc comment gives for staying app-wide instead — a desktop-only window carries no merge
        // of it on its own.
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://d47/"))
        {
            Source = new Uri("avares://d47/Panel/PanelTabs.axaml"),
        });

        var body = new StackPanel
        {
            Spacing = 32,
            Children =
            {
                RampSection(),
                ControlsSection(),
                HeadingsAndRowsSection(),
                NavigationSection(),
                ThemeSection(),
            },
        };

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Border
            {
                MaxWidth = ContentMaxWidth,
                Padding = new Thickness(PanelPadding),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = body,
            },
        };
    }

    // -- 1. The ramp --

    private static Control RampSection()
    {
        var ramp = new WrapPanel { ItemSpacing = 20, LineSpacing = 20 };

        foreach (var (token, key) in new[]
        {
            ("hot", ThemeManager.AccentInkKey),
            ("ink", ThemeManager.TextKey),
            ("ink-2", ThemeManager.TextMutedKey),
            ("ink-3", ThemeManager.TextFaintKey),
            ("line", ThemeManager.RuleKey),
            ("line-2", ThemeManager.BorderKey),
            ("fill-1", ThemeManager.FillLowKey),
            ("fill-2", ThemeManager.FillHighKey),
            ("fill-3", ThemeManager.FillHigherKey),
            ("knock", ThemeManager.KnockKey),
            ("Background", ThemeManager.BackgroundKey),
        })
        {
            ramp.Children.Add(Swatch(token, key));
        }

        var status = new WrapPanel { ItemSpacing = 20, LineSpacing = 20 };

        foreach (var (token, key) in new[]
        {
            ("danger", ThemeManager.DangerKey),
            ("warn", ThemeManager.WarnKey),
            ("good", ThemeManager.GoodKey),
            ("info", ThemeManager.InfoKey),
        })
        {
            status.Children.Add(Swatch(token, key));
        }

        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                TitleText.GroupRow(TitleText.Build("Ramp", TypeScale.Heading, TitleRank.Group)),
                ramp,
                TitleText.Build("Status", TypeScale.Subheading, TitleRank.Subgroup),
                status,
            },
        };
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

        return new StackPanel { Spacing = 6, Width = 130, Children = { box, name, key } };
    }

    // -- 2. Every control, in each state --

    private static Control ControlsSection()
    {
        var wrap = new WrapPanel { ItemSpacing = 24, LineSpacing = 24 };

        wrap.Children.Add(Cell("SWITCH — ON", new ToggleSwitch { IsChecked = true }));
        wrap.Children.Add(Cell("SWITCH — OFF", new ToggleSwitch { IsChecked = false }));

        wrap.Children.Add(Cell(
            "CHOICE — SEGMENTED",
            new Segment { ItemsSource = ["NAMED ONLY", "CAUTIOUS", "BALANCED", "EAGER"], SelectedIndex = 2 }));

        wrap.Children.Add(Cell(
            "CHOICE — STEPPER",
            new Stepper
            {
                Width = 280,
                ItemsSource = ["Small", "Medium (English only)", "Large"],
                Consequences = [null, "1.5 GB · English only · slower than Small", null],
                SelectedIndex = 1,
            }));

        wrap.Children.Add(Cell(
            "AMOUNT",
            new NumericUpDown { Width = 216, Value = 500, InnerRightContent = UnitChip("ms") }));

        wrap.Children.Add(Cell("FIELD — EMPTY", new TextBox { Width = 240, PlaceholderText = "Search this page" }));
        wrap.Children.Add(Cell("FIELD — FILLED", new TextBox { Width = 240, Text = "Farseer" }));

        wrap.Children.Add(Cell("REPORT", ReportRow("11 ships, the oldest last seen about a day ago.")));

        wrap.Children.Add(Cell("LEVEL", new Level { Width = 320, Minimum = 0, Maximum = 100, Value = 64 }));

        wrap.Children.Add(Cell("GAUGE — DANGER", DemoGauge(LoadoutTone.Danger)));
        wrap.Children.Add(Cell("GAUGE — WARN", DemoGauge(LoadoutTone.Warn)));
        wrap.Children.Add(Cell("GAUGE — GOOD", DemoGauge(LoadoutTone.Good)));

        wrap.Children.Add(Cell("BINDING", BindingDemo("Ctrl+Alt+X", "button 9")));

        var buttons = new WrapPanel { ItemSpacing = 12, LineSpacing = 12 };
        buttons.Children.Add(new Button { Content = "NORMAL" });

        var primary = new Button { Content = "PRIMARY" };
        primary.Classes.Add("primary");
        buttons.Children.Add(primary);

        var quiet = new Button { Content = "QUIET" };
        quiet.Classes.Add("quiet");
        buttons.Children.Add(quiet);

        var destructive = new Button { Content = "DESTRUCTIVE" };
        destructive.Classes.Add("destructive");
        buttons.Children.Add(destructive);

        buttons.Children.Add(new Button { Content = "DISABLED", IsEnabled = false });

        wrap.Children.Add(Cell("ACTIONS", buttons));

        return new StackPanel
        {
            Spacing = 16,
            Children = { TitleText.GroupRow(TitleText.Build("Controls", TypeScale.Heading, TitleRank.Group)), wrap },
        };
    }

    private static Control Cell(string caption, Control control)
    {
        var label = new TextBlock
        {
            Text = caption,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
            LetterSpacing = 1.2,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.TextFaintKey);

        return new StackPanel { Spacing = 8, Children = { label, control } };
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
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.TextFaintKey);

        var chip = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(11, 0), Child = text };
        Themed(chip, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return chip;
    }

    /// <summary>A Report row: no box, a 2px left rule, the same shape <c>BuildInfo</c> draws (#335).</summary>
    private static Control ReportRow(string text)
    {
        var block = new SelectableTextBlock
        {
            Text = text,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            Width = 320,
        };
        Themed(block, SelectableTextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var inset = new Border { BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(14), Child = block };
        Themed(inset, Border.BorderBrushProperty, ThemeManager.BorderKey);

        return inset;
    }

    private static Control DemoGauge(LoadoutTone tone)
    {
        var (name, reading, fill) = tone switch
        {
            LoadoutTone.Danger => ("Power", "31.2 / 28.4 MW", 1.0),
            LoadoutTone.Warn => ("Heat", "82%", 0.82),
            _ => ("Jump range", "22.4 LY", 0.6),
        };

        var gauge = LoadoutPages.Gauge(new LoadoutGauge(name, reading, fill, tone));
        gauge.Width = 320;
        return gauge;
    }

    /// <summary>Chips and CLEAR, the same shape <c>BuildBind</c> draws for a real binding row (#354).</summary>
    private static Control BindingDemo(params string[] chips)
    {
        var wrap = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };

        foreach (var label in chips)
        {
            var chip = new Button
            {
                Content = label,
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Secondary,
                Padding = new Thickness(14, 10),
            };
            Themed(chip, Button.BackgroundProperty, ThemeManager.FillHigherKey);
            Themed(chip, Button.ForegroundProperty, ThemeManager.AccentInkKey);
            wrap.Children.Add(chip);
        }

        var clear = new Button { Content = "CLEAR" };
        clear.Classes.Add("quiet");
        wrap.Children.Add(clear);

        return wrap;
    }

    // -- 3. Heading ranks and a settings row: normal, protected, with a reset shown --

    private static Control HeadingsAndRowsSection()
    {
        var ranks = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                TitleText.GroupRow(TitleText.Build("Screen title", TypeScale.Title, TitleRank.Screen)),
                TitleText.GroupRow(TitleText.Build("Group heading", TypeScale.Heading, TitleRank.Group)),
                TitleText.Build("SUBGROUP CAPTION", TypeScale.Subheading, TitleRank.Subgroup),
                TitleText.Build("Row label", TypeScale.Body, TitleRank.Row, sentence: true),
            },
        };

        var legend = new TextBlock
        {
            Text = SettingsView.ProtectedLegend,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(legend, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var rows = new StackPanel
        {
            Children =
            {
                SettingsRowDemo(
                    "When it assumes you mean it",
                    new Segment { ItemsSource = ["NAMED ONLY", "CAUTIOUS", "BALANCED", "EAGER"], SelectedIndex = 2 }),
                SettingsRowDemo(
                    "Push to talk",
                    new ToggleSwitch { IsChecked = true },
                    protectedRow: true),
                SettingsRowDemo(
                    "Capture before the key",
                    new NumericUpDown { Value = 500, InnerRightContent = UnitChip("ms") },
                    showReset: true),
            },
        };

        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                TitleText.GroupRow(TitleText.Build("Headings and rows", TypeScale.Heading, TitleRank.Group)),
                ranks,
                legend,
                rows,
            },
        };
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
                Content = Glyphs.Draw(Glyphs.Reset, ThemeManager.AccentKey, TypeScale.Secondary),
                Width = TypeScale.MinimumTarget,
                Height = TypeScale.MinimumTarget,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

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

    // -- 4. Level-1 tabs and the level-2 text row --

    private Control NavigationSection()
    {
        var tabTheme = this.TryFindResource("D47.Tab", out var resource) ? resource as ControlTheme : null;
        var group = $"D47KitTabs{Guid.NewGuid():N}";
        var level1 = new WrapPanel { ItemSpacing = 3, LineSpacing = 3 };

        var tabs = new[] { "SHIP", "SUPPLY", "TRAVEL" };
        for (var i = 0; i < tabs.Length; i++)
        {
            level1.Children.Add(new RadioButton
            {
                Theme = tabTheme,
                GroupName = group,
                Content = tabs[i],
                IsChecked = i == 0,
            });
        }

        var level1Strip = new Border { BorderThickness = new Thickness(0, 0, 0, 2), Child = level1 };
        Themed(level1Strip, Border.BorderBrushProperty, ThemeManager.RuleKey);

        var level2 = new TextChoice { ItemsSource = ["In Ship", "Log File", "Journal File"], SelectedIndex = 0 };

        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                TitleText.GroupRow(TitleText.Build("Navigation", TypeScale.Heading, TitleRank.Group)),
                Cell("LEVEL 1 — TABS", level1Strip),
                Cell("LEVEL 2 — TEXT ROW", level2),
            },
        };
    }

    // -- 5. Theme picker and the Accent entry --

    private Control ThemeSection()
    {
        var picker = new Segment { ItemsSource = [.. ThemeCatalog.All.Select(t => t.Name)], SelectedIndex = 0 };

        picker.SelectionChanged += (_, _) =>
        {
            if (picker.SelectedIndex >= 0)
            {
                _themeManager.Apply(ThemeCatalog.All[picker.SelectedIndex].Id);
            }
        };

        var accentBox = new TextBox { Width = 160, PlaceholderText = "#00E5FF" };
        var apply = new Button { Content = "APPLY" };

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
            Children = { accentBox, apply },
        };

        return new StackPanel
        {
            Spacing = 16,
            Children =
            {
                TitleText.GroupRow(TitleText.Build("Theme", TypeScale.Heading, TitleRank.Group)),
                Cell("THEME", picker),
                Cell("ACCENT — AS IF A HUD MATRIX SUPPLIED IT", accentRow),
            },
        };
    }

    /// <summary>
    /// A diagonal matrix that scales Elite's own Accent to <paramref name="target"/> one channel at a
    /// time — the same shape a Commander's HUD colour matrix takes, so typing an Accent exercises
    /// <see cref="ThemeManager.Apply"/>'s matrix path rather than a shortcut around it (#358).
    /// </summary>
    private static GuiColourMatrix MatrixFor(Color target)
    {
        var source = Palettes.Elite.Accent;

        double Scale(byte from, byte to) => from == 0 ? 0 : to / (double)from;

        return new GuiColourMatrix(
            Scale(source.R, target.R), 0, 0,
            0, Scale(source.G, target.G), 0,
            0, 0, Scale(source.B, target.B));
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
#endif
