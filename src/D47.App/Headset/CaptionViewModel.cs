using System.ComponentModel;
using System.Runtime.CompilerServices;
using D47.Core.Vr;

namespace D47.App.Headset;

/// <summary>What the caption layer shows, as a view binds to it.</summary>
public sealed class CaptionViewModel : INotifyPropertyChanged
{
    /// <summary>Point sizes for the three sizes the standard leaves to the viewer.</summary>
    private static readonly IReadOnlyDictionary<CaptionSize, double> Sizes =
        new Dictionary<CaptionSize, double>
        {
            [CaptionSize.Small] = 40,
            [CaptionSize.Medium] = 52,
            [CaptionSize.Large] = 66,
        };

    /// <summary>The text's size when D47's line sits above a subtitle, against the band's own.</summary>
    private const double StackedScale = 40.0 / 44.0;

    /// <summary>The D47 mark's size against the band's text.</summary>
    private const double MarkScale = 26.0 / 44.0;

    private IReadOnlyList<string> _lines = [];
    private string? _above;
    private CaptionSettings _settings = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Lines
    {
        get => _lines;
        private set
        {
            _lines = value;
            Raise(nameof(Lines));
            Raise(nameof(Text));
            Raise(nameof(HasLines));
        }
    }

    /// <summary>The window as one string, which is what the view draws.</summary>
    public string Text => string.Join('\n', _lines);

    public bool HasLines => _lines.Count > 0;

    /// <summary>D47's line drawn above a subtitle, after the D47 mark, or null.</summary>
    public string? Above
    {
        get => _above;
        private set
        {
            _above = value;
            Raise(nameof(Above));
            Raise(nameof(Stacked));
            Raise(nameof(TextFontSize));
        }
    }

    /// <summary>Whether D47's line sits above a subtitle.</summary>
    public bool Stacked => _above is not null;

    public double FontSize => Sizes[_settings.Size];

    /// <summary>The size the band's text is drawn at: smaller while D47's line sits above a subtitle.</summary>
    public double TextFontSize => Stacked ? Math.Round(FontSize * StackedScale) : FontSize;

    public double MarkFontSize => Math.Round(FontSize * MarkScale);

    public double BackgroundOpacity => _settings.Sane().BackgroundOpacity;

    public void Show(IReadOnlyList<string> lines, string? above = null)
    {
        Lines = [.. lines];
        Above = above;
    }

    public void Configure(CaptionSettings settings)
    {
        _settings = settings;
        Raise(nameof(FontSize));
        Raise(nameof(TextFontSize));
        Raise(nameof(MarkFontSize));
        Raise(nameof(BackgroundOpacity));
    }

    private void Raise([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
