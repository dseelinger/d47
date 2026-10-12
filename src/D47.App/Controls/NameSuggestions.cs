using Avalonia;
using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>What every <see cref="SystemNameSuggestions{T}"/> shares, whatever kind of name it lists.</summary>
public sealed class NameSuggestions
{
    private NameSuggestions()
    {
    }

    /// <summary>The most names listed.</summary>
    public const int Most = 8;

    /// <summary>The fewest characters that are sent.</summary>
    public const int MinimumLength = 3;

    /// <summary>Marks the list, for a test to find it.</summary>
    public const string ListName = "SystemNameSuggestions";

    /// <summary>How long typing has to stop before the names are asked for.</summary>
    public static readonly TimeSpan DefaultPause = TimeSpan.FromMilliseconds(300);

    /// <summary>The suggestions attached to a text box, for the drawn keyboard to offer the same names.</summary>
    public static readonly AttachedProperty<INameSuggestions?> OfProperty =
        AvaloniaProperty.RegisterAttached<NameSuggestions, TextBox, INameSuggestions?>("Of");

    public static INameSuggestions? GetOf(TextBox box) => box.GetValue(OfProperty);
}

/// <summary>A text box's suggestions, whatever kind of name they list.</summary>
public interface INameSuggestions
{
    /// <summary>Names asked for as the drawn keyboard's text changes, and what picking one does.</summary>
    NameTiles NewTiles();
}

/// <summary>Suggested names as text, for a surface that draws its own tiles.</summary>
public sealed class NameTiles
{
    private readonly Action _cancel;
    private readonly Func<string, Task> _typed;
    private readonly Action<int> _choose;

    internal NameTiles(Func<string, Task> typed, Action cancel, Action<int> choose, Action<Action<IReadOnlyList<string>>> onAnswered)
    {
        _typed = typed;
        _cancel = cancel;
        _choose = choose;
        onAnswered(names => Answered?.Invoke(names));
    }

    /// <summary>Raised with the names for the latest text, or with none.</summary>
    public event Action<IReadOnlyList<string>>? Answered;

    public Task Typed(string typed) => _typed(typed);

    public void Cancel() => _cancel();

    /// <summary>Does what picking the name at <paramref name="index"/> in the last answer does.</summary>
    public void Choose(int index) => _choose(index);
}
