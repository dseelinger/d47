using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A live possible first, its body's name and when it appeared on the panels.</summary>
public sealed record RegionFirstMoment(CodexFirstSeen Seen, string? Body, DateTimeOffset Shown)
{
    /// <summary>How long the mini panels show it.</summary>
    public static readonly TimeSpan Lasts = TimeSpan.FromSeconds(60);

    public DateTimeOffset Until => Shown + Lasts;

    /// <summary>"You may be the first Commander to log this in Region." or "… anywhere in the galaxy."</summary>
    public string Sentence => Seen.First == CodexFirst.Galaxy
        ? "You may be the first Commander to log this anywhere in the galaxy."
        : $"You may be the first Commander to log this in {Seen.Region ?? CodexModel.NoRegionName}.";
}

/// <summary>
/// The mini panels' read-out of a live possible first, over the whole text area: the kind, the entry, where, the
/// sentence, the hint or the press, and a line along the bottom that shortens as its time runs out.
/// </summary>
public sealed class RegionFirstReadout : Border
{
    public const string LabelName = "RegionFirstLabel";
    public const string EntryName = "RegionFirstEntry";
    public const string WhereName = "RegionFirstWhere";
    public const string TextName = "RegionFirstText";
    public const string HintName = "RegionFirstHint";
    public const string FaceName = "RegionFirstFace";
    public const string SentName = "RegionFirstSent";
    public const string FailedName = "RegionFirstFailed";
    public const string SendingName = "RegionFirstSending";
    public const string TimeName = "RegionFirstTime";

    private readonly TextBlock _label = new() { Name = LabelName };
    private readonly TextBlock _entry = new() { Name = EntryName };
    private readonly TextBlock _where = new() { Name = WhereName, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _text = new() { Name = TextName, TextWrapping = TextWrapping.Wrap };
    private readonly Border _foot = new();
    private readonly Grid _time = new() { Name = TimeName, Height = 3, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly WaitingState _mark = new(WaitForm.Line, RegionFirstPress.SendingText, cellSize: 10) { Name = SendingName };

    private RegionFirstMoment? _drawn;
    private (RegionFirstPressState, bool Reported, bool Headset, bool Here)? _footDrawn;
    private int _secondsLeft = -1;

    public RegionFirstReadout()
    {
        ClipToBounds = true;
        ThemeInk.Bind(this, BackgroundProperty, ThemeManager.BgKey);

        Typed(_label, TypeScale.Meta, Fonts.ChromeFamily, FontWeight.SemiBold, TypeScale.Meta * Fonts.ChromeTracking);
        Typed(_entry, 26, Fonts.ChromeFamily, FontWeight.SemiBold, 26 * Fonts.TitleTracking);
        Typed(_where, TypeScale.Secondary, Fonts.ProseFamily);
        Typed(_text, TypeScale.Secondary, Fonts.ProseFamily);
        _entry.LineHeight = 26 * 1.15;
        _entry.TextTrimming = TextTrimming.CharacterEllipsis;
        _text.LineHeight = TypeScale.Secondary * 1.45;
        _text.Margin = new Thickness(0, 4, 0, 0);
        ThemeInk.Bind(_label, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
        ThemeInk.Bind(_entry, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(_where, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(_text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var column = new StackPanel { Spacing = 6, Children = { _label, _entry, _where, _text } };

        var rows = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(16, 35, 16, 16),
        };
        rows.Children.Add(column);
        Grid.SetRow(_foot, 2);
        rows.Children.Add(_foot);

        _time.ColumnDefinitions = new ColumnDefinitions("1*,0*");
        ThemeInk.Bind(_time, Avalonia.Controls.Panel.BackgroundProperty, ThemeManager.Line2Key);
        var fill = new Border();
        ThemeInk.Bind(fill, BackgroundProperty, ThemeManager.CyanKey);
        _time.Children.Add(fill);

        Child = new Avalonia.Controls.Panel { Children = { rows, _time } };
    }

    /// <summary>The share of the read-out's time left, 0 to 1.</summary>
    public double Left { get; private set; }

    /// <summary>
    /// Draws <paramref name="moment"/> as of <paramref name="now"/>: the face and its states when
    /// <paramref name="headset"/>, the hint alone otherwise. True when anything drawn changed, which is at most once a
    /// second unless the moment or the press moved.
    /// </summary>
    public bool Draw(RegionFirstMoment moment, DateTimeOffset now, RegionFirstPress? press, bool headset, string? currentSystem)
    {
        ArgumentNullException.ThrowIfNull(moment);

        var changed = false;
        var here = currentSystem is { Length: > 0 } && string.Equals(currentSystem, moment.Seen.System, StringComparison.OrdinalIgnoreCase);

        if (!ReferenceEquals(moment, _drawn))
        {
            _drawn = moment;
            _footDrawn = null;
            _secondsLeft = -1;
            _label.Text = CodexModel.RegionFirstWord(moment.Seen.First);
            _entry.Text = moment.Seen.Name.ToUpperInvariant();
            _text.Text = moment.Sentence;
            changed = true;
        }

        var record = press?.Find(moment.Seen.EntryId, moment.Seen.RegionId);
        var state = record is null || press is null ? RegionFirstPressState.Ready : press.StateOf(record);
        var foot = (state, record?.Reported is not null, headset, here);

        if (_footDrawn != foot)
        {
            _footDrawn = foot;
            DrawWhere(moment, here);
            _foot.Child = Foot(press, record, state, headset);
            changed = true;
        }

        var seconds = (int)Math.Ceiling(Math.Max(0, (moment.Until - now).TotalSeconds));

        if (seconds != _secondsLeft)
        {
            _secondsLeft = seconds;
            Left = Math.Clamp(seconds / RegionFirstMoment.Lasts.TotalSeconds, 0, 1);
            _time.ColumnDefinitions[0].Width = new GridLength(Left, GridUnitType.Star);
            _time.ColumnDefinitions[1].Width = new GridLength(1 - Left, GridUnitType.Star);
            changed = true;
        }

        return changed;
    }

    /// <summary>Forgets what was drawn, so the next <see cref="Draw"/> draws everything.</summary>
    public void Clear() => _drawn = null;

    /// <summary>"Region · System body", the system in Cyan while the Commander is in it and the body in Grey.</summary>
    private void DrawWhere(RegionFirstMoment moment, bool here)
    {
        var inlines = new InlineCollection { new Run($"{moment.Seen.Region ?? CodexModel.NoRegionName} · ") };

        if (moment.Seen.System is { Length: > 0 } system)
        {
            var name = new Run(system);

            if (here)
            {
                ThemeInk.Bind(name, Run.ForegroundProperty, ThemeManager.CyanKey);
            }

            inlines.Add(name);
        }

        if (CodexModel.BodyOf(moment.Seen.System, moment.Body) is { Length: > 0 } body)
        {
            var grey = new Run($" {body}");
            ThemeInk.Bind(grey, Run.ForegroundProperty, ThemeManager.GreyKey);
            inlines.Add(grey);
        }

        _where.Text = null;
        _where.Inlines = inlines;
    }

    /// <summary>The sent line, the failure, the wait mark, or the hint with the face before it in the headset.</summary>
    private Control Foot(RegionFirstPress? press, RegionFirstRecord? record, RegionFirstPressState state, bool headset)
    {
        (_mark.Parent as Avalonia.Controls.Panel)?.Children.Remove(_mark);
        _mark.IsVisible = false;

        if (state == RegionFirstPressState.Sent || record?.Reported is not null)
        {
            var sent = Line(RegionFirstPress.SentText, ThemeManager.BlueKey, Fonts.ChromeFamily);
            sent.Name = SentName;
            sent.FontWeight = FontWeight.SemiBold;

            return sent;
        }

        if (state == RegionFirstPressState.Failed)
        {
            return new Notice(NoticeLevel.Error, inline: true)
            {
                Name = FailedName,
                Label = RegionFirstPress.FailedLabel,
                Text = RegionFirstPress.RetryText,
            };
        }

        if (state == RegionFirstPressState.Sending)
        {
            _mark.IsVisible = true;

            return new StackPanel { Children = { _mark } };
        }

        var pressable = headset && press is not null && record is not null && RegionFirstPress.Offers(record);
        var hint = Line(pressable ? RegionFirstPress.PressHint : RegionFirstPress.SayHint, ThemeManager.GreyKey, Fonts.ProseFamily);
        hint.Name = HintName;

        if (!pressable)
        {
            return hint;
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(Face(() => _ = press!.PressAsync(record!)));
        row.Children.Add(hint);

        return row;
    }

    /// <summary>
    /// The REPORT TO CANONN tile as a pressed border, because an output-only mini surface hides every button; the ray
    /// presses it in the headset.
    /// </summary>
    private static Border Face(Action press)
    {
        var word = new TextBlock
        {
            Text = RegionFirstPress.TileLabel.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Control,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.78,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(word, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var face = new Border
        {
            Name = FaceName,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = word,
        };
        ThemeInk.Bind(face, BackgroundProperty, ThemeManager.TileKey);
        AutomationProperties.SetName(face, RegionFirstPress.TileLabel);
        face.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            press();
        };

        return face;
    }

    private static TextBlock Line(string text, string key, string family)
    {
        var line = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(family),
            FontSize = TypeScale.Small,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(line, TextBlock.ForegroundProperty, key);

        return line;
    }

    private static void Typed(TextBlock block, double size, string family, FontWeight? weight = null, double? tracking = null)
    {
        block.FontSize = size;
        block.FontFamily = new FontFamily(family);

        if (weight is { } w)
        {
            block.FontWeight = w;
        }

        if (tracking is { } t)
        {
            block.LetterSpacing = t;
        }
    }
}
