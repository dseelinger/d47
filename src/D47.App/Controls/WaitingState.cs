using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>Where a wait mark is placed, which sets its cell size and where its sentence goes.</summary>
public enum WaitForm
{
    /// <summary>Beside a control: 6 px cells and no sentence.</summary>
    Inline,

    /// <summary>In a status line: 6 px cells, the sentence after the mark.</summary>
    Line,

    /// <summary>In a page or dialog body: 12 px cells, 10 px on the mini panel, the sentence under the mark.</summary>
    Body,
}

/// <summary>
/// The wait mark: five square cells, one lit in A that steps right on every second <see cref="Beat"/> and returns
/// to the first after the fifth, the cell it left in Line, the rest in Tile; with an optional sentence in Grey.
/// It moves only when beaten, through <see cref="WaitTicker"/> on a host's tick.
/// </summary>
public sealed class WaitingState : StackPanel
{
    /// <summary>How many cells the mark has.</summary>
    public const int Cells = 5;

    /// <summary>How many beats of the 100 ms panel tick one step takes.</summary>
    public const int BeatsPerStep = 2;

    /// <summary>The accessible name of a mark with no sentence.</summary>
    public const string Unsaid = "Waiting";

    private static readonly StyledProperty<IBrush?> LitProperty =
        AvaloniaProperty.Register<WaitingState, IBrush?>("Lit");

    private static readonly StyledProperty<IBrush?> TrailProperty =
        AvaloniaProperty.Register<WaitingState, IBrush?>("Trail");

    private static readonly StyledProperty<IBrush?> RestProperty =
        AvaloniaProperty.Register<WaitingState, IBrush?>("Rest");

    /// <summary>The marks shown now, across every surface.</summary>
    private static readonly HashSet<WaitingState> Showing = [];

    private static readonly Lock ShowingGate = new();

    private readonly WaitForm _form;
    private readonly Border[] _cells = new Border[Cells];
    private readonly TextBlock _sentence;
    private readonly Func<bool> _animated;

    private IDisposable? _mode;
    private bool _attached;
    private bool _shown;
    private bool _still;
    private int _step;
    private int _beats;

    /// <summary>A mark in <paramref name="form"/>; <paramref name="cellSize"/>, when given, is the cells' side in pixels in place of the form's own.</summary>
    public WaitingState(WaitForm form = WaitForm.Inline, string? sentence = null, Func<bool>? animated = null, double? cellSize = null)
    {
        _form = form;
        _animated = animated ?? AnimationEffects.AreOn;

        Orientation = form == WaitForm.Body ? Orientation.Vertical : Orientation.Horizontal;
        Spacing = form == WaitForm.Body ? 14 : 10;
        IsHitTestVisible = false;
        Focusable = false;

        var mark = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        for (var index = 0; index < Cells; index++)
        {
            _cells[index] = new Border();
            mark.Children.Add(_cells[index]);
        }

        _sentence = new TextBlock
        {
            FontSize = form == WaitForm.Body ? TypeScale.Body : TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        ThemeInk.Bind(_sentence, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        Children.Add(mark);
        Children.Add(_sentence);

        ThemeInk.Bind(this, LitProperty, ThemeManager.AKey);
        ThemeInk.Bind(this, TrailProperty, ThemeManager.LineKey);
        ThemeInk.Bind(this, RestProperty, ThemeManager.TileKey);

        SizeCells(cellSize ?? (form == WaitForm.Body ? 12 : 6));
        SetSentence(sentence);
        Paint();
    }

    /// <summary>Shows what is being waited for, in sentence case ending in "…", or no sentence for null.</summary>
    public void SetSentence(string? sentence)
    {
        _sentence.Text = sentence;
        _sentence.IsVisible = sentence is not null;
        AutomationProperties.SetName(this, sentence ?? Unsaid);
    }

    /// <summary>One beat of the panel tick; true when the lit cell stepped.</summary>
    public bool Beat()
    {
        if (_still || ++_beats < BeatsPerStep)
        {
            return false;
        }

        _beats = 0;
        _step = (_step + 1) % Cells;
        Paint();

        return true;
    }

    /// <summary>The marks shown under <paramref name="root"/>: visible, with every ancestor up to the root visible.</summary>
    /// <remarks>The root's own window is not consulted, since the headset draws in a window that is never shown.</remarks>
    internal static List<WaitingState> ShownUnder(Visual root)
    {
        lock (ShowingGate)
        {
            return Showing.Count == 0 ? [] : [.. Showing.Where(mark => Within(mark, root))];
        }
    }

    private static bool Within(Visual mark, Visual root)
    {
        for (Visual? at = mark; at is not null; at = at.GetVisualParent())
        {
            if (!at.IsVisible)
            {
                return false;
            }

            if (ReferenceEquals(at, root))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LitProperty || change.Property == TrailProperty || change.Property == RestProperty)
        {
            Paint();
        }
        else if (change.Property == IsVisibleProperty)
        {
            Track();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        Track();

        if (_form == WaitForm.Body)
        {
            _mode = this.GetVisualAncestors()
                .OfType<Panel.PanelView>()
                .FirstOrDefault()
                ?.GetObservable(Panel.PanelView.ModeProperty)
                .Subscribe(new AnonymousObserver<Panel.PanelMode>(mode =>
                    SizeCells(mode == Panel.PanelMode.Mini ? 10 : 12)));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        Track();

        _mode?.Dispose();
        _mode = null;
    }

    /// <summary>Keeps <see cref="Showing"/> in step, and lights the first cell when the mark appears.</summary>
    private void Track()
    {
        var shown = _attached && IsVisible;

        if (shown == _shown)
        {
            return;
        }

        _shown = shown;

        lock (ShowingGate)
        {
            if (shown)
            {
                Showing.Add(this);
            }
            else
            {
                Showing.Remove(this);
            }
        }

        if (shown)
        {
            _step = 0;
            _beats = 0;
            _still = !_animated();
            Paint();
        }
    }

    private void SizeCells(double size)
    {
        foreach (var cell in _cells)
        {
            cell.Width = size;
            cell.Height = size;
        }
    }

    private void Paint()
    {
        if (_still)
        {
            foreach (var cell in _cells)
            {
                cell.Background = GetValue(TrailProperty);
            }

            return;
        }

        var trail = (_step + Cells - 1) % Cells;

        for (var index = 0; index < Cells; index++)
        {
            _cells[index].Background = index == _step
                ? GetValue(LitProperty)
                : index == trail ? GetValue(TrailProperty) : GetValue(RestProperty);
        }
    }
}
