namespace D47.Core.Adventures;

/// <summary>The stages of an engineer's progress that a beat can wait for.</summary>
public static class EngineerStages
{
    /// <summary>The stages a beat can name, lowest first.</summary>
    public static IReadOnlyList<string> Named { get; } = ["Invited", "Unlocked"];

    /// <summary>0 for <c>Known</c> or anything unrecognised, 1 for <c>Invited</c>, 2 for <c>Unlocked</c>.</summary>
    public static int Rank(string? stage) => stage?.Trim().ToLowerInvariant() switch
    {
        "invited" => 1,
        "unlocked" => 2,
        _ => 0,
    };
}
