using D47.Core.Checklists;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>Where a story heads when the Commander has turned down chapters in the black, and what such a chapter must do.</summary>
public static class HomeLeg
{
    public const string Sol = "Sol";

    public const string Colonia = "Colonia";

    /// <summary>Light years from Sol or Colonia within which the Commander is home.</summary>
    public const double Home = 1_000;

    /// <summary>Changed chapters in a row that send the story home.</summary>
    public const int ChangedInARow = 2;

    /// <summary>The system the story heads for from <paramref name="at"/>, or null when the Commander is not in the black.</summary>
    public static string? TargetFrom(StarPosition? at)
    {
        if (at is not { } position)
        {
            return null;
        }

        var fromSol = position.DistanceTo(StarPosition.Origin);
        var fromColonia = position.DistanceTo(ExpeditionKit.Colonia);

        if (fromColonia <= Home)
        {
            return Sol;
        }

        return fromSol <= Home ? null : fromColonia < fromSol ? Colonia : Sol;
    }

    /// <summary>The position of a target named by <see cref="Sol"/> or <see cref="Colonia"/>.</summary>
    public static StarPosition? PositionOf(string? target) =>
        string.Equals(target, Sol, StringComparison.OrdinalIgnoreCase) ? StarPosition.Origin
        : string.Equals(target, Colonia, StringComparison.OrdinalIgnoreCase) ? ExpeditionKit.Colonia
        : null;

    /// <summary>Whether <paramref name="at"/> is within <see cref="Home"/> light years of <paramref name="target"/>.</summary>
    public static bool IsHome(StarPosition? at, string? target) =>
        at is { } position && PositionOf(target) is { } there && position.DistanceTo(there) <= Home;

    /// <summary>The target for a chapter of <paramref name="ask"/>, or null where the rule does not apply.</summary>
    public static string? Of(AdventureAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ask is { Story: { Beacon: null, FinaleChapter: null, Home: { Length: > 0 } home }, Rewrite: null, Length: not AdventureLength.Short } ? home : null;
    }

    /// <summary>The target to carry into the next chapter: kept until the Commander is home, set after two changed chapters in a row in the black.</summary>
    public static string? Next(string? current, StarPosition? at, IReadOnlyList<string> chapters, IReadOnlyList<string> changed, bool excluded)
    {
        if (IsHome(at, current))
        {
            current = null;
        }

        if (current is not null || excluded || chapters.Count < ChangedInARow)
        {
            return current;
        }

        return chapters.TakeLast(ChangedInARow).All(changed.Contains) ? TargetFrom(at) : null;
    }
}
