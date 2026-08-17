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
            Raise(nameof(Text));
            Raise(nameof(HasLines));
        }
    }

    /// <summary>
    /// The window as one string, which is what the view draws.
    /// <para>
    /// <b>One text block rather than an item per line, and that is a fix rather than a
    /// simplification.</b> The quad is rasterised out of a window that is constructed and never
    /// shown, and an <c>ItemsControl</c> in one does not regenerate its containers when its
    /// source is replaced: the first caption drew, and every caption after it drew an empty box
    /// — measured at 6,202 lit pixels for the first and 0 for the second and third, with the
    /// view's desired size frozen at the first one's. An ordinary bound property in the same
    /// host updates and redraws perfectly, which is what this now is
    /// (remediation.md, "Only the first caption arrives").
    /// </para>
    /// <para>
    /// Nothing is lost by it. Every line is the same size, weight and colour, centred, and the
    /// wrapping was decided by <see cref="Caption"/> before it ever reached here — the item
    /// template was three identical text blocks where one with newlines in it says the same.
    /// </para>
    /// </summary>
    public string Text => string.Join('\n', _lines);

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
