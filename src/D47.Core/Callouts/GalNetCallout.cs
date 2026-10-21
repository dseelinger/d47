using System.Globalization;
using D47.Core.Audio;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Says the headlines of the GalNet articles that arrived in the last poll, newest first, never the articles.</summary>
public sealed class GalNetCallout : ICallout
{
    public string Id => "galnet";

    public const string Key = "galnet.new";

    /// <summary>Headlines said in one line; more articles are a count.</summary>
    public const int TitlesSaid = 3;

    /// <summary>The articles that arrived in the last poll, newest first; empty while GalNet news is off.</summary>
    public Func<IReadOnlyList<GalNetArticle>> Arrived { get; set; } = () => [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var arrived = Arrived();

        if (context.IsPriming || arrived.Count == 0)
        {
            yield break;
        }

        var titles = arrived.Take(TitlesSaid).Select(article => article.Title.Trim().TrimEnd('.')).ToList();

        var text = arrived.Count switch
        {
            1 => $"GalNet: {titles[0]}.",
            <= TitlesSaid => $"New on GalNet: {Join(titles)}.",
            _ => $"{Capitalise(SpokenNumbers.Expand(arrived.Count.ToString(CultureInfo.InvariantCulture)))} new GalNet articles. The latest: {Join(titles)}.",
        };

        yield return new Announcement(Key, text) { Cooldown = TimeSpan.Zero };
    }

    private static string Join(List<string> titles) => titles.Count switch
    {
        1 => titles[0],
        2 => $"{titles[0]}; and {titles[1]}",
        _ => $"{string.Join("; ", titles.Take(titles.Count - 1))}; and {titles[^1]}",
    };

    private static string Capitalise(string word) => char.ToUpperInvariant(word[0]) + word[1..];
}
