using System.Globalization;

namespace D47.Core.Adventures;

/// <summary>The part of a story write a <see cref="StoryProgress"/> is in.</summary>
public enum WriteStage
{
    Places,
    Outline,
    Objectives,
    Checking,
}

/// <summary>A model's typical seconds per stage at medium effort, and the total the Commander is told to expect.</summary>
public sealed record StoryPace(double Places, double Outline, double Objectives)
{
    private static readonly StoryPace Other = new(5, 13, 22);
    private static readonly StoryPace LocalStory = new(5, 20, 50);
    private static readonly StoryPace LocalChapter = new(5, 20, 105);

    private static readonly Dictionary<string, StoryPace> Models = new(StringComparer.Ordinal)
    {
        ["claude-sonnet-5-5"] = new(5, 6, 15),
        ["claude-haiku-5-5"] = new(5, 12, 23),
        ["claude-opus-5-5"] = new(5, 26, 27),
        ["gpt-6.1-sol"] = new(5, 23, 25),
        ["gpt-6-luna"] = new(5, 24, 22),
        ["gpt-5.6-terra"] = new(5, 9, 12),
    };

    public double Total => Places + Outline + Objectives;

    /// <summary>The pace for <paramref name="model"/>; a model outside the table gets the local row when it runs on this machine, else the median of the measured models.</summary>
    public static StoryPace For(string? model, bool local = false, bool chapter = false) =>
        model is not null && Models.TryGetValue(model, out var pace) ? pace
        : !local ? Other
        : chapter ? LocalChapter : LocalStory;

    internal double Seconds(WriteStage stage) => stage switch
    {
        WriteStage.Places => Places,
        WriteStage.Outline => Outline,
        _ => Objectives,
    };
}

/// <summary>
/// How far a story write has got, for a page to read on a timer while the pool writes to it. <see cref="Fill"/> rises with
/// time in the stage and with the characters received, never decreases, and reaches 1 only from <see cref="Done"/>.
/// </summary>
public sealed class StoryProgress(TimeProvider time)
{
    private const double Ceiling = 0.99;
    private const double CharactersPerStage = 20000;

    private static readonly (double From, double To)[] Bands = [(0, 0.10), (0.10, 0.45), (0.45, 0.90), (0.90, 0.99)];

    private readonly object _gate = new();
    private readonly List<WriteStage> _visited = [];
    private readonly long _began = time.GetTimestamp();
    private StoryPace? _pace;
    private WriteStage _stage;
    private long _stageBegan;
    private long _draftBegan;
    private long _characters;
    private double _base;
    private double _fill;
    private bool _stopped;
    private string? _line;
    private TimeSpan? _finished;

    public WriteStage Stage
    {
        get
        {
            lock (_gate)
            {
                return _stage;
            }
        }
    }

    /// <summary>The stages entered, in order.</summary>
    public IReadOnlyList<WriteStage> Visited
    {
        get
        {
            lock (_gate)
            {
                return [.. _visited];
            }
        }
    }

    /// <summary>0 to 0.99 while writing, 1 after <see cref="Done"/>.</summary>
    public double Fill
    {
        get
        {
            lock (_gate)
            {
                if (_finished is not null)
                {
                    return 1;
                }

                if (_pace is not null)
                {
                    _fill = Math.Max(_fill, Current());
                }

                return _fill;
            }
        }
    }

    public TimeSpan Elapsed => _finished ?? time.GetElapsedTime(_began);

    public string Hint
    {
        get
        {
            lock (_gate)
            {
                if (_stopped)
                {
                    return "That took too long. Trying again…";
                }

                if (_line is not null)
                {
                    return _line;
                }

                if (_pace is null)
                {
                    return "";
                }

                var total = _pace.Total;

                return time.GetElapsedTime(_draftBegan).TotalSeconds > 2 * total
                    ? "Taking longer than usual."
                    : string.Create(CultureInfo.InvariantCulture, $"Usually ready in about {Math.Round(total / 5) * 5:0} seconds.");
            }
        }
    }

    public void Enter(WriteStage stage, StoryPace pace)
    {
        ArgumentNullException.ThrowIfNull(pace);

        lock (_gate)
        {
            if (_pace is not null)
            {
                _fill = Math.Max(_fill, Current());
            }
            else
            {
                _draftBegan = time.GetTimestamp();
            }

            _pace = pace;
            _stage = stage;
            _stageBegan = time.GetTimestamp();
            _characters = 0;
            _stopped = false;
            _line = null;
            _visited.Add(stage);
        }
    }

    /// <summary>The hint says the model is being loaded with room for a story, until the next <see cref="Enter"/>.</summary>
    public void Reloading(string model, bool loaded)
    {
        lock (_gate)
        {
            _line = $"{(loaded ? "Reloading" : "Loading")} {model} with room for a story.";
        }
    }

    /// <summary>Text or thinking of this many characters arrived.</summary>
    public void Received(int characters)
    {
        if (characters > 0)
        {
            Interlocked.Add(ref _characters, characters);
        }
    }

    /// <summary>A further draft begins: the four bands are laid again between the fill so far and 0.99.</summary>
    public void NextDraft()
    {
        lock (_gate)
        {
            if (_pace is not null)
            {
                _fill = Math.Max(_fill, Current());
            }

            _base = _fill;
            _stage = WriteStage.Places;
            _draftBegan = time.GetTimestamp();
            _characters = 0;
            _stageBegan = _draftBegan;
        }
    }

    /// <summary>A call limit stopped the call; the hint says so until the next <see cref="Enter"/>.</summary>
    public void CallStopped()
    {
        lock (_gate)
        {
            _stopped = true;
        }
    }

    public void Done()
    {
        lock (_gate)
        {
            _finished ??= time.GetElapsedTime(_began);
        }
    }

    private double Current()
    {
        var (from, to) = Bands[(int)_stage];
        var seconds = time.GetElapsedTime(_stageBegan).TotalSeconds;
        var typical = _pace!.Seconds(_stage);
        var share = 1 - Math.Exp(-(seconds / typical + Interlocked.Read(ref _characters) / CharactersPerStage));
        var position = from + share * (to - from);

        return _base + position / Ceiling * (Ceiling - _base);
    }
}
