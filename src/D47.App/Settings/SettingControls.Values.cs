using System.Diagnostics;
using System.Globalization;
using D47.Core.Listening;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Persona;

namespace D47.App.Settings;

internal sealed partial class SettingControls
{
    private (Control, Action) BuildToggle(SettingRow row, StatusLine message)
    {
        var toggle = new CheckBox { Margin = new Thickness(0) };
        toggle.Classes.Add("bare");

        toggle.IsCheckedChanged += (_, _) =>
        {
            if (!host.Refreshing())
            {
                host.Apply(row.Key, toggle.IsChecked == true ? "true" : "false", message);
            }
        };

        return (toggle, () => toggle.IsChecked = host.Settings.Read(row.Key) is "true");
    }

    private (Control, Action) BuildChoice(SettingRow row, StatusLine message)
    {
        // Through ChoicesFor, not the bare list.
        var choices = row.ChoicesFor(host.Settings.Current);

        // A clear item only where clearing means something.
        var clearable = row.IsClearable;

        var items = new List<string>();
        if (clearable)
        {
            items.Add(row.BareDefaultFor(host.Settings.Current) is { } bare ? $"(default: {bare})" : "(default)");
        }

        // One describer for the whole list rather than one call per item: a row may label a choice against
        // the others beside it — the model rows mark the cheapest of what is offered — and that is a property
        // of the list, not of the line (#152).
        items.AddRange(choices.Select(row.DescriberFor(host.Settings.Current)));

        // A row whose list comes from ChoiceSource can grow past a segment row's four between one Refresh
        // and the next, so it is always a stepper (#274).
        // Live: whether a provider's key is stored can change while the page is open.
        List<ChoiceStatus?> Statuses() =>
            [.. clearable ? [null] : Array.Empty<ChoiceStatus?>(), .. choices.Select(choice => row.StatusFor(choice, host.Settings.Current))];

        var (view, combo) = Choice.Build(
            items, selectedIndex: -1, alwaysStepper: row.ChoiceSource is not null, statuses: Statuses());

        // The position and what stepping onto a value costs, both shown only on a stepper (#336).
        if (view is Stepper stepper)
        {
            var consequences = new List<string?>();
            if (clearable)
            {
                consequences.Add(null);
            }

            consequences.AddRange(choices.Select(row.ConsequenceFor));
            stepper.Consequences = consequences;
        }

        view.HorizontalAlignment = HorizontalAlignment.Right;
        view.MinWidth = SettingsView.StandardControlWidth;
        SettingsView.DressAsAChoice(view);
        AutomationProperties.SetName(view, row.Label);

        var offset = clearable ? 1 : 0;

        // The rows that download something carry a progress bar, and only those.
        var downloads = string.Equals(row.Key, ListeningCapability.ModelKey, StringComparison.Ordinal)
                        || row.FetchChoiceAsync is not null;

        var bar = new ProgressBar
        {
            Height = 3,
            Minimum = 0,
            Maximum = 1,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
        };

        // A row whose change costs something stages the pressed choice, and only this button applies it,
        // so stepping past a value never fetches it (#274).
        var confirm = new Button
        {
            Name = "ApplyStaged",
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        };

        var stagedNote = new TextBlock
        {
            Text = "Staged. What is in use keeps running until you press this.",
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
        };

        Themed(stagedNote, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        string? staged = null;

        void Unstage()
        {
            confirm.IsVisible = false;
            stagedNote.IsVisible = false;
        }

        void Take(string? chosen)
        {
            // One handler with a branch rather than two handlers.
            if (downloads)
            {
                _ = FetchModelAsync(row, chosen, view, combo, bar, message);
                return;
            }

            host.Apply(row.Key, chosen, message);
        }

        confirm.Click += (_, _) =>
        {
            Unstage();
            Take(staged);
        };

        combo.SelectionChanged += (_, _) =>
        {
            if (host.Refreshing() || combo.SelectedIndex < 0)
            {
                return;
            }

            var chosen = clearable && combo.SelectedIndex == 0
                ? null
                : choices[combo.SelectedIndex - offset];

            if (row.ConfirmLabel is not { } confirmLabel)
            {
                Take(chosen);
                return;
            }

            // Stepping back to what is in use has nothing left to apply.
            if (string.Equals(chosen, host.Settings.Read(row.Key), StringComparison.OrdinalIgnoreCase))
            {
                Unstage();
                return;
            }

            staged = chosen;
            confirm.Content = chosen is null ? "Use the default" : confirmLabel(chosen);
            confirm.IsVisible = true;
            stagedNote.IsVisible = true;
        };

        Control control = downloads || row.ConfirmLabel is not null
            ? new StackPanel { Children = { view, stagedNote, confirm, bar } }
            : view;

        return (control, () =>
        {
            Unstage();

            var statuses = Statuses();
            switch (view)
            {
                case Segment segment when !statuses.SequenceEqual(segment.Statuses):
                    segment.Statuses = statuses;
                    break;

                case Stepper stepper when !statuses.SequenceEqual(stepper.Statuses):
                    stepper.Statuses = statuses;
                    break;
            }

            var value = host.Settings.Read(row.Key);
            var found = value is null
                ? -1
                : choices.Select((choice, i) => (choice, i))
                    .Where(pair => string.Equals(pair.choice, value, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => (int?)pair.i)
                    .FirstOrDefault() ?? -1;

            combo.SelectedIndex = found < 0 ? (clearable ? 0 : -1) : found + offset;
        });
    }

    /// <summary>Applies a speech model choice, downloading it first if it is not on disk.</summary>
    private async Task FetchModelAsync(
        SettingRow row,
        string? chosen,
        TemplatedControl view,
        IChoiceControl combo,
        ProgressBar bar,
        StatusLine message)
    {
        if (_downloadingModel)
        {
            return;
        }

        // A row that carries its own fetch (#139).
        if (row.FetchChoiceAsync is { } fetch)
        {
            await FetchChoiceAsync(row, chosen, view, combo, bar, message, fetch);
            return;
        }

        var model = WhisperModels.Find(chosen);

        // None, or no downloader behind this view: an ordinary setting with nothing to fetch.
        if (model is null || services.DownloadModel is null)
        {
            host.Apply(row.Key, chosen, message);
            return;
        }

        _downloadingModel = true;

        // Shut while it runs.
        view.IsEnabled = false;

        bar.Value = 0;
        bar.IsVisible = true;

        message.Say($"Fetching {model.Label} - about {model.ApproximateMegabytes} MB.");

        try
        {
            // A model already on disk comes straight back as AlreadyPresent, so there is no need to ask the
            // store separately whether this is a download at all.
            var progress = new Progress<ModelProgress>(report => bar.Value = report.Fraction);
            var result = await services.DownloadModel(model, progress);

            if (result.Outcome is ModelInstall.Installed or ModelInstall.AlreadyPresent)
            {
                // Written only now that the file is there, so the row can never name a model d47 cannot load.
                host.Apply(row.Key, chosen, message);

                // Nothing left to say.
                message.Clear();
                return;
            }

            host.Refresh();
            message.Fail(result.Detail ?? $"{model.Id} was not downloaded.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            host.Refresh();
            message.Fail($"{model.Id} could not be downloaded: {ex.Message}");
        }
        finally
        {
            _downloadingModel = false;
            view.IsEnabled = true;
            bar.IsVisible = false;
        }
    }

    /// <summary>The same flow for a row that carries its own fetch (#139).</summary>
    private async Task FetchChoiceAsync(
        SettingRow row,
        string? chosen,
        TemplatedControl view,
        IChoiceControl combo,
        ProgressBar bar,
        StatusLine message,
        Func<string?, IProgress<double>, CancellationToken, Task<string?>> fetch)
    {
        _downloadingModel = true;
        view.IsEnabled = false;

        bar.Value = 0;
        bar.IsVisible = true;

        message.Say($"Fetching {row.LabelForChoice(chosen ?? string.Empty, host.Settings.Current)}.");

        try
        {
            var progress = new Progress<double>(fraction => bar.Value = fraction);
            var failure = await fetch(chosen, progress, CancellationToken.None);

            if (failure is null)
            {
                host.Apply(row.Key, chosen, message);

                // Nothing left to say, for the reason the speech model row records: a change that worked is
                // visible in the control that made it.
                message.Clear();
                return;
            }

            host.Refresh();
            message.Fail(failure);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            host.Refresh();
            message.Fail($"That could not be downloaded: {ex.Message}");
        }
        finally
        {
            _downloadingModel = false;
            view.IsEnabled = true;
            bar.IsVisible = false;
        }
    }

    /// <summary>
    /// A list judged when the page is built. A row that stages its choice or downloads one keeps its stepper,
    /// since the picker page commits on the press.
    /// </summary>
    private bool IsLongList(SettingRow row) =>
        row.ConfirmLabel is null
        && row.FetchChoiceAsync is null
        && !string.Equals(row.Key, ListeningCapability.ModelKey, StringComparison.Ordinal)
        && row.ChoicesFor(host.Settings.Current).Count > SettingsView.LongListThreshold;

    private (Control, Action) BuildDropdownTile(SettingRow row, StatusLine message) => BuildDropdownTile(row, message, out _);

    /// <summary>
    /// The tile that opens the picker page. On a row with <see cref="SettingRow.ChoiceClips"/>, while its slot is on
    /// Chatterbox, it shows the voice's name, bands and clip state, and <paramref name="below"/> says who speaks while
    /// the chosen voice is not on this PC.
    /// </summary>
    private (Control, Action) BuildDropdownTile(SettingRow row, StatusLine message, out Control? below)
    {
        var (button, value, status) = SettingsView.DropdownTile(row.Label, out var state);

        var busy = new BusyGlyph
        {
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(busy, BusyGlyph.StrokeProperty, ThemeManager.AKey);

        button.Click += async (_, _) => await ChooseAsync(row, button, busy, message);

        // A DockPanel, not a horizontal StackPanel, which measures with no width limit and let the button run
        // past the column. The fixed width is capped to the control column by the row.
        var withBusy = new DockPanel { Width = Stepper.MaximumWidth };
        DockPanel.SetDock(busy, Dock.Left);
        withBusy.Children.Add(busy);
        withBusy.Children.Add(button);

        IDisposable? statusInk = null;
        IDisposable? stateInk = null;

        var away = row.ChoiceClips is null ? null : new VoiceAwayLine(this, () => ChooseAsync(row, button, busy, message));
        below = away?.View;

        void Refresh()
        {
            var current = host.Settings.Read(row.Key);
            var clips = current is null ? null : row.ChoiceClips?.Invoke(host.Settings.Current);
            var clip = current is null ? null : clips?.Clip(current);

            value.Text = current is null
                ? $"({row.BareDefaultFor(host.Settings.Current) ?? "not set"})"
                : clip is not null && clips!.Name?.Invoke(current) is { } name
                    ? name
                    : row.LabelForChoice(current, host.Settings.Current);

            Themed(value, TextBlock.ForegroundProperty, current is null ? ThemeManager.Grey2Key : ThemeManager.WhiteKey);

            statusInk?.Dispose();
            statusInk = null;
            stateInk?.Dispose();
            stateInk = null;

            if (clip is not null)
            {
                var bands = clips!.Detail?.Invoke(current!);

                status.Text = bands?.ToUpperInvariant();
                status.IsVisible = bands is not null;
                statusInk = Themed(status, TextBlock.ForegroundProperty, ThemeManager.AKey);

                state.Text = clip.State.Text.ToUpperInvariant();
                state.IsVisible = true;
                stateInk = state.Bind(TextBlock.ForegroundProperty, Stepper.Ink(clip.State.Tone));
            }
            else if (current is not null && row.StatusFor(current, host.Settings.Current) is { } line)
            {
                status.Text = line.Text;
                status.IsVisible = true;
                statusInk = status.Bind(TextBlock.ForegroundProperty, Stepper.Ink(line.Tone));
                state.IsVisible = false;
            }
            else
            {
                status.IsVisible = false;
                state.IsVisible = false;
            }

            away?.Show(clip is null ? null : clips, current);
        }

        away?.Watching(Refresh);

        return (withBusy, Refresh);
    }

    /// <summary>
    /// The line under a Chatterbox voice row while the chosen voice is not on this PC: fetching or not fetched with
    /// the stand-in named, or a notice when the fetch failed. Its tiles are built once and shown by state.
    /// </summary>
    private sealed class VoiceAwayLine
    {
        private readonly Border _line;
        private readonly DockPanel _words;
        private readonly TextBlock _head;
        private readonly TextBlock _mono;
        private readonly TextBlock _text;
        private readonly Button _fetchNow;
        private readonly Notice _failed;
        private IDisposable? _headInk;

        private SettingChoiceClips? _clips;
        private string? _id;
        private Action? _unwatch;
        private Action? _refresh;

        public VoiceAwayLine(SettingControls owner, Func<Task> chooseAnother)
        {
            _head = new TextBlock
            {
                Name = "VoiceAwayHead",
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.Meta,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };

            _mono = new TextBlock
            {
                Name = "VoiceAwayMono",
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Meta,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            owner.Themed(_mono, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            _text = new TextBlock
            {
                Name = "VoiceAwayText",
                FontFamily = new FontFamily(Fonts.ProseFamily),
                FontSize = TypeScale.Tip,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            owner.Themed(_text, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            _fetchNow = new Button { Content = "Fetch now", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _fetchNow.Click += (_, _) => Fetch();

            _words = new DockPanel();
            DockPanel.SetDock(_head, Dock.Left);
            DockPanel.SetDock(_mono, Dock.Left);
            DockPanel.SetDock(_fetchNow, Dock.Right);
            _words.Children.Add(_head);
            _words.Children.Add(_mono);
            _words.Children.Add(_fetchNow);
            _words.Children.Add(_text);

            var retry = new Button { Content = "Retry" };
            retry.Click += (_, _) => Fetch();

            var another = new Button { Content = "Choose another" };
            another.Click += async (_, _) => await chooseAnother();

            _failed = new Notice(NoticeLevel.Error, inline: true) { Name = "VoiceAwayNotice" };
            _failed.Actions.Add(retry);
            _failed.Actions.Add(another);

            _line = new Border
            {
                Name = "VoiceAway",
                Padding = new Thickness(15, 10, 60, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = new Avalonia.Controls.Panel { Children = { _words, _failed } },
                IsVisible = false,
            };
            owner.Themed(_line, Border.BorderBrushProperty, ThemeManager.Line2Key);

            // The provider outlives the settings view, so a row off screen stops listening.
            _line.AttachedToVisualTree += (_, _) => Watch();
            _line.DetachedFromVisualTree += (_, _) => Unwatch();
        }

        public Control View => _line;

        /// <summary>The row's refresh, run on the UI thread when the shown voice's clip changes.</summary>
        public void Watching(Action refresh) => _refresh = refresh;

        /// <summary>Shows the line for <paramref name="id"/>; hidden while it is on this PC or the slot is not on Chatterbox.</summary>
        public void Show(SettingChoiceClips? clips, string? id)
        {
            _clips = clips;
            _id = id;

            if (clips is null)
            {
                Unwatch();
            }
            else if (_line.IsAttachedToVisualTree())
            {
                Watch();
            }

            _headInk?.Dispose();
            _headInk = null;

            if (id is null || clips?.Away(id) is not { } away)
            {
                _line.IsVisible = false;
                return;
            }

            _line.IsVisible = true;

            var failed = away.Clip == ChatterboxClip.Failed;
            _failed.IsVisible = failed;
            _words.IsVisible = !failed;

            if (failed)
            {
                _failed.Label = away.Label;
                _failed.Text = away.Text;
                _failed.Detail = away.Reason;
                return;
            }

            _head.Text = away.Head;
            _headInk = _head.Bind(
                TextBlock.ForegroundProperty,
                Stepper.Ink(away.Clip == ChatterboxClip.Fetching ? ChoiceTone.White : ChoiceTone.Grey));
            _mono.Text = away.Mono;
            _mono.IsVisible = away.Mono is not null;
            _text.Text = away.Text;
            _fetchNow.IsVisible = away.Clip == ChatterboxClip.NotFetched;
        }

        private void Fetch()
        {
            if (_id is { } id)
            {
                _clips?.Fetch?.Invoke(id);
            }
        }

        private void Watch()
        {
            if (_unwatch is not null || _clips?.Watch is not { } watch)
            {
                return;
            }

            _unwatch = watch(id => Dispatcher.UIThread.Post(() =>
            {
                if (string.Equals(id, _id, StringComparison.OrdinalIgnoreCase))
                {
                    _refresh?.Invoke();
                }
            }));
        }

        private void Unwatch()
        {
            _unwatch?.Invoke();
            _unwatch = null;
        }
    }

    private (Control, Action) BuildNumber(SettingRow row, StatusLine message)
    {
        if (SettingsView.IsLevel(row))
        {
            return BuildLevel(row, message);
        }

        // Both from the row, so the control cannot offer a precision the store will not keep, or a press the
        // store is only going to clamp away.
        var amount = new Amount
        {
            Step = (decimal)row.Step,
            Format = row.NumberFormat,
            Unit = row.Unit,
            Minimum = row.Minimum is { } low ? (decimal)low : null,
            Maximum = row.Maximum is { } high ? (decimal)high : null,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        amount.ValueCommitted += (_, _) =>
        {
            if (!host.Refreshing())
            {
                host.Apply(
                    row.Key,
                    amount.Value?.ToString(row.NumberFormat, CultureInfo.InvariantCulture),
                    message);
            }
        };

        return (amount, () =>
        {
            amount.Value = decimal.TryParse(
                host.Settings.Read(row.Key),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : null;
            amount.Placeholder = row.DefaultDisplayFor(host.Settings.Current);
        });
    }

    private (Control, Action) BuildLevel(SettingRow row, StatusLine message)
    {
        var level = new Level
        {
            Minimum = row.Minimum ?? 0,
            Maximum = 1,
            Step = row.Step,
            Width = Level.CompactWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        level.ValueChanged += (_, _) =>
        {
            if (!host.Refreshing())
            {
                host.Apply(
                    row.Key,
                    level.Value.ToString(row.NumberFormat, CultureInfo.InvariantCulture),
                    message);
            }
        };

        return (level, () =>
        {
            var stored = host.Settings.Read(row.Key) ?? row.DefaultValueFor(host.Settings.Current);

            level.Value = double.TryParse(
                stored,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 1;
        });
    }

    private (Control, Action) BuildText(SettingRow row, StatusLine message)
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

        // Applied on leaving the box rather than on every keystroke: a setting that persists per character
        // would write a file per character, and would reject half-typed URLs as it went.
        box.LostFocus += (_, _) =>
        {
            if (!host.Refreshing())
            {
                host.Apply(row.Key, box.Text, message);
            }
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !row.Multiline)
            {
                e.Handled = true;
                host.Apply(row.Key, box.Text, message);
            }
        };

        SystemNameSuggestions? names = null;

        if (row.NamesASystem && services.SystemNames is { } lookup)
        {
            names = new SystemNameSuggestions(
                box,
                lookup.Lookup,
                lookup.Enabled,
                match =>
                {
                    names!.SetText(match.Name);
                    host.Apply(row.Key, match.Name, message);
                },
                lookup.Pause);
        }

        Control control = names is null
            ? box
            : new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Left, Children = { box, names.List } };

        return (control, () =>
        {
            // Not while the Commander is typing in it: leaving the box saves what they typed.
            if (!box.IsFocused)
            {
                var stored = host.Settings.Read(row.Key) ?? string.Empty;

                if (names is null)
                {
                    box.Text = stored;
                }
                else
                {
                    names.SetText(stored);
                }
            }

            // The default is a placeholder, never a value, so "I have not chosen" stays distinguishable from
            // "I chose the default" (Phase 4).
            box.PlaceholderText = row.DefaultDisplayFor(host.Settings.Current) ?? string.Empty;
        });
    }

    /// <summary>
    /// The key row, which is <see cref="SecretEditor"/> — the same control the first-run guide shows
    /// (Phase 16).
    /// </summary>
    private (Control, Action) BuildSecret(SettingRow row, StatusLine message)
    {
        // The editor reports its own failures inline, next to the box that caused them, so the row's shared
        // message line stays for everything else.
        message.IsVisible = false;

        var editor = new SecretEditor(row, host.Settings);

        // A stored key changes what other rows can offer — the voice picker is the obvious one — so the
        // surface re-reads itself rather than waiting for the next open.
        editor.Changed += host.Refresh;

        return (editor, editor.Refresh);
    }

    /// <summary>
    /// The one bind control (#217): a chip per bound key, or one reading NONE, then BIND and CLEAR, in a row
    /// that wraps (#354). Chips are shown or hidden rather than rebuilt, so a capture in progress keeps its
    /// own control.
    /// </summary>
    private (Control, Action) BuildBind(SettingRow row, StatusLine message)
    {
        var chips = row.BoundKeys.Select(_ => SettingsView.BindingChip()).ToList();

        var bind = new Button { Content = "BIND" };
        bind.Click += async (_, _) => await CaptureBindAsync(row, bind, message);

        var clear = new Button { Content = "CLEAR" };

        clear.Click += (_, _) =>
        {
            foreach (var key in row.BoundKeys)
            {
                host.Apply(key, null, message);
            }
        };

        var wrap = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (chip, _) in chips)
        {
            wrap.Children.Add(chip);
        }
        wrap.Children.Add(bind);
        wrap.Children.Add(clear);

        return (wrap, () =>
        {
            var said = BoundAs(row);
            var bound = said.Any(text => text is not null);

            // A row that can only be filled from a controller is dead without one, and saying so beats a
            // button that does nothing.
            var dead = row.Kind == SettingKind.HotasButton && services.Switches is null;

            for (var i = 0; i < chips.Count; i++)
            {
                var (chip, text) = chips[i];
                var empty = !bound && i == 0;

                chip.IsVisible = said[i] is not null || empty;
                text.Text = empty ? (dead ? "NO CONTROLLERS" : "NONE") : said[i]?.ToUpperInvariant();
                text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(
                    empty ? ThemeManager.Grey2Key : ThemeManager.WhiteKey);
            }

            bind.IsEnabled = !dead;
            clear.IsEnabled = bound;
        });
    }

    /// <summary>
    /// What each of a bind row's <see cref="SettingRow.BoundKeys"/> is bound to, in the Commander's
    /// words, aligned by index — null where that slot holds nothing.
    /// </summary>
    private IReadOnlyList<string?> BoundAs(SettingRow row) => row.BoundKeys.Select(key =>
    {
        var stored = host.Settings.Read(key);

        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        return KindOf(key) == SettingKind.HotasButton
            ? D47.Core.Hotas.HotasButton.Parse(stored)?.Describe() ?? stored
            : Gestures.Describe(stored);
    }).ToList();

    private SettingKind KindOf(string key) => host.Settings.Find(key)?.Kind ?? SettingKind.Hotkey;

    private async Task ChooseAsync(SettingRow row, Button button, BusyGlyph busy, StatusLine message)
    {
        // The surface this view is drawn on, so the page opens there: the window's panel or the headset's.
        if (button.FindAncestorOfType<Panel.PanelView>()?.Prompts is not { } prompts)
        {
            return;
        }

        // The work is "until the list is on screen", not "until the Commander has chosen": what can take a
        // moment is asking the machine for its capture devices or a provider for its voices.
        var listed = new TaskCompletionSource();
        var clips = row.ChoiceClips?.Invoke(host.Settings.Current);

        prompts.Pick(
            $"settings-pick:{row.Key}",
            row.Label,
            new PickerRequest
            {
                Prompt = row.Label,

                // A voice row states what a press costs instead; every other row's help is readable here,
                // where a ray cannot reach the label's hover text.
                Help = row.Audition is null ? row.Help : clips?.Help,
                Clips = clips,
                Choices = row.ChoicesFor(host.Settings.Current),

                // Read at open like the choices themselves: a label can depend on the provider serving the
                // list right now, and on the rest of the list beside it (#152).
                Describe = row.DescriberFor(host.Settings.Current),
                Current = host.Settings.Read(row.Key),
                DefaultDisplay = row.IsClearable ? row.BareDefaultFor(host.Settings.Current) : null,
                AllowsFreeText = row.AllowsFreeText,
                WhyEmpty = row.WhyNoChoicesFor(host.Settings.Current),

                // Read at open like the choices themselves, and for the same reason: which properties the
                // list has depends on the provider serving it right now (#146).
                Facet = row.Facet?.Invoke(host.Settings.Current),
                Facets = row.Facets?.Invoke(host.Settings.Current),
                ChoiceNoun = row.ChoiceNoun ?? "choices",

                // Read at open rather than captured once, because both the price and the reason it might be
                // unavailable follow the selected provider.
                Audition = row.Audition is { } audition
                    ? new PickerAudition
                    {
                        Play = audition.Play,
                        Preview = audition.Preview,
                        HasPreview = audition.HasPreview,
                        Cost = audition.Cost(host.Settings.Current),
                        LineCost = audition.LineCost?.Invoke(host.Settings.Current),
                        Unavailable = audition.Unavailable?.Invoke(host.Settings.Current),
                    }
                    : null,
            },
            result =>
            {
                host.Apply(row.Key, result.Value, message);
                button.Focus();
            },
            onListed: () => listed.TrySetResult());

        await Busy.While(button, busy, () => listed.Task);
    }

    /// <summary>Binds by listening for a gesture, rather than by offering a list of key names.</summary>
    private async Task CaptureBindAsync(SettingRow row, Button button, StatusLine message)
    {
        if (TopLevel.GetTopLevel(button) is not { } top)
        {
            return;
        }

        var keys = row.Kind != SettingKind.HotasButton;

        // Whether a modifier pressed on its own is a binding here.
        var bare = keys && !row.SystemWide;

        // The stick is armed for a row that is one, or for a row naming one as its other half.
        var buttonKey = row.Kind == SettingKind.HotasButton
            ? row.Key
            : row.BoundKeys.FirstOrDefault(key => KindOf(key) == SettingKind.HotasButton);

        var stick = buttonKey is not null && services.Switches is not null;

        var previous = button.Content;

        button.Content = (keys, stick) switch
        {
            (true, true) => "Press a key or button…",
            (true, false) => "Press a key…",
            _ => "Press a button…",
        };

        try
        {
            if (await BindCapture.RunAsync(top, keys ? row.Key : null, bare, stick ? buttonKey : null, services.Switches, message)
                is { } caught)
            {
                host.Apply(caught.Key, caught.Value, message);
            }
        }
        finally
        {
            button.Content = previous;
            host.Refresh();
        }
    }
}
