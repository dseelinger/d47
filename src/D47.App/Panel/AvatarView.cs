using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using D47.Core.Audio;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The ship AI's face: one look per loop state, animated (Phase 11, "Ship's AI Avatar").</summary>
public sealed class AvatarView : UserControl
{
    /// <summary>How long one frame of a dropped-in sequence holds.</summary>
    private static readonly TimeSpan FrameHold = TimeSpan.FromMilliseconds(250);

    private readonly Ellipse _ring = new();
    private readonly Path _core = new();
    private readonly Image _custom = new();
    private readonly Grid _root = new();

    private readonly DispatcherTimer _frames = new() { Interval = FrameHold };

    private AvatarLibrary? _library;
    private IReadOnlyList<Bitmap> _sequence = [];
    private int _frame;
    private LoopState _state = LoopState.Idle;

    /// <summary>How big the whole mark is, and everything inside it follows (#234).</summary>
    public double Extent
    {
        get => Width;
        set
        {
            var scale = value / 44d;

            Width = value;
            Height = value;

            _ring.Width = 40 * scale;
            _ring.Height = 40 * scale;
            _core.Width = 22 * scale;
            _core.Height = 22 * scale;
        }
    }

    public AvatarView()
    {
        Width = 44;
        Height = 44;

        _ring.StrokeThickness = 1.5;
        _ring.Opacity = 0.35;
        _ring.Width = 40;
        _ring.Height = 40;

        _core.Stretch = Stretch.Uniform;
        _core.Width = 22;
        _core.Height = 22;
        _core.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        _core.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        _custom.Stretch = Stretch.Uniform;
        _custom.IsVisible = false;

        _root.Children.Add(_ring);
        _root.Children.Add(_core);
        _root.Children.Add(_custom);
        Content = _root;

        _frames.Tick += (_, _) => Advance();

        Apply(LoopState.Idle);
    }

    /// <summary>The Commander's own imagery, or null for none.</summary>
    public AvatarLibrary? Library
    {
        get => _library;
        set
        {
            _library = value;
            Apply(_state);
        }
    }

    /// <summary>Which state to show.</summary>
    public void Show(LoopState state)
    {
        if (state == _state)
        {
            return;
        }

        Apply(state);
    }

    private void Apply(LoopState state)
    {
        _state = state;

        StopSequence();

        if (LoadCustom(state) is { Count: > 0 } custom)
        {
            _sequence = custom;
            _frame = 0;
            _custom.Source = _sequence[0];
            _custom.IsVisible = true;
            _ring.IsVisible = false;
            _core.IsVisible = false;

            // One image is a still, not an animation.
            if (_sequence.Count > 1)
            {
                _frames.Start();
            }

            return;
        }

        _custom.IsVisible = false;
        _custom.Source = null;
        _ring.IsVisible = true;
        _core.IsVisible = true;

        // Bound rather than looked up.
        var key = ResourceKeyFor(state);

        _ring.Bind(Shape.StrokeProperty, new DynamicResourceExtension(key));
        _core.Bind(Shape.FillProperty, new DynamicResourceExtension(key));
        _core.Data = GeometryFor(state);

        Animate(state);
    }

    /// <summary>Colour by role, never by literal, like every other view.</summary>
    internal static string ResourceKeyFor(LoopState state) => state switch
    {
        LoopState.Failed => "D47.Danger",
        LoopState.Unsure => "D47.Info",
        LoopState.Idle => "D47.TextMuted",
        _ => "D47.Accent",
    };

    /// <summary>
    /// One distinctive centre per state, so they are told apart at a glance and in greyscale — colour
    /// is never the only signal.
    /// </summary>
    private static Geometry GeometryFor(LoopState state) => Geometry.Parse(PathFor(state));

    /// <summary>The path data itself.</summary>
    internal static string PathFor(LoopState state) => state switch
    {
        // A closed ring: nothing in flight.
        LoopState.Idle => "M 12,4 A 8,8 0 1 1 11.99,4 Z M 12,8 A 4,4 0 1 0 12.01,8 Z",

        // An open mouth, facing up.
        LoopState.Listening => "M 3,10 A 9,9 0 0 0 21,10 L 17,10 A 5,5 0 0 1 7,10 Z",

        // Three bars, left to right: sound becoming words.
        LoopState.Transcribing => "M 4,9 h 3 v 6 h -3 Z M 10.5,6 h 3 v 12 h -3 Z M 17,10 h 3 v 4 h -3 Z",

        // Four blocks around a gap — the Guardian motif, working.
        LoopState.Thinking => "M 4,4 h 6 v 6 h -6 Z M 14,4 h 6 v 6 h -6 Z M 4,14 h 6 v 6 h -6 Z M 14,14 h 6 v 6 h -6 Z",

        // A widening wave: speech leaving.
        LoopState.Speaking => "M 5,8 h 3 v 8 h -3 Z M 11,5 h 3 v 14 h -3 Z M 17,9 h 3 v 6 h -3 Z",

        // A tick.
        LoopState.Answered => "M 4,13 l 5,5 l 11,-13 l -3,-2 l -8,10 l -3,-3 Z",

        // A question, without a font in it.
        LoopState.Unsure =>
            "M 8,8 A 4,4 0 1 1 12,12 L 12,15 h -2 L 10,11 A 2.5,2.5 0 1 0 10,8 Z M 10,17 h 2 v 2 h -2 Z",

        // A cross.
        LoopState.Failed => "M 5,7 l 2,-2 l 5,5 l 5,-5 l 2,2 l -5,5 l 5,5 l -2,2 l -5,-5 l -5,5 l -2,-2 l 5,-5 Z",

        _ => "M 12,4 A 8,8 0 1 1 11.99,4 Z",
    };

    /// <summary>The movement.</summary>
    private void Animate(LoopState state)
    {
        _core.Transitions = null;
        _ring.Transitions = null;

        var (duration, from, to) = state switch
        {
            // Working.
            LoopState.Thinking => (TimeSpan.FromMilliseconds(900), 0.45d, 1.0d),
            LoopState.Speaking => (TimeSpan.FromMilliseconds(500), 0.55d, 1.0d),
            LoopState.Listening => (TimeSpan.FromMilliseconds(1400), 0.6d, 1.0d),
            LoopState.Transcribing => (TimeSpan.FromMilliseconds(700), 0.5d, 1.0d),

            // At rest.
            _ => (TimeSpan.FromSeconds(4), 0.75d, 1.0d),
        };

        var pulse = new Animation
        {
            Duration = duration,
            IterationCount = IterationCount.Infinite,
            PlaybackDirection = PlaybackDirection.Alternate,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, to) } },
            },
        };

        // Fire and forget.
        _ = pulse.RunAsync(_core);
    }

    private IReadOnlyList<Bitmap> LoadCustom(LoopState state)
    {
        if (_library?.For(state) is not { Count: > 0 } files)
        {
            return [];
        }

        var loaded = new List<Bitmap>(files.Count);

        foreach (var file in files)
        {
            try
            {
                loaded.Add(new Bitmap(file));
            }
            catch (Exception)
            {
            // A file that passed the readability check and is still a truncated PNG.
            }
        }

        return loaded;
    }

    private void Advance()
    {
        if (_sequence.Count == 0)
        {
            return;
        }

        _frame = (_frame + 1) % _sequence.Count;
        _custom.Source = _sequence[_frame];
    }

    private void StopSequence()
    {
        _frames.Stop();

        foreach (var bitmap in _sequence)
        {
            bitmap.Dispose();
        }

        _sequence = [];
        _frame = 0;
    }
}
