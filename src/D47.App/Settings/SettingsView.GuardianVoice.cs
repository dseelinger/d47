using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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

    /// <summary>Marks the drag handle on a strip.</summary>
    public const string GuardianHandleName = "GuardianHandle";

    /// <summary>Marks the SAVE AS button, for a test to find it.</summary>
    public const string GuardianSaveAsName = "GuardianSaveAs";

    /// <summary>Marks the RENAME button, for a test to find it.</summary>
    public const string GuardianRenameName = "GuardianRename";

    /// <summary>Marks the name row, shown only while creating or renaming a preset.</summary>
    public const string GuardianNameRowName = "GuardianNameRow";

    /// <summary>Marks the name row's label: "Preset name" or "New name".</summary>
    public const string GuardianNameLabelName = "GuardianNameLabel";

    /// <summary>Marks the name row's text box.</summary>
    public const string GuardianNameFieldName = "GuardianNameField";

    /// <summary>Marks the name row's SAVE or RENAME button.</summary>
    public const string GuardianNameActionName = "GuardianNameAction";

    /// <summary>Marks the name row's CANCEL button.</summary>
    public const string GuardianNameCancelName = "GuardianNameCancel";

    /// <summary>Marks the name row's message line.</summary>
    public const string GuardianNameMessageName = "GuardianNameMessage";

    /// <summary>Marks the notice shown after a preset action.</summary>
    public const string GuardianNoticeName = "GuardianNotice";

    /// <summary>How long the notice stays up before it hides itself.</summary>
    public static readonly TimeSpan GuardianNoticeDuration = TimeSpan.FromSeconds(6);

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

    /// <summary>The handle's tooltip.</summary>
    public const string GuardianHandleTip = "Drag to reorder. Arrow keys also move it.";

    private const double GuardianDraggedOpacity = 0.55;

    private sealed record GuardianStrip(
        string Id,
        Border Strip,
        Border Handle,
        IReadOnlyList<Border> Dots,
        TextBlock Number,
        CheckBox Box,
        Level Level,
        TextBlock Value)
    {
        public IDisposable? ValueInk { get; set; }

        public IDisposable? StripInk { get; set; }

        public IDisposable? HandleInk { get; set; }

        public List<IDisposable> DotInks { get; } = [];

        public bool Hovered { get; set; }

        public bool Dragging { get; set; }
    }

    /// <summary>A drag in progress: the strip's place when it started, where the pointer began, and where it would drop.</summary>
    private sealed class GuardianDrag
    {
        public required int From { get; init; }

        public required double StartY { get; init; }

        public required double Pitch { get; init; }

        public int To { get; set; }
    }

    /// <summary>Whether the name row is closed, saving a new preset or renaming the loaded one.</summary>
    private enum GuardianNameMode
    {
        None,
        Create,
        Rename,
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

        var nameMode = GuardianNameMode.None;
        string? nameRowOldName = null;
        DispatcherTimer? noticeTimer = null;
        IDisposable? saveAsFill = null;
        IDisposable? saveAsInk = null;
        IDisposable? renameFill = null;
        IDisposable? renameInk = null;

        var notice = GuardianNotice(out var noticeText);

        var (nameRow, nameLabel, nameField, nameAction, nameMessage, nameCancel) = GuardianNameRow();

        var saveAs = GuardianTileButton("Save as");
        saveAs.Name = GuardianSaveAsName;
        saveAs.VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(saveAs, "Save as");
        saveAs.Click += (_, _) => OpenNameRow(GuardianNameMode.Create);

        var rename = GuardianTileButton("Rename");
        rename.Name = GuardianRenameName;
        rename.VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetName(rename, "Rename");
        rename.Click += (_, _) => OpenNameRow(GuardianNameMode.Rename);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { test, saveAs, rename } };

        var control = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 2 };
        Grid.SetColumn(buttons, 1);
        control.Children.Add(picker);
        control.Children.Add(buttons);

        var strips = new StackPanel { Spacing = 2 };
        var byId = new Dictionary<string, GuardianStrip>(StringComparer.Ordinal);

        foreach (var effect in GuardianVoice.Table)
        {
            var strip = BuildGuardianStrip(effect, message);
            byId[effect.Id] = strip;
            strips.Children.Add(strip.Strip);
            WireGuardianHandle(strip, strips, byId, message);
            _drawnByGroup[SpeechCapability.GuardianEffectKey(effect.Id)] = strip.Box;
            _drawnByGroup[SpeechCapability.GuardianLevelKey(effect.Id)] = strip.Level;
        }

        _drawnByGroup[SpeechCapability.GuardianTestKey] = test;

        var title = new TextBlock { Text = "Effects", FontFamily = Fonts.ProseFamily, FontSize = TypeScale.Secondary };
        Themed(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var hint = new TextBlock { Text = "Applied top to bottom. Drag to reorder.", FontSize = TypeScale.Meta, VerticalAlignment = VerticalAlignment.Bottom };
        Themed(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { title, hint } };

        var list = new StackPanel
        {
            Name = GuardianEffectsName,
            Spacing = 10,
            Margin = new Thickness(RowBarWidth + RowHorizontalPadding, 12, 0, 12),
            Children = { notice, nameRow, head, strips },
        };

        _underRow = (list, string.Join(" ", GuardianVoice.Table.Select(effect => $"{effect.Label} {effect.Parameter}")));

        void ShowNotice(string text)
        {
            noticeTimer?.Stop();
            noticeText.Text = text;
            notice.IsVisible = true;
            noticeTimer = new DispatcherTimer { Interval = GuardianNoticeDuration };
            noticeTimer.Tick += (_, _) =>
            {
                noticeTimer!.Stop();
                notice.IsVisible = false;
            };
            noticeTimer.Start();
        }

        void HideNotice()
        {
            noticeTimer?.Stop();
            notice.IsVisible = false;
        }

        void OpenNameRow(GuardianNameMode mode)
        {
            picker.IsOpen = false;
            HideNotice();
            nameMode = mode;

            var preset = GuardianPresets.Preset(_settings!.Current.Speech);
            var current = mode == GuardianNameMode.Rename ? GuardianPresets.Label(preset) : null;
            nameRowOldName = current;

            nameLabel.Text = mode == GuardianNameMode.Rename ? "New name" : "Preset name";
            nameField.Text = current ?? string.Empty;
            nameAction.Content = mode == GuardianNameMode.Rename ? "Rename" : "Save";
            AutomationProperties.SetName(nameAction, mode == GuardianNameMode.Rename ? "Rename" : "Save");
            nameMessage.IsVisible = false;
            nameRow.IsVisible = true;
            nameField.Focus();
            nameField.SelectAll();
        }

        void CloseNameRow()
        {
            nameMode = GuardianNameMode.None;
            nameRow.IsVisible = false;
            nameMessage.IsVisible = false;
        }

        void Commit()
        {
            if (nameMode == GuardianNameMode.None)
            {
                return;
            }

            var speech = _settings!.Current.Speech;
            var result = nameMode == GuardianNameMode.Rename
                ? GuardianPresets.Rename(speech, nameField.Text)
                : GuardianPresets.Save(speech, nameField.Text);

            if (result.Refusal is { } refusal)
            {
                nameMessage.Text = refusal;
                nameMessage.IsVisible = true;
                return;
            }

            var trimmed = nameField.Text!.Trim();
            var said = nameMode == GuardianNameMode.Rename ? $"Renamed {nameRowOldName} to {trimmed}." : $"Saved {trimmed}.";
            var reason = nameMode == GuardianNameMode.Rename ? "Rename Guardian voice preset" : "Save Guardian voice preset";

            _settings!.Replace(reason, s => s with { Speech = result.Settings! });

            CloseNameRow();
            ShowNotice(said);
            Refresh();
        }

        nameAction.Click += (_, _) => Commit();
        nameCancel.Click += (_, _) => CloseNameRow();

        nameField.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    Commit();
                    break;

                case Key.Escape:
                    e.Handled = true;
                    CloseNameRow();
                    break;
            }
        };

        return (control, () =>
        {
            var speech = _settings!.Current.Speech;
            var preset = GuardianPresets.Preset(speech);
            var choices = GuardianPresets.Choices(speech);
            var saved = choices.Where(id => GuardianPresets.Find(id) is null && id != GuardianPresets.CustomId).ToList();

            var isBuiltin = GuardianPresets.Find(preset) is not null;
            var isSavedPreset = saved.Contains(preset);

            saveAs.IsVisible = !isSavedPreset;
            saveAs.IsEnabled = !isBuiltin;
            rename.IsVisible = isSavedPreset;

            saveAsFill?.Dispose();
            saveAsInk?.Dispose();
            saveAsFill = nameMode == GuardianNameMode.Create ? Themed(saveAs, Button.BackgroundProperty, ThemeManager.AKey) : null;
            saveAsInk = nameMode == GuardianNameMode.Create ? Themed(saveAs, Button.ForegroundProperty, ThemeManager.KnockKey) : null;

            renameFill?.Dispose();
            renameInk?.Dispose();
            renameFill = nameMode == GuardianNameMode.Rename ? Themed(rename, Button.BackgroundProperty, ThemeManager.AKey) : null;
            renameInk = nameMode == GuardianNameMode.Rename ? Themed(rename, Button.ForegroundProperty, ThemeManager.KnockKey) : null;

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

        var dots = new List<Border>();
        var pattern = new Canvas
        {
            Width = 9,
            Height = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        for (var i = 0; i < 6; i++)
        {
            var dot = new Border { Width = 3, Height = 3 };
            Canvas.SetLeft(dot, i % 2 * 6);
            Canvas.SetTop(dot, i / 2 * 6);
            pattern.Children.Add(dot);
            dots.Add(dot);
        }

        var handle = new Border
        {
            Name = GuardianHandleName,
            Width = GuardianHandleWidth,
            Focusable = true,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Child = pattern,
        };
        ToolTip.SetTip(handle, GuardianHandleTip);
        AutomationProperties.SetName(handle, $"Move {effect.Label}");

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

        var made = new GuardianStrip(effect.Id, strip, handle, dots, number, box, level, value);
        PaintGuardianHandle(made);

        return made;

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

    /// <summary>
    /// Dragging the handle moves the other strips aside and renumbers them as the pointer moves, and writes the
    /// order once, on drop. Up and Down on the focused handle move the effect one place and keep focus on it.
    /// </summary>
    private void WireGuardianHandle(
        GuardianStrip strip, StackPanel strips, Dictionary<string, GuardianStrip> byId, TextBlock message)
    {
        var handle = strip.Handle;
        GuardianDrag? drag = null;

        handle.PointerEntered += (_, _) => Hover(true);
        handle.PointerExited += (_, _) => Hover(false);
        handle.GotFocus += (_, _) => PaintGuardianHandle(strip);
        handle.LostFocus += (_, _) => PaintGuardianHandle(strip);

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;
            handle.Focus();
            e.Pointer.Capture(handle);

            var from = strips.Children.IndexOf(strip.Strip);
            drag = new GuardianDrag
            {
                From = from,
                To = from,
                StartY = e.GetPosition(strips).Y,
                Pitch = strip.Strip.Bounds.Height + strips.Spacing,
            };

            strip.Dragging = true;
            strip.Strip.ZIndex = 1;
            strip.Strip.Opacity = GuardianDraggedOpacity;
            PaintGuardianHandle(strip);
        };

        handle.PointerMoved += (_, e) =>
        {
            if (drag is null)
            {
                return;
            }

            var dy = e.GetPosition(strips).Y - drag.StartY;
            var last = strips.Children.Count - 1;
            drag.To = drag.Pitch > 0 ? Math.Clamp(drag.From + (int)Math.Round(dy / drag.Pitch), 0, last) : drag.From;
            strip.Strip.RenderTransform = new TranslateTransform(0, dy);

            for (var j = 0; j <= last; j++)
            {
                var other = strips.Children[j];

                if (other == strip.Strip)
                {
                    continue;
                }

                var shift = drag.From < drag.To && j > drag.From && j <= drag.To ? -1
                    : drag.To < drag.From && j >= drag.To && j < drag.From ? 1
                    : 0;

                other.RenderTransform = shift == 0 ? null : new TranslateTransform(0, shift * drag.Pitch);

                if (other.Tag is string id && byId.TryGetValue(id, out var shifted))
                {
                    Number(shifted, j + shift);
                }
            }

            Number(strip, drag.To);
        };

        handle.PointerReleased += (_, e) =>
        {
            if (drag is not { } done)
            {
                return;
            }

            drag = null;
            e.Pointer.Capture(null);
            Settle();

            if (done.To != done.From)
            {
                WriteGuardianOrder(strips, done.From, done.To, message);
            }
        };

        handle.PointerCaptureLost += (_, _) =>
        {
            if (drag is not null)
            {
                drag = null;
                Settle();
            }
        };

        handle.KeyDown += (_, e) =>
        {
            var by = e.Key switch
            {
                Key.Up => -1,
                Key.Down => 1,
                _ => 0,
            };

            if (by == 0 || drag is not null)
            {
                return;
            }

            e.Handled = true;
            var from = strips.Children.IndexOf(strip.Strip);
            var to = from + by;

            if (to < 0 || to >= strips.Children.Count)
            {
                return;
            }

            WriteGuardianOrder(strips, from, to, message);
            handle.Focus(NavigationMethod.Directional);
        };

        void Hover(bool on)
        {
            strip.Hovered = on;
            PaintGuardianHandle(strip);
        }

        void Settle()
        {
            strip.Dragging = false;
            strip.Strip.ZIndex = 0;
            strip.Strip.Opacity = 1;

            for (var j = 0; j < strips.Children.Count; j++)
            {
                strips.Children[j].RenderTransform = null;

                if (strips.Children[j].Tag is string id && byId.TryGetValue(id, out var placed))
                {
                    Number(placed, j);
                }
            }

            PaintGuardianHandle(strip);
        }

        static void Number(GuardianStrip strip, int index) =>
            strip.Number.Text = (index + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Writes the order shown, with the effect at <paramref name="from"/> moved to <paramref name="to"/>.</summary>
    private void WriteGuardianOrder(StackPanel strips, int from, int to, TextBlock message)
    {
        var ids = strips.Children.Select(child => (string)child.Tag!).ToList();
        var moved = ids[from];
        ids.RemoveAt(from);
        ids.Insert(to, moved);

        Apply(SpeechCapability.GuardianOrderKey, string.Join(",", ids), message);
    }

    /// <summary>
    /// Paints a strip and its handle: the handle clear at rest and tile2 on hover or focus; while dragging, the
    /// handle solid A with knock dots on a tile2 strip.
    /// </summary>
    private void PaintGuardianHandle(GuardianStrip strip)
    {
        strip.StripInk?.Dispose();
        strip.StripInk = Themed(
            strip.Strip, Border.BackgroundProperty, strip.Dragging ? ThemeManager.Tile2Key : ThemeManager.TileKey);

        strip.HandleInk?.Dispose();
        strip.HandleInk = null;
        strip.Handle.Background = Brushes.Transparent;

        if (strip.Dragging)
        {
            strip.HandleInk = Themed(strip.Handle, Border.BackgroundProperty, ThemeManager.AKey);
        }
        else if (strip.Hovered || strip.Handle.IsFocused)
        {
            strip.HandleInk = Themed(strip.Handle, Border.BackgroundProperty, ThemeManager.Tile2Key);
        }

        foreach (var ink in strip.DotInks)
        {
            ink.Dispose();
        }

        strip.DotInks.Clear();

        foreach (var dot in strip.Dots)
        {
            strip.DotInks.Add(
                Themed(dot, Border.BackgroundProperty, strip.Dragging ? ThemeManager.KnockKey : ThemeManager.AKey));
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

    /// <summary>The notice shown below the preset row after a save or rename, hidden until then.</summary>
    private Border GuardianNotice(out TextBlock text)
    {
        text = new TextBlock
        {
            Name = "GuardianNoticeText",
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var notice = new Border
        {
            Name = GuardianNoticeName,
            MinHeight = 36,
            Padding = new Thickness(15, 0, 12, 0),
            BorderThickness = new Thickness(3, 0, 0, 0),
            IsVisible = false,
            Child = text,
        };
        Themed(notice, Border.BackgroundProperty, ThemeManager.SlabKey);
        Themed(notice, Border.BorderBrushProperty, ThemeManager.CyanKey);

        return notice;
    }

    /// <summary>
    /// The name row: "Preset name"/"New name", a text field, SAVE or RENAME, CANCEL and the refusal
    /// message line. Hidden until a preset button opens it.
    /// </summary>
    private (StackPanel Row, TextBlock Label, TextBox Field, Button Action, TextBlock Message, Button Cancel) GuardianNameRow()
    {
        var label = new TextBlock
        {
            Name = GuardianNameLabelName,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            VerticalAlignment = VerticalAlignment.Center,
            Width = LabelColumnWidth,
        };
        Themed(label, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var field = new TextBox
        {
            Name = GuardianNameFieldName,
            MaxLength = 32,
            PlaceholderText = "Name this preset",
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        var action = GuardianTileButton("Save");
        action.Name = GuardianNameActionName;

        var cancel = GuardianTileButton("Cancel");
        cancel.Name = GuardianNameCancelName;
        AutomationProperties.SetName(cancel, "Cancel preset name");

        var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 2 };
        Grid.SetColumn(field, 0);
        Grid.SetColumn(action, 1);
        Grid.SetColumn(cancel, 2);
        controls.Children.Add(field);
        controls.Children.Add(action);
        controls.Children.Add(cancel);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LabelColumnWidth},{RowColumnGap},*") };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(controls, 2);
        head.Children.Add(label);
        head.Children.Add(controls);

        var message = new TextBlock
        {
            Name = GuardianNameMessageName,
            FontSize = TypeScale.Secondary,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        Themed(message, TextBlock.ForegroundProperty, ThemeManager.RedKey);

        var row = new StackPanel
        {
            Name = GuardianNameRowName,
            IsVisible = false,
            Spacing = 6,
            Children = { head, message },
        };

        return (row, label, field, action, message, cancel);
    }

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
