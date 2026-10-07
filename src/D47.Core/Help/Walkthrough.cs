using D47.Core.Conversation;

namespace D47.Core.Help;

/// <summary>
/// Speaks one capability page's how-to steps one at a time, on fixed phrases and without a model. Holds
/// one walkthrough at most, stores nothing, and reads no clock: the caller supplies the time.
/// </summary>
public sealed class Walkthrough
{
    /// <summary>How long a walkthrough lasts with no walkthrough phrase.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(10);

    /// <summary>The line for "stop" or "cancel", which the caller shows and does not speak.</summary>
    public const string Stopped = "Walkthrough stopped.";

    private static readonly string[] Openers = ["walk me through ", "how do i use "];

    private const string ShowOpener = "show me how ";
    private const string ShowTail = " works";

    private readonly Lazy<IReadOnlyList<HelpArticle>> _pages;

    private HelpArticle? _page;
    private IReadOnlyList<HelpSection> _steps = [];
    private int _at;
    private DateTimeOffset _last;

    /// <summary>Over every shipped page that carries how-to steps.</summary>
    public Walkthrough()
        : this(ShippedPages)
    {
    }

    /// <summary>Over the given pages, read when the first phrase arrives.</summary>
    public Walkthrough(Func<IEnumerable<HelpArticle>> pages) =>
        _pages = new Lazy<IReadOnlyList<HelpArticle>>(() => [.. pages()]);

    /// <summary>Whether a walkthrough is running at <paramref name="now"/>.</summary>
    public bool IsRunning(DateTimeOffset now) => _page is not null && now - _last < Idle;

    /// <summary>
    /// The line to say for a walkthrough phrase, or null when the utterance is not one, which falls
    /// through to the turn and leaves a running walkthrough where it is.
    /// </summary>
    public string? Take(string utterance, DateTimeOffset now)
    {
        if (_page is not null && now - _last >= Idle)
        {
            End();
        }

        var said = KeywordRouter.Utterance(utterance).ToLowerInvariant();

        if (Named(said) is { } name)
        {
            if (Find(name) is not { } page)
            {
                return null;
            }

            _page = page;
            _steps = [.. page.Sections.Where(section => !string.IsNullOrWhiteSpace(section.Say))];
            _at = 0;
            _last = now;

            var count = _steps.Count == 1 ? "1 step" : $"{_steps.Count} steps";
            return $"{page.Title}. {count}. {Step()}";
        }

        if (_page is null)
        {
            return null;
        }

        string? line = said switch
        {
            "next" or "skip" => Next(),
            "again" => Step(),
            "what should i see" => _steps[_at].Expect is { Length: > 0 } expect
                ? expect
                : $"Step {_at + 1} says nothing about what you should see.",
            "back" => Back(),
            "stop" or "cancel" => Stop(),
            _ => null,
        };

        if (line is not null && _page is not null)
        {
            _last = now;
        }

        return line;
    }

    private string Step() => $"Step {_at + 1}. {_steps[_at].Say}";

    private string Next()
    {
        if (_at + 1 >= _steps.Count)
        {
            End();
            return "That was the last step. The walkthrough is over.";
        }

        _at++;
        return Step();
    }

    private string Back()
    {
        if (_at == 0)
        {
            return $"This is the first step. {Step()}";
        }

        _at--;
        return Step();
    }

    private string Stop()
    {
        End();
        return Stopped;
    }

    private void End()
    {
        _page = null;
        _steps = [];
        _at = 0;
    }

    /// <summary>The feature named after one of the starting phrases, or null.</summary>
    private static string? Named(string said)
    {
        foreach (var opener in Openers)
        {
            if (said.Length > opener.Length && said.StartsWith(opener, StringComparison.Ordinal))
            {
                return said[opener.Length..];
            }
        }

        if (said.Length > ShowOpener.Length + ShowTail.Length
            && said.StartsWith(ShowOpener, StringComparison.Ordinal)
            && said.EndsWith(ShowTail, StringComparison.Ordinal))
        {
            return said[ShowOpener.Length..^ShowTail.Length];
        }

        return null;
    }

    /// <summary>The page whose title is the name, ignoring case, any "the" and a trailing "s".</summary>
    private HelpArticle? Find(string name)
    {
        var wanted = Key(name);

        return _pages.Value.FirstOrDefault(page => string.Equals(Key(page.Title), wanted, StringComparison.Ordinal));
    }

    private static string Key(string title)
    {
        var key = KeywordRouter.WithoutThe(title).ToLowerInvariant();

        return key.Length > 3 && key.EndsWith('s') ? key[..^1] : key;
    }

    private static IEnumerable<HelpArticle> ShippedPages() =>
        HelpLibrary.Pages
            .Select(id => HelpLibrary.ParseHowTo(HelpLibrary.PageFor(id), id))
            .OfType<HelpArticle>()
            .Where(page => page.Sections.Any(section => !string.IsNullOrWhiteSpace(section.Say)));
}
