using System.Text.Json.Nodes;

namespace D47.Core.Tests.Adventures;

/// <summary>Repair answers for a scripted model, built from a whole beats answer.</summary>
internal static class Repairs
{
    /// <summary>A repair answer replacing each numbered objective with the beat at that number in <paramref name="beats"/>.</summary>
    public static string Replacing(string beats, params int[] objectives) => ReplacingAfter(0, beats, objectives);

    /// <summary>As <see cref="Replacing"/> for a chapter rewritten after <paramref name="done"/> objectives, which the replacements are numbered after.</summary>
    public static string ReplacingAfter(int done, string beats, params int[] objectives)
    {
        var written = JsonNode.Parse(beats)!["beats"]!.AsArray();
        var replacements = new JsonArray();

        foreach (var objective in objectives)
        {
            var beat = written[objective - 1]!.DeepClone().AsObject();
            beat.Insert(0, "objective", done + objective);
            replacements.Add(beat);
        }

        return new JsonObject { ["replacements"] = replacements }.ToJsonString();
    }

    /// <summary>As <see cref="Replacing"/>, with the opening of <paramref name="beats"/> written again.</summary>
    public static string ReplacingOpening(string beats, params int[] objectives) => With(beats, ["opening", "openingSpeaker"], objectives);

    /// <summary>As <see cref="Replacing"/>, with the destination of <paramref name="beats"/> named again.</summary>
    public static string ReplacingDestination(string beats, params int[] objectives) => With(beats, ["destination"], objectives);

    private static string With(string beats, string[] fields, int[] objectives)
    {
        var written = JsonNode.Parse(beats)!.AsObject();
        var answer = JsonNode.Parse(Replacing(beats, objectives))!.AsObject();

        foreach (var field in fields)
        {
            if (written[field] is { } value)
            {
                answer[field] = value.DeepClone();
            }
        }

        return answer.ToJsonString();
    }
}
