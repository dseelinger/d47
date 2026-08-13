using System.ComponentModel;
using System.Runtime.CompilerServices;
using D47.Core.Vr;

namespace D47.App.Headset;

/// <summary>
/// What the caption layer shows, as a view binds to it. A thin projection of
/// <see cref="CaptionLayer"/> rather than a second copy of its state — the layer decides what
/// is on screen and for how long, and this decides nothing.
/// </summary>
public sealed class CaptionViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Point sizes for the three sizes the standard leaves to the viewer. Chosen against the
    /// caption surface's own pixel height rather than against a screen: the quad is 0.9 m wide
    /// at 1.6 m out, so these are what "medium" subtends at about two degrees of arc.
    /// </summary>
    private static readonly IReadOnlyDictionary<CaptionSize, double> Sizes =
        new Dictionary<CaptionSize, double>
        {
            [CaptionSize.Small] = 40,
            [CaptionSize.Medium] = 52,
            [CaptionSize.Large] = 66,
        };

    private IReadOnlyList<string> _lines = [];
    private CaptionSettings _settings = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Lines
    {
        get => _lines;
        private set
        {
            _lines = value;
            Raise(nameof(Lines));
            Raise(nameof(HasLines));
        }
    }

    public bool HasLines => _lines.Count > 0;

    public double FontSize => Sizes[_settings.Size];

    public double BackgroundOpacity => _settings.Sane().BackgroundOpacity;

    public void Show(IReadOnlyList<string> lines) => Lines = [.. lines];

    public void Configure(CaptionSettings settings)
    {
        _settings = settings;
        Raise(nameof(FontSize));
        Raise(nameof(BackgroundOpacity));
    }

    private void Raise([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
