using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;

namespace D47.App.Settings;

/// <summary>
/// The Guardian Voice Effects group as one control, drawn from the preset row: the preset picker and
/// TEST on the row, and every effect in chain order under it at the group's full width.
/// </summary>
public partial class SettingsView
{
    /// <summary>Marks the effects list, for a test to find it.</summary>
    public const string GuardianEffectsName = "GuardianEffects";

    /// <summary>Marks one effect's strip in the list.</summary>
    public const string GuardianStripClass = "guardian-effect";

    /// <summary>Marks the position number on a strip.</summary>
    public const string GuardianNumberName = "GuardianNumber";

    /// <summary>Marks the value a strip's stepper shows.</summary>
    public const string GuardianValueName = "GuardianValue";

    /// <summary>Marks the reserved drag-handle column on a strip.</summary>
    public const string GuardianHandleName = "GuardianHandle";

    private const double GuardianHandleWidth = 24;

    private const double GuardianNumberWidth = 24;

    private const double GuardianCheckWidth = 200;

    private const double GuardianParameterWidth = 58;

    /// <summary>The narrowest the level track is drawn.</summary>
    public const double GuardianTrackMinWidth = 180;

    private const double GuardianStepperWidth = 156;

    private const double GuardianTileButtonWidth = 120;

    private const double GuardianTileButtonHeight = 40;

    /// <summary>Controls a group's own builder draws for rows that have none of their own, by key.</summary>
    private readonly Dictionary<string, Control> _drawnByGroup = new(StringComparer.Ordinal);

    /// <summary>
    /// Set by a builder whose control also draws a block at the group's full width under its row, and the
    /// words a query matches that block by; taken by <see cref="BuildRow"/>.
    /// </summary>
    private (Control Block, string Words)? _underRow;

    private sealed record GuardianStrip(
        string Id,
        Border Strip,
        TextBlock Number,
        CheckBox Box,
        Level Level,
        TextBlock Value)
    {
        public IDisposable? ValueInk { get; set; }
    }

    private (Control, Action) BuildGuardianVoice(SettingRow row, TextBlock message)
    {
        var picker = new InlinePicker { Label = row.Label };
        picker.Picked += (_, id) => Apply(row, id, message);

        var testRow = _settings!.Find(SpeechCapability.GuardianTestKey);
        var test = GuardianTileButton(testRow?.PressLabel ?? "Test");
        test.Name = "GuardianTest";
        test.VerticalAlignment = VerticalAlignment.Top;
        test.IsEnabled = testRow?.PressAsync is not null;
        test.Click += async (_, _) => await PlayGuardianTestAsync(testRow!, test, message);

        var control = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 2 };
        Grid.SetColumn(test, 1);
        control.Children.Add(picker);
        control.Children.Add(test);

        var strips = new StackPanel { Spacing = 2 };
        var byId = new Dictionary<string, GuardianStrip>(StringComparer.Ordinal);

        foreach (var effect in GuardianVoice.Table)
        {
            var strip = BuildGuardianStrip(effect, message);
            byId[effect.Id] = strip;
            strips.Children.Add(strip.Strip);
            _drawnByGroup[SpeechCapability.GuardianEffectKey(effect.Id)] = strip.Box;
            _drawnByGroup[SpeechCapability.GuardianLevelKey(effect.Id)] = strip.Level;
        }

        _drawnByGroup[SpeechCapability.GuardianTestKey] = test;

        var title = new TextBlock { Text = "Effects", FontFamily = Fonts.ProseFamily, FontSize = TypeScale.Secondary };
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var hint = new TextBlock { Text = "Applied top to bottom.", FontSize = TypeScale.Meta, VerticalAlignment = VerticalAlignment.Bottom };
        Themed(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { title, hint } };

        var list = new StackPanel
        {
            Name = GuardianEffectsName,
            Spacing = 10,
            Margin = new Thickness(RowBarWidth + RowHorizontalPadding, 12, 0, 12),
            Children = { head, strips },
        };

        _underRow = (list, string.Join(" ", GuardianVoice.Table.Select(effect => $"{effect.Label} {effect.Parameter}")));

        return (control, () =>
        {
            var speech = _settings!.Current.Speech;
            var preset = GuardianPresets.Preset(speech);
            var choices = GuardianPresets.Choices(speech);
            var saved = choices.Where(id => GuardianPresets.Find(id) is null && id != GuardianPresets.CustomId).ToList();

            var entries = new List<InlinePickerEntry>();
            entries.AddRange(GuardianPresets.Builtins.Select(builtin =>
                new InlinePickerOption(builtin.Id, builtin.Label, ThemeManager.WhiteKey)));
            entries.Add(new InlinePickerHeading("Your presets"));
            entries.AddRange(saved.Count == 0
                ? [new InlinePickerNote("None yet. Set up the effects, then press SAVE AS.")]
                : saved.Select(id => new InlinePickerOption(id, GuardianPresets.Label(id), ThemeManager.CyanKey)));
            entries.Add(new InlinePickerSpace(8));
            entries.Add(new InlinePickerOption(GuardianPresets.CustomId, GuardianPresets.Label(GuardianPresets.CustomId), ThemeManager.WhiteKey));

            var basis = speech.GuardianVoice.Basis is { } named
                ? (speech.GuardianVoice.SavedPresets ?? [])
                    .FirstOrDefault(p => string.Equals(p.Name, named, StringComparison.OrdinalIgnoreCase))?.Name
                : null;

            picker.Show(
                entries,
                preset,
                GuardianPresets.Label(preset),
                saved.Contains(preset) ? ThemeManager.CyanKey : ThemeManager.WhiteKey,
                preset == GuardianPresets.CustomId && basis is not null ? $"  · changed from {basis}" : null);

            var effects = GuardianVoice.Effects(speech.GuardianVoice);

            if (!effects.Select(effect => effect.Id).SequenceEqual(strips.Children.Select(child => child.Tag as string)))
            {
                strips.Children.Clear();

                foreach (var effect in effects)
                {
                    strips.Children.Add(byId[effect.Id].Strip);
                }
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var stored = effects[i];
                var strip = byId[stored.Id];
                var effect = GuardianVoice.Table.First(known => known.Id == stored.Id);

                strip.Number.Text = (i + 1).ToString("00", CultureInfo.InvariantCulture);
                strip.Box.IsChecked = stored.Ticked;
                strip.Level.Value = stored.Level / (double)GuardianVoice.HighestLevel;
                strip.Level.Muted = !stored.Ticked;
                strip.Value.Text = GuardianShown(effect, stored.Level);
                strip.ValueInk?.Dispose();
                strip.ValueInk = Themed(
                    strip.Value, TextBlock.ForegroundProperty, stored.Ticked ? ThemeManager.WhiteKey : ThemeManager.Grey2Key);
            }
        });
    }

    /// <summary>One effect's strip: reserved handle, position, checkbox, level and a − value + stepper.</summary>
    private GuardianStrip BuildGuardianStrip(GuardianEffect effect, TextBlock message)
    {
        var levelKey = SpeechCapability.GuardianLevelKey(effect.Id);

        var handle = new Border { Name = GuardianHandleName, Width = GuardianHandleWidth };

        var number = new TextBlock
        {
            Name = GuardianNumberName,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(number, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var (box, label) = LabeledCheckBox.Build(effect.Label, labelFirst: false);
        label.FontSize = TypeScale.Secondary;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        box.Width = GuardianCheckWidth;
        box.VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(box, effect.Label);
        ToolTip.SetTip(label, effect.Help);

        box.IsCheckedChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                Apply(SpeechCapability.GuardianEffectKey(effect.Id), box.IsChecked == true ? "true" : "false", message);
            }
        };

        var parameter = new TextBlock
        {
            Text = effect.Parameter.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = TypeScale.Caption * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(parameter, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var level = new Level
        {
            Minimum = 1.0 / GuardianVoice.HighestLevel,
            Maximum = 1,
            Step = 1.0 / GuardianVoice.HighestLevel,
            ShowsReadout = false,
            OnTile = true,
            MinWidth = GuardianTrackMinWidth,
        };
        AutomationProperties.SetName(level, $"{effect.Label} level");

        level.ValueChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                WriteGuardianLevel(levelKey, (int)Math.Round(level.Value * GuardianVoice.HighestLevel), message);
            }
        };

        var levelCell = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{GuardianParameterWidth},*"),
            Margin = new Thickness(4, 0, 12, 0),
        };
        Grid.SetColumn(level, 1);
        levelCell.Children.Add(parameter);
        levelCell.Children.Add(level);

        var value = new TextBlock
        {
            Name = GuardianValueName,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var valueCell = new Border { Child = value };
        Themed(valueCell, Border.BackgroundProperty, ThemeManager.SlabKey);

        var stepper = new Grid
        {
            Width = GuardianStepperWidth,
            Height = TypeScale.MinimumTarget,
            ColumnDefinitions = new ColumnDefinitions($"{TypeScale.MinimumTarget},*,{TypeScale.MinimumTarget}"),
        };
        var down = GuardianArrow("−", $"Decrease {effect.Label}", () => Stored() - 1);
        var up = GuardianArrow("+", $"Increase {effect.Label}", () => Stored() + 1);
        Grid.SetColumn(valueCell, 1);
        Grid.SetColumn(up, 2);
        stepper.Children.Add(down);
        stepper.Children.Add(valueCell);
        stepper.Children.Add(up);

        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                $"{GuardianHandleWidth},{GuardianNumberWidth},{GuardianCheckWidth},*,{GuardianStepperWidth}"),
        };
        Grid.SetColumn(number, 1);
        Grid.SetColumn(box, 2);
        Grid.SetColumn(levelCell, 3);
        Grid.SetColumn(stepper, 4);
        columns.Children.Add(handle);
        columns.Children.Add(number);
        columns.Children.Add(box);
        columns.Children.Add(levelCell);
        columns.Children.Add(stepper);

        var strip = new Border { MinHeight = TypeScale.MinimumTarget, Child = columns, Tag = effect.Id };
        strip.Classes.Add(GuardianStripClass);
        Themed(strip, Border.BackgroundProperty, ThemeManager.TileKey);

        return new GuardianStrip(effect.Id, strip, number, box, level, value);

        int Stored() =>
            int.TryParse(_settings!.Read(levelKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : effect.DefaultLevel;

        Button GuardianArrow(string glyph, string name, Func<int> next)
        {
            var arrow = new Button
            {
                Content = glyph,
                FontSize = TypeScale.Subheading,
                Width = TypeScale.MinimumTarget,
                MinWidth = TypeScale.MinimumTarget,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Themed(arrow, Button.BackgroundProperty, ThemeManager.Tile2Key);
            AutomationProperties.SetName(arrow, name);
            arrow.Click += (_, _) => WriteGuardianLevel(levelKey, next(), message);

            return arrow;
        }
    }

    /// <summary>Writes a level through its row, held between the lowest and highest level.</summary>
    private void WriteGuardianLevel(string key, int level, TextBlock message) =>
        Apply(
            key,
            Math.Clamp(level, GuardianVoice.LowestLevel, GuardianVoice.HighestLevel).ToString(CultureInfo.InvariantCulture),
            message);

    /// <summary>An effect's parameter at a level, in the unit its table names.</summary>
    internal static string GuardianShown(GuardianEffect effect, int level)
    {
        var value = effect.Value(level);

        return effect.Unit switch
        {
            "%" => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value * 100):0}%"),
            "st" => string.Create(CultureInfo.InvariantCulture, $"{value:0.0} st").Replace('-', '−'),
            "×" => string.Create(CultureInfo.InvariantCulture, $"{value:0.0}×"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{value:0.##} {effect.Unit}"),
        };
    }

    /// <summary>Every effect off, default order and levels, basis cleared: <see cref="GuardianPresets.Reset"/>.</summary>
    private void ResetGuardianVoice() =>
        _settings!.Replace(
            "Guardian voice reset",
            settings => GuardianPresets.Reset(settings.Speech).Settings is { } speech ? settings with { Speech = speech } : settings);

    /// <summary>A 120 × 40 tile button, the preset row's size for TEST and the preset buttons.</summary>
    private static Button GuardianTileButton(string label) => new()
    {
        Content = label,
        Width = GuardianTileButtonWidth,
        Height = GuardianTileButtonHeight,
        MinHeight = GuardianTileButtonHeight,
        Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    /// <summary>Presses the Test row's action, the button solid A and reading PLAYING while it runs.</summary>
    private async Task PlayGuardianTestAsync(SettingRow row, Button test, TextBlock message)
    {
        if (_pressing || row.PressAsync is not { } running)
        {
            return;
        }

        _pressing = true;
        message.IsVisible = false;

        var label = test.Content;
        test.Content = "Playing";
        var fill = Themed(test, Button.BackgroundProperty, ThemeManager.AKey);
        var ink = Themed(test, Button.ForegroundProperty, ThemeManager.KnockKey);

        try
        {
            var said = await running(new Progress<double>(), CancellationToken.None);

            if (said is { Length: > 0 })
            {
                Note(message, said);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            Note(message, $"{row.Label} could not be done: {ex.Message}");
        }
        finally
        {
            _pressing = false;
            fill.Dispose();
            ink.Dispose();
            test.Content = label;
            Refresh();
        }
    }
}
