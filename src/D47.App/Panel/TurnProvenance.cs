using System.Globalization;
using System.Text;
using D47.Core.Conversation;

namespace D47.App.Panel;

/// <summary>
/// The line drawn inside a finished turn: outcome and what answered, then effort, then the cost. Parts that do
/// not apply are left out.
/// </summary>
/// <param name="Lead">Every part but the cost, already joined.</param>
/// <param name="Cost">The priced figure, drawn in A, or null for a turn with none.</param>
public sealed record TurnProvenance(string Lead, string? Cost)
{
    /// <summary>What joins the parts.</summary>
    public const string Separator = " · ";

    public string Text => Cost is null ? Lead : Lead + Separator + Cost;

    public static TurnProvenance For(TurnResult result)
    {
        var answeredBy = result.Model is { Length: > 0 } model ? model : Words(result.Route.ToString());
        var parts = new List<string> { $"{result.Outcome} via {answeredBy}".ToUpperInvariant() };

        if (result.Effort is { } effort)
        {
            parts.Add($"EFFORT {effort.ToString().ToUpperInvariant()}");
        }

        string? figure = null;

        if (result.Cost is { } cost)
        {
            if (cost.Priced)
            {
                figure = cost.Dollars.ToString("C4", CultureInfo.CurrentCulture);
            }
            else
            {
                parts.Add("UNPRICED MODEL");
            }
        }

        return new TurnProvenance(string.Join(Separator, parts), figure);
    }

    /// <summary>A route's name as words: <c>KeywordRouter</c> is <c>Keyword Router</c>.</summary>
    private static string Words(string name)
    {
        var words = new StringBuilder();

        foreach (var letter in name)
        {
            if (char.IsUpper(letter) && words.Length > 0)
            {
                words.Append(' ');
            }

            words.Append(letter);
        }

        return words.ToString();
    }
}
