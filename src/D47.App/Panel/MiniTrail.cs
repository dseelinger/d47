using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Which colour a part of the mini panel's trail is drawn in.</summary>
public enum MiniTrailInk
{
    Earlier,
    Separator,
    Current,
}

/// <summary>One drawn piece of the mini panel's trail.</summary>
public sealed record MiniTrailPart(string Text, MiniTrailInk Ink);

/// <summary>The words of the mini panel's trail and how they give way when there is no room.</summary>
public static class MiniTrail
{
    public const string Separator = "›";

    public const string Elision = "…";

    /// <summary>Space either side of a separator.</summary>
    public const double SeparatorGap = 6;

    /// <summary>
    /// The tab's word, the root's word when the tab has more than one root, then every deeper level, then the
    /// page's open section when it has one. Upper case.
    /// </summary>
    public static IReadOnlyList<string> Words(string tabWord, IReadOnlyList<NavCrumb> trail, int rootCount, string openSection = "")
    {
        var words = new List<string> { tabWord.ToUpperInvariant() };

        for (var index = 0; index < trail.Count; index++)
        {
            if (index == 0 && rootCount <= 1)
            {
                continue;
            }

            words.Add(trail[index].Word.ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(openSection))
        {
            words.Add(openSection.Trim().ToUpperInvariant());
        }

        return words;
    }

    /// <summary>
    /// The parts to draw in <paramref name="room"/>. Levels after the tab give way from the second, the run
    /// that gave way becoming one elision; the tab and the current level stay, the current level trimming last.
    /// </summary>
    public static IReadOnlyList<MiniTrailPart> Fit(IReadOnlyList<string> words, double room, Func<string, double> width)
    {
        if (words.Count == 0)
        {
            return [];
        }

        var separator = width(Separator) + (2 * SeparatorGap);

        // `gone` levels after the tab are replaced by one elision.
        for (var gone = 0; gone <= words.Count - 2; gone++)
        {
            var parts = Build(words, gone);

            if (Measure(parts, separator, width) <= room)
            {
                return parts;
            }
        }

        var shortest = Build(words, Math.Max(0, words.Count - 2)).ToList();

        if (words.Count < 2)
        {
            return shortest;
        }

        var last = shortest[^1];
        var others = Measure(shortest.Take(shortest.Count - 1).ToList(), separator, width) + separator;
        var space = room - others;

        shortest[^1] = last with { Text = Trim(last.Text, space, width) };
        return shortest;
    }

    private static List<MiniTrailPart> Build(IReadOnlyList<string> words, int gone)
    {
        var parts = new List<MiniTrailPart>();
        var last = words.Count - 1;

        for (var index = 0; index <= last; index++)
        {
            if (index >= 1 && index <= gone)
            {
                if (index == 1)
                {
                    parts.Add(new MiniTrailPart(Separator, MiniTrailInk.Separator));
                    parts.Add(new MiniTrailPart(Elision, MiniTrailInk.Separator));
                }

                continue;
            }

            if (index > 0)
            {
                parts.Add(new MiniTrailPart(Separator, MiniTrailInk.Separator));
            }

            parts.Add(new MiniTrailPart(words[index], index == last ? MiniTrailInk.Current : MiniTrailInk.Earlier));
        }

        return parts;
    }

    private static double Measure(IReadOnlyList<MiniTrailPart> parts, double separator, Func<string, double> width) =>
        parts.Sum(part => part.Text == Separator && part.Ink == MiniTrailInk.Separator ? separator : width(part.Text));

    private static string Trim(string text, double space, Func<string, double> width)
    {
        if (width(text) <= space)
        {
            return text;
        }

        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text[..length].TrimEnd() + Elision;

            if (width(candidate) <= space)
            {
                return candidate;
            }
        }

        return Elision;
    }
}
