using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Conversation;

namespace D47.App.Controls;

/// <summary>
/// The Your accounts section of the SPEND dialog: one block per provider in use, each holding what the provider
/// reports about the account. Reads start in <see cref="Start"/>, on the thread pool, and are cancelled by
/// <see cref="Cancel"/>.
/// </summary>
public sealed class AccountsSection : StackPanel
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly IReadOnlyList<IAccountReader> _readers;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeZoneInfo _zone;
    private readonly TextBlock _aside;
    private readonly Dictionary<AccountProvider, Border> _slots = [];

    private CancellationTokenSource? _reads;
    private bool _started;

    public AccountsSection(IReadOnlyList<IAccountReader> readers, Func<DateTimeOffset> now, TimeZoneInfo zone)
    {
        _readers = readers;
        _now = now;
        _zone = zone;

        Name = "SpendAccounts";
        Spacing = 4;

        _aside = new TextBlock
        {
            FontSize = TypeScale.ControlLarge,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(_aside, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Children.Add(StarSystemPage.GroupHead("Your accounts", _aside));

        foreach (var reader in readers)
        {
            var slot = new Border { Name = $"Account{reader.Provider}", Child = Waiting(reader.Provider) };
            _slots[reader.Provider] = slot;
            slot.Margin = new Thickness(0, 4, 0, 0);
            Children.Add(slot);
        }
    }

    /// <summary>Starts every read; the section's aside gives the local time they were started.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _reads = new CancellationTokenSource();

        var at = _now();
        _aside.Text = $"As each provider reports it · read {Local(at).ToString("HH:mm", Culture)}";

        foreach (var reader in _readers)
        {
            Read(reader, _reads.Token);
        }
    }

    /// <summary>Cancels any read still running.</summary>
    public void Cancel()
    {
        var reads = _reads;
        _reads = null;

        if (reads is null)
        {
            return;
        }

        reads.Cancel();
        reads.Dispose();
    }

    private void Read(IAccountReader reader, CancellationToken token)
    {
        _slots[reader.Provider].Child = Waiting(reader.Provider);

        _ = Task.Run(async () =>
        {
            AccountReading reading;

            try
            {
                reading = await reader.ReadAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                reading = new AccountReading.Unreachable(exception.Message);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    _slots[reader.Provider].Child = Draw(reader, reading);
                }
            });
        }, token);
    }

    private Control Draw(IAccountReader reader, AccountReading reading)
    {
        var name = NameOf(reader.Provider);

        return reading switch
        {
            AccountReading.NoKey => Alert(
                NoticeLevel.Warning,
                $"No {name} key",
                $"Your settings use {name}, but no key is stored for it, so there is no account to read. Add the key in Settings."),
            AccountReading.Refused refused => Alert(
                NoticeLevel.Error,
                $"{name} refused the read",
                reader.Provider == AccountProvider.ElevenLabs
                    ? "ElevenLabs did not accept this key for reading the account. Speech is unaffected."
                    : "Deepgram did not accept this key for reading the balance. Transcription is unaffected.",
                Said(refused)),
            AccountReading.Unreachable unreachable => Unreachable(reader, name, unreachable),
            ElevenLabsFigures figures => ElevenLabs(figures),
            DeepgramCredit credit => Deepgram(credit),
            _ => Alert(NoticeLevel.Error, $"{name} not read", "D47 could not read this account."),
        };
    }

    private static string Said(AccountReading.Refused refused) =>
        refused.Message is { Length: > 0 } said ? $"HTTP {refused.Status} · {said}" : $"HTTP {refused.Status}";

    private static Notice Alert(NoticeLevel level, string label, string text, string? detail = null) =>
        new(level) { Label = label, Text = text, Detail = detail };

    private Control Unreachable(IAccountReader reader, string name, AccountReading.Unreachable unreachable)
    {
        var host = reader.Provider == AccountProvider.ElevenLabs ? "api.elevenlabs.io" : "api.deepgram.com";

        var retry = new Button
        {
            Name = $"Retry{reader.Provider}",
            Content = "Retry",
            MinWidth = 110,
            MinHeight = TypeScale.MinimumTarget,
        };

        retry.Click += (_, _) =>
        {
            if (_reads is { } reads)
            {
                Read(reader, reads.Token);
            }
        };

        var notice = Alert(
            NoticeLevel.Error,
            $"{name} not reached",
            $"D47 couldn't reach {host}. Check the connection, then try again.",
            unreachable.Message);
        notice.Actions.Add(retry);

        return notice;
    }

    private Control Waiting(AccountProvider provider)
    {
        var sentence = provider == AccountProvider.ElevenLabs
            ? "Reading your ElevenLabs account…"
            : "Reading your Deepgram balance…";

        var mark = new WaitingState(WaitForm.Line, sentence) { Name = $"Reading{provider}" };

        return Slab(NameOf(provider), source: null, mark);
    }

    private Control ElevenLabs(ElevenLabsFigures figures)
    {
        var now = _now();
        var pace = AccountPace.Estimate(figures, now);

        var used = figures.CharactersUsed;
        var limit = figures.CharacterLimit;
        var atLimit = used >= limit;
        var stops = figures.Extension.IsStop;

        var parts = new List<Control>();

        var value = atLimit ? (stops ? 0 : used - limit) : limit - used;
        var valueKey = atLimit ? (stops ? ThemeManager.RedKey : ThemeManager.WarnKey) : ThemeManager.YellowKey;

        parts.Add(Figure(
            Count(value),
            valueKey,
            atLimit && !stops
                ? [Word("characters past the limit of "), Num(Count(limit))]
                : [Word("characters left of "), Num(Count(limit))]));

        var fill = limit > 0 ? Math.Clamp(used / (double)limit, 0, 1) : 1;
        var barKey = atLimit ? (stops ? ThemeManager.RedKey : ThemeManager.WarnKey) : ThemeManager.YellowKey;
        parts.Add(Bar(fill, barKey, atLimit ? null : pace.PeriodGone));

        if (atLimit && stops)
        {
            var until = figures.NextReset is { } reset
                ? $"until the reset at {When(reset)}"
                : "until the limit is raised";

            parts.Add(Alert(
                NoticeLevel.Error,
                "Limit reached",
                $"ElevenLabs refuses anything more on this account {until}. Raise the limit on elevenlabs.io, or choose another voice until then."));
        }

        var rows = new List<(string Key, Inline[] Value, string Ink)>
        {
            ("Used", [Num(Count(used)), Word(" this period, by everything on the account")], ThemeManager.WhiteKey),
        };

        if (figures.Overage.Amount > 0m)
        {
            rows.Add(("Overage", [Num(MoneyText(figures.Overage.Amount, figures.Overage.Currency)), Word(" this period, billed on top of the plan")], ThemeManager.WarnKey));
        }

        if (figures.NextReset is { } next)
        {
            rows.Add(("Resets", [Num(When(next)), Word(" · in "), Num(Days(next, now))], ThemeManager.WhiteKey));
        }
        else
        {
            rows.Add(("Resets", [Word("ElevenLabs reports no reset time")], ThemeManager.GreyKey));
        }

        if (!atLimit)
        {
            rows.Add(("At this rate", Rate(pace, figures), RateInk(pace)));
        }

        if (!(atLimit && stops))
        {
            rows.Add(("At the limit", Extension(figures.Extension), ThemeManager.WhiteKey));
        }

        parts.Add(Rows(rows));

        return Slab("ElevenLabs", "whole account", [.. parts]);
    }

    private Control Deepgram(DeepgramCredit credit)
    {
        var parts = new List<Control>();

        if (credit.Projects.Count == 0)
        {
            var none = new TextBlock
            {
                Text = "This key reaches no Deepgram project.",
                FontSize = TypeScale.Small,
                TextWrapping = TextWrapping.Wrap,
            };
            ThemeInk.Bind(none, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
            parts.Add(none);
        }
        else if (credit.Projects.Select(balance => balance.Project).Distinct(StringComparer.Ordinal).Count() == 1)
        {
            foreach (var balance in credit.Projects)
            {
                parts.Add(Figure(Credit(balance), ThemeManager.YellowKey, [Word("credit left")]));
            }
        }
        else
        {
            foreach (var balance in credit.Projects)
            {
                parts.Add(Breakdown(balance));
            }
        }

        return Slab("Deepgram", "prepaid · no reset", [.. parts]);
    }

    private static Control Breakdown(DeepgramBalance balance)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            ],
        };

        var name = Text(balance.Project, TypeScale.Small, ThemeManager.WhiteKey);
        name.TextWrapping = TextWrapping.Wrap;
        name.Margin = new Thickness(0, 0, 12, 0);

        var use = Text("credit left", TypeScale.Small, ThemeManager.GreyKey);
        use.Margin = new Thickness(0, 0, 12, 0);

        var cost = Text(Credit(balance), TypeScale.Small, ThemeManager.YellowKey);
        cost.FontFamily = new FontFamily(Fonts.MonoFamily);

        Grid.SetColumn(use, 1);
        Grid.SetColumn(cost, 2);
        grid.Children.Add(name);
        grid.Children.Add(use);
        grid.Children.Add(cost);

        return grid;
    }

    private static string Credit(DeepgramBalance balance) => MoneyText(balance.Amount, balance.Units);

    /// <summary>Dollars to two places for USD; any other units as the amount and the units given.</summary>
    internal static string MoneyText(decimal amount, string units) =>
        string.Equals(units, "usd", StringComparison.OrdinalIgnoreCase)
            ? "$" + amount.ToString("N2", Culture)
            : $"{amount.ToString(Culture)} {units}";

    private static string Count(long value) => value.ToString("N0", Culture);

    private static string NameOf(AccountProvider provider) =>
        provider == AccountProvider.ElevenLabs ? "ElevenLabs" : "Deepgram";

    private DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, _zone);

    private string When(DateTimeOffset at) => Local(at).ToString("d MMM HH:mm", Culture);

    private string Day(DateTimeOffset at) => Local(at).ToString("d MMM", Culture);

    private static string Days(DateTimeOffset reset, DateTimeOffset now) =>
        Plural(Math.Max(1, (int)Math.Round((reset - now).TotalDays, MidpointRounding.AwayFromZero)));

    private static string Plural(int days) => days == 1 ? "1 day" : $"{days} days";

    private Inline[] Rate(AccountPaceEstimate pace, ElevenLabsFigures figures)
    {
        switch (pace.Outcome)
        {
            case PaceOutcome.Lasts:
                return [Word("Lasts to the reset, with about "), Num(Count(pace.Left ?? 0)), Word(" left")];
            case PaceOutcome.RunsOut:
                return
                [
                    Word("Runs out about "),
                    Num(Day(pace.LimitAt!.Value)),
                    Word(", "),
                    Num(pace.DaysBeforeReset!.Value.ToString(Culture)),
                    Word(pace.DaysBeforeReset == 1 ? " day before the reset" : " days before the reset"),
                ];
            case PaceOutcome.ReachesLimitThenOverage:
                return [Word("Reaches the limit about "), Num(Day(pace.LimitAt!.Value)), Word(", then overage until the reset")];
            default:
                return pace.Reason switch
                {
                    NoEstimateReason.FirstDay => [Word("Estimated after the first day, from "), Num(When(pace.EstimateFrom!.Value))],
                    NoEstimateReason.NothingUsed => [Word("Nothing used this period, so no estimate")],
                    _ => [Word("No estimate without a reset time")],
                };
        }
    }

    private static string RateInk(AccountPaceEstimate pace) => pace.Outcome switch
    {
        PaceOutcome.RunsOut or PaceOutcome.ReachesLimitThenOverage => ThemeManager.WarnKey,
        PaceOutcome.Lasts => ThemeManager.WhiteKey,
        _ => ThemeManager.GreyKey,
    };

    private static Inline[] Extension(LimitExtension extension)
    {
        if (extension.IsStop)
        {
            return [Word("Stops. This account can't go past its limit.")];
        }

        return extension.IsUnlimited
            ? [Word("Goes past it, with no cap on overage")]
            : [Word("Goes up to "), Num(Count(extension.Characters!.Value)), Word(" past it, billed as overage")];
    }

    private static Run Word(string text) => new(text);

    private static Run Num(string text) => Fonts.Mono(text);

    private static TextBlock Text(string text, double size, string ink)
    {
        var block = new TextBlock { Text = text, FontSize = size };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ink);
        return block;
    }

    /// <summary>The figure line: the value in mono 20, then a sentence.</summary>
    private static Control Figure(string value, string ink, Inline[] sentence)
    {
        var big = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Headline,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        };
        ThemeInk.Bind(big, TextBlock.ForegroundProperty, ink);

        var words = new TextBlock
        {
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        words.Inlines!.AddRange(sentence);
        ThemeInk.Bind(words, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return new StackPanel { Orientation = Orientation.Horizontal, Children = { big, words } };
    }

    /// <summary>The capacity gauge, with the pace tick at <paramref name="periodGone"/> when there is one.</summary>
    private static Control Bar(double fill, string fillKey, double? periodGone)
    {
        var track = Gauge.Track(fill, fillKey);
        track.VerticalAlignment = VerticalAlignment.Center;

        var bar = new Grid { Height = 14 };
        bar.Children.Add(track);

        if (periodGone is { } gone)
        {
            var share = Math.Clamp(gone, 0.001, 0.999);

            var tick = new Border
            {
                Name = "PaceMark",
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, -1, 0),
            };
            ThemeInk.Bind(tick, Border.BackgroundProperty, ThemeManager.WhiteKey);

            var positioned = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(share, GridUnitType.Star),
                    new ColumnDefinition(1 - share, GridUnitType.Star),
                ],
            };
            positioned.Children.Add(tick);
            bar.Children.Add(positioned);
        }

        return bar;
    }

    private static Control Rows(IReadOnlyList<(string Key, Inline[] Value, string Ink)> rows)
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(120, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star)],
            RowSpacing = 2,
        };

        for (var row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var key = new TextBlock
            {
                Text = rows[row].Key.ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.MetaSmall,
                FontWeight = FontWeight.Medium,
                LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 8, 0),
            };
            ThemeInk.Bind(key, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            var value = new TextBlock { FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
            value.Inlines!.AddRange(rows[row].Value);
            ThemeInk.Bind(value, TextBlock.ForegroundProperty, rows[row].Ink);

            Grid.SetRow(key, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(key);
            grid.Children.Add(value);
        }

        return grid;
    }

    /// <summary>A slab holding the provider's label, its source at the right, and the parts.</summary>
    private static Border Slab(string label, string? source, params Control[] parts)
    {
        var stack = new StackPanel { Spacing = 8 };

        var head = Gauge.Heading(label, source ?? string.Empty, ThemeManager.GreyKey);
        stack.Children.Add(head);

        foreach (var part in parts)
        {
            stack.Children.Add(part);
        }

        var slab = new Border { Padding = new Thickness(12, 10), Child = stack };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }
}
