using System.Globalization;
using D47.Core.Engineers;

namespace D47.App.Panel;

/// <summary>Draws a plan's engineers block, shared by every mode that has one (#195).</summary>
internal static class EngineerLines
{
    public static IReadOnlyList<LoadoutLine> For(PlanEngineerGroups groups)
    {
        if (groups.IsEmpty)
        {
            return [];
        }

        var lines = new List<LoadoutLine> { new("Engineers", LoadoutTone.Heading) };

        if (!groups.ProgressKnown)
        {
            lines.Add(new LoadoutLine(
                "I have not read your engineer progress yet, so I cannot say who you have unlocked."));

            lines.AddRange(groups.All.Select(entry => Row(entry, groups.Grade)));

            return lines;
        }

        if (groups.Unlocked.Count > 0)
        {
            lines.Add(new LoadoutLine("Unlocked", LoadoutTone.Heading));
            lines.AddRange(groups.Unlocked.Select(entry => Row(entry, groups.Grade)));
        }

        if (groups.Locked.Count > 0)
        {
            lines.Add(new LoadoutLine("Locked", LoadoutTone.Heading));
            lines.AddRange(groups.Locked.Select(entry => Row(entry, groups.Grade)));
        }

        return lines;
    }

    /// <summary>One row: the name, the grade held against the grade planned where both apply, the
    /// distance, and the system for the copy glyph beside it.</summary>
    private static LoadoutLine Row(PlanEngineerEntry entry, int? grade)
    {
        var distance = EngineerSay.Distance(entry.LightYears);
        var system = entry.Engineer.System;

        var head = entry.Held is { } held && grade is { } wanted
            ? held == wanted
                ? $"{entry.Engineer.Name}, grade {held.ToString(CultureInfo.InvariantCulture)}"
                : $"{entry.Engineer.Name}, grade {held.ToString(CultureInfo.InvariantCulture)} of "
                  + wanted.ToString(CultureInfo.InvariantCulture)
            : entry.Engineer.Name;

        var text = system is { Length: > 0 }
            ? $"{head} · {distance} · {system}"
            : $"{head} · {distance}";

        return new LoadoutLine(text, LoadoutTone.Body)
        {
            Copy = system is { Length: > 0 } ? new LoadoutCopy(system) : null,
        };
    }
}
