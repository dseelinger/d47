using System.Text.RegularExpressions;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>Reads whether a GalNet article looks like a Community Goal launch, from its title and closing call to action.</summary>
public static partial class CommunityGoalLaunch
{
    private static readonly string[] ClosingTitleWords = ["conclude", "success", "deemed"];

    /// <summary>Paragraphs from the end of the article that are searched for a call to action.</summary>
    public const int ClosingParagraphs = 3;

    [GeneratedRegex(
        @"register at|registering at|register their interest|sign up for|should head to|should make their way to|invited to support|calling on\b.{0,120}?\bto deliver|called upon commanders",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CallToAction();

    public static bool Recognises(GalNetArticle article)
    {
        ArgumentNullException.ThrowIfNull(article);

        if (ClosingTitleWords.Any(word => article.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return article.Body.TakeLast(ClosingParagraphs).Any(paragraph => CallToAction().IsMatch(paragraph));
    }
}
