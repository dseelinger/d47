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
    /// caption surface's own pixel height rather than against a screen: the quad is 1600 px across
    /// 0.9 m, hung 1.6 m out.
    /// <para>
    /// <b>Which works out at about one degree of arc for medium</b>, with a cap height near 42
    /// arcmin — small is 33 and large 54
    /// (<a href="https://github.com/dseelinger/d47/issues/201">#201</a>). This used to claim two
    /// degrees, which is about double, and the measured sizes were fine: it was only the comment
    /// that was wrong. Recorded because a number written down and never checked is the sort of
    /// wrong that stops the next person checking.
    /// </para>
    /// <para>
    /// <b>One table, and it holds for the world-locked band too</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/204">#204</a>). That band is nearer —
    /// 1.04 m from the eye where this one is 1.66 m — so it would draw these sizes 59% larger if
    /// its width had been left alone. <c>VrCaptionSurface</c> scales the width with the distance
    /// instead, both bands cover the same 30.3° side to side, and every figure above is the same
    /// in either. Re-measuring the steps for the second position would have made the size row
    /// mean two different things.
    /// </para>
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
