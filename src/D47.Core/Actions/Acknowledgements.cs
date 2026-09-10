using D47.Core.Input;

namespace D47.Core.Actions;

/// <summary>
/// The terse reply a performed command answers with. Never the form it said last, so a run of
/// commands is not one word repeated.
/// </summary>
public sealed class Acknowledgements(Random? choice = null)
{
    /// <summary>Forms that name nothing, and so fit any action.</summary>
    private static readonly IReadOnlyList<string> Bare = ["Aye.", "Aye, aye.", "Done.", "Acknowledged."];

    /// <summary>Above this the named form stops being an acknowledgement and becomes a sentence again.</summary>
    private const int LongestNamedPhrase = 3;

    private readonly Random _choice = choice ?? Random.Shared;
    private readonly Lock _gate = new();

    private string? _last;

    /// <summary>What to say for an action that has just been performed.</summary>
    public string For(GameAction action, DesiredState state)
    {
        var forms = Forms(action, state);

        lock (_gate)
        {
            var open = forms.Where(form => !string.Equals(form, _last, StringComparison.Ordinal)).ToArray();
            var picked = open[_choice.Next(open.Length)];

            _last = picked;

            return picked;
        }
    }

    /// <summary>Every acknowledgement this action and state can produce.</summary>
    public static IReadOnlyList<string> Forms(GameAction action, DesiredState state) =>
        Named(action, state) is { } named ? [.. Bare, named] : Bare;

    /// <summary>
    /// The Commander's own short phrase for what was asked — "Aye, gear down." — where the catalogue
    /// has one and it is short enough to say.
    /// </summary>
    private static string? Named(GameAction action, DesiredState state)
    {
        var phrase = action.Phrases.FirstOrDefault(entry => entry.State == state).Phrase;

        return phrase is { Length: > 0 } && phrase.Split(' ').Length <= LongestNamedPhrase
            ? $"Aye, {phrase}."
            : null;
    }
}
