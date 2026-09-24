using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace D47.App.Controls;

/// <summary>
/// A number and its unit between <c>◄</c> and <c>►</c> tiles. The arrows move by <see cref="Step"/>,
/// clamp to the range and repeat while held. Clicking the value opens it for typing: Enter or leaving
/// the box applies, Esc cancels.
/// </summary>
public sealed class Amount : ContentControl
{
    /// <summary>The width an amount is drawn at on a settings row.</summary>
    public const double CompactWidth = 220;

    /// <summary>Raised when a press or an edit sets the value — never by setting <see cref="Value"/>.</summary>
    public event EventHandler? ValueCommitted;

    private readonly TextBlock _shown = new()
    {
        Name = "AmountValue",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
        FontSize = Theming.TypeScale.Body,
    };

    private readonly TextBox _editor = new()
    {
        Name = "AmountEditor",
        IsVisible = false,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Center,
        FontFamily = new FontFamily(Theming.Fonts.MonoFamily),
        FontSize = Theming.TypeScale.Body,
    };

    private readonly RepeatButton _previous;
    private readonly RepeatButton _next;
    private IDisposable? _ink;
    private bool _cancelling;

    public Amount()
    {
        Focusable = true;
        Width = CompactWidth;

        _previous = Arrow("◄", "Decrease", -1);
        _next = Arrow("►", "Increase", 1);

        var cell = new Border
        {
            Padding = new Thickness(8, 0),
            Cursor = new Cursor(StandardCursorType.Ibeam),
            Child = new Avalonia.Controls.Panel { Children = { _shown, _editor } },
        };
        cell.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.SlabKey));
        cell.PointerPressed += (_, e) =>
        {
            if (!_editor.IsVisible)
            {
                e.Handled = true;
                BeginEdit();
            }
        };

        TruncationTip.Watch(_shown, () => _shown.Text);

        var row = new Grid
        {
            Height = Theming.TypeScale.MinimumTarget,
            ColumnSpacing = Segment.Gap,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        Grid.SetColumn(_previous, 0);
        Grid.SetColumn(cell, 1);
        Grid.SetColumn(_next, 2);
        row.Children.Add(_previous);
        row.Children.Add(cell);
        row.Children.Add(_next);

        Content = row;

        _editor.KeyDown += OnEditorKey;
        _editor.LostFocus += (_, _) =>
        {
            if (_editor.IsVisible && !_cancelling)
            {
                EndEdit(commit: true);
            }
        };

        KeyDown += OnKeyDown;
        Sync();
    }

    /// <summary>The value, or null where the setting is cleared and <see cref="Placeholder"/> shows.</summary>
    public decimal? Value
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    public decimal Step { get; set; } = 1;

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    /// <summary>The numeric format the value is shown and written in.</summary>
    public string Format { get; set; } = "0";

    /// <summary>What the value is counted in: "ms", "%", "$".</summary>
    public string? Unit
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>What shows when <see cref="Value"/> is null — the default, in words or as a number.</summary>
    public string? Placeholder
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>The value one press moves to, from <paramref name="from"/> or, where nothing is set, the default.</summary>
    public static decimal Stepped(
        decimal? from, decimal? fallback, decimal step, int direction, decimal? minimum, decimal? maximum)
    {
        var next = (from ?? fallback ?? minimum ?? 0) + direction * step;

        if (minimum is { } low && next < low)
        {
            next = low;
        }

        if (maximum is { } high && next > high)
        {
            next = high;
        }

        return next;
    }

    /// <summary>A value as the box shows it: <c>500 MS</c>, <c>$0.05</c>, <c>80%</c>.</summary>
    public static string Display(decimal value, string format, string? unit)
    {
        var number = value.ToString(format, CultureInfo.InvariantCulture);

        return unit switch
        {
            null or "" => number,
            "$" => "$" + number,
            "%" => number + "%",
            _ => $"{number} {unit.ToUpperInvariant()}",
        };
    }

    /// <summary>Opens the value for typing, as a click on it does.</summary>
    public void BeginEdit()
    {
        _editor.Text = Value?.ToString(Format, CultureInfo.InvariantCulture) ?? string.Empty;
        _editor.PlaceholderText = Fallback()?.ToString(Format, CultureInfo.InvariantCulture);
        _shown.IsVisible = false;
        _editor.IsVisible = true;
        _editor.Focus();
        _editor.SelectAll();
    }

    private decimal? Fallback() =>
        decimal.TryParse(Placeholder, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private RepeatButton Arrow(string glyph, string name, int direction)
    {
        var button = new RepeatButton
        {
            Theme = Application.Current!.FindResource("D47.StepperArrow") as ControlTheme,
            Delay = 400,
            Interval = 125,
            Content = glyph,
            Width = Theming.TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => Move(direction);

        return button;
    }

    private void Move(int direction)
    {
        Value = Stepped(Value, Fallback(), Step, direction, Minimum, Maximum);
        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor.IsVisible)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                Move(-1);
                e.Handled = true;
                break;

            case Key.Right:
                Move(1);
                e.Handled = true;
                break;

            case Key.Enter:
                BeginEdit();
                e.Handled = true;
                break;
        }
    }

    private void OnEditorKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                EndEdit(commit: true);
                break;

            case Key.Escape:
                e.Handled = true;
                EndEdit(commit: false);
                break;
        }
    }

    private void EndEdit(bool commit)
    {
        _cancelling = !commit;
        _editor.IsVisible = false;
        _shown.IsVisible = true;
        Focus();
        _cancelling = false;

        if (!commit)
        {
            return;
        }

        var typed = _editor.Text?.Trim().TrimStart('$').TrimEnd('%') ?? string.Empty;

        if (typed.Length == 0)
        {
            Value = null;
        }
        else if (decimal.TryParse(typed, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = parsed;
        }
        else
        {
            return;
        }

        ValueCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void Sync()
    {
        _ink?.Dispose();

        if (Value is { } value)
        {
            _shown.Text = Display(value, Format, Unit);
            _ink = _shown.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.WhiteKey));
        }
        else
        {
            _shown.Text = Fallback() is { } fallback ? Display(fallback, Format, Unit) : Placeholder ?? string.Empty;
            _ink = _shown.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(Theming.ThemeManager.GreyKey));
        }
    }
}
