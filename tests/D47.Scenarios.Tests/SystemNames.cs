using System.Text.RegularExpressions;

namespace D47.Scenarios.Tests;

/// <summary>What a resolver can say about a name. Three answers, because two would be a lie.</summary>
public enum SystemVerdict
{
    Exists,

    /// <summary>
    /// The resolver looked and the galaxy does not contain it. The only verdict that fails an
    /// assertion — an invention, in Phase 23's sense.
    /// </summary>
    DoesNotExist,

    /// <summary>
    /// Nobody could say. No resolver composed, or the one composed could not be reached. Reported
    /// as unchecked rather than as a pass.
    /// </summary>
    Unknown,
}

/// <summary>
/// Whether a system name names something real.
/// <para>
/// The seam exists because this is the one quality assertion that needs somebody off this machine
/// to answer it, and the free tier has nobody to ask. A null resolver reports unchecked, which the
/// report then prints beside the green — the alternative is a suite that says a model invented
/// nothing when it never looked.
/// </para>
/// </summary>
public interface ISystemResolver
{
    SystemVerdict Resolve(string systemName);
}

/// <summary>
/// Pulling system names out of a reply, so the invention check is a property rather than a string
/// comparison (docs/plans/phase-30-prove-the-model-behaves.md, finding 4).
/// </summary>
public static partial class SystemNames
{
    /// <summary>
    /// The procedural grammar, as it appears inside a sentence.
    /// <para>
    /// <b>Deliberately only the procedural form.</b> <see cref="D47.Core.Knowledge.SystemName"/>'s
    /// own pattern is validated against 4,746 real names with nothing procedural failing to parse,
    /// so a match here is a system name and not an English phrase that happened to be capitalised.
    /// Hand-named systems — Sol, Shinrarta Dezhra, Arumclaw — are not extracted, because no
    /// pattern separates a real one from a plausible noun, and a check that guessed would produce
    /// false inventions, which is worse than a narrower check that does not.
    /// </para>
    /// <para>
    /// That narrowness is stated in the report rather than left to be discovered. It is also where
    /// the value is: a procedural name is the thing a model invents most confidently, because it
    /// is generated from a grammar the model has plainly learned.
    /// </para>
    /// </summary>
    [GeneratedRegex(
        @"\b([A-Z][A-Za-z]*(?:[ '-][A-Z][A-Za-z]*)* [A-Z][A-Z]-[A-Z] [a-h](?:\d+-)?\d+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Procedural { get; }

    /// <summary>Every system-shaped name in <paramref name="text"/>, deduplicated, in order.</summary>
    public static IReadOnlyList<string> Candidates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();

        foreach (Match match in Procedural.Matches(text))
        {
            var name = match.Groups[1].Value.Trim();

            if (seen.Add(name))
            {
                found.Add(name);
            }
        }

        return found;
    }
}
