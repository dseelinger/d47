using static D47.Core.Stories.StoryStage;

namespace D47.Core.Stories;

/// <summary>
/// How one story length is paced: the clue days, the finale, the stage after each number of clues given, and
/// whether the chapter writer gets the full fifteen-beat sheet or the short one. Days count from the beacon scan.
/// </summary>
public sealed record StoryPacing
{
    public static readonly StoryPacing ThreeDays = new("3-days", "3 days", [1], 2, 1, false, [(BreakIntoTwo, 1), (Midpoint, 1)]);

    public static readonly StoryPacing OneWeek = new("1-week", "1 week", [1, 3], 5, 2, false, [(BreakIntoTwo, 1), (Midpoint, 1), (AllIsLost, 1)]);

    public static readonly StoryPacing TwoWeeks = new(
        "2-weeks", "2 weeks", [2, 4, 7, 9], 11, 2, false, [(BreakIntoTwo, 1), (FunAndGames, 1), (Midpoint, 1), (AllIsLost, 2)]);

    public static readonly StoryPacing OneMonth = new(
        "1-month", "1 month", [3, 7, 11, 15, 19], 23, 3, false, [(BreakIntoTwo, 1), (FunAndGames, 2), (Midpoint, 1), (AllIsLost, 2)]);

    public static readonly StoryPacing ThreeMonths = new(
        "3-months", "3 months", [7, 14, 21, 28, 42, 56, 70], 84, 3, true,
        [(BreakIntoTwo, 1), (FunAndGames, 3), (Midpoint, 1), (BadGuysCloseIn, 1), (AllIsLost, 1), (DarkNightOfTheSoul, 1)]);

    public static readonly StoryPacing SixMonths = new(
        "6-months", "6 months", [7, 14, 21, 28, 60, 90, 120, 150], 180, 4, true,
        [(BreakIntoTwo, 1), (FunAndGames, 4), (Midpoint, 1), (BadGuysCloseIn, 1), (AllIsLost, 1), (DarkNightOfTheSoul, 1)]);

    public static readonly StoryPacing OneYear = new(
        "1-year", "1 year", [7, 14, 21, 28, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330], 360, 4, true,
        [(BreakIntoTwo, 1), (FunAndGames, 8), (Midpoint, 1), (BadGuysCloseIn, 3), (AllIsLost, 1), (DarkNightOfTheSoul, 1)]);

    /// <summary>Every length, shortest first.</summary>
    public static readonly IReadOnlyList<StoryPacing> All = [ThreeDays, OneWeek, TwoWeeks, OneMonth, ThreeMonths, SixMonths, OneYear];

    private StoryPacing(
        string key, string name, IReadOnlyList<int> clueDays, int finaleDay, int finaleChapters, bool fullSheet,
        IReadOnlyList<(StoryStage Stage, int Clues)> stages)
    {
        Key = key;
        Name = name;
        ClueDays = clueDays;
        FinaleDay = finaleDay;
        FinaleChapters = finaleChapters;
        FullSheet = fullSheet;
        Stages = [.. stages.SelectMany(run => Enumerable.Repeat(run.Stage, run.Clues))];
    }

    /// <summary>The key a card's <c>length</c> holds.</summary>
    public string Key { get; }

    /// <summary>The length as the Commander reads it.</summary>
    public string Name { get; }

    /// <summary>Days after the beacon scan, paused days not counted, before each clue may be spoken.</summary>
    public IReadOnlyList<int> ClueDays { get; }

    /// <summary>Days after the beacon scan, counted as for <see cref="ClueDays"/>, before the finale may begin.</summary>
    public int FinaleDay { get; }

    /// <summary>Finale chapters, each with one finale line.</summary>
    public int FinaleChapters { get; }

    /// <summary>Whether the chapter writer gets all fifteen beats; otherwise only the beats of the stages reached.</summary>
    public bool FullSheet { get; }

    /// <summary>The stage after the beacon scan, indexed by the clues given, 0 to <see cref="ClueDays"/>' count.</summary>
    public IReadOnlyList<StoryStage> Stages { get; }

    /// <summary>Every clue and finale line.</summary>
    public int Lines => ClueDays.Count + FinaleChapters;

    /// <summary>The length with this key, or null.</summary>
    public static StoryPacing? Find(string? key) => All.FirstOrDefault(pacing => string.Equals(pacing.Key, key, StringComparison.Ordinal));

    /// <summary>The beat keys a hidden entry of this length has, exactly.</summary>
    public IReadOnlyList<string> BeatKeys =>
    [
        .. new[] { StoryClues.BeatKeys(this, ActOne, true, null), StoryClues.BeatKeys(this, Finale, true, FinaleChapters) }
            .Concat(Stages.Distinct().Select(stage => StoryClues.BeatKeys(this, stage, true, null)))
            .SelectMany(keys => keys)
            .Distinct(StringComparer.Ordinal),
    ];
}
