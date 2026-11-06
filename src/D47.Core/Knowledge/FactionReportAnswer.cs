using System.Globalization;
using System.Text;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The spoken answer to "how is this faction doing", built from the rows the Faction page draws.</summary>
public static class FactionReportAnswer
{
    private const int Named = 3;

    public static string Describe(FactionReport report, SystemStandings standings)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(standings);

        if (report.Systems.Count == 0)
        {
            return $"The galaxy service lists no systems for {report.Name}.";
        }

        var rows = FactionRows.Of(report, standings);
        var conflicts = FactionConflictRows.Of(report, standings);
        var controlled = rows.Count(row => row.Controls);

        var said = new StringBuilder(report.Name);

        var identity = string.Join(
            ", ",
            new[]
            {
                report.Government is { Length: > 0 } government ? $"government {government}" : null,
                report.Allegiance is { Length: > 0 } allegiance ? $"allegiance {allegiance}" : null,
            }.OfType<string>());

        said.Append(identity.Length > 0 ? $": {identity}. " : ". ");
        said.Append(CultureInfo.InvariantCulture, $"Present in {Systems(rows.Count)}, controls {controlled}. ");

        if (conflicts.Count == 0)
        {
            said.Append("No conflicts active or pending. ");
        }
        else
        {
            said.Append("Conflicts: ");
            said.Append(string.Join("; ", conflicts.Take(Named).Select(Conflict)));
            said.Append(conflicts.Count > Named ? $"; and {conflicts.Count - Named} more. " : ". ");
        }

        var weakest = rows
            .Where(row => row.Controls && row.Influence.HasValue)
            .OrderBy(row => row.Influence)
            .ThenBy(row => row.System, StringComparer.CurrentCultureIgnoreCase)
            .Take(Named)
            .ToList();

        if (weakest.Count > 0)
        {
            said.Append("Lowest influence where it controls: ");
            said.Append(string.Join(
                ", ",
                weakest.Select(row => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{row.System} {Math.Round(row.Influence!.Value * 100, MidpointRounding.AwayFromZero):0}%"))));
            said.Append(". ");
        }

        foreach (var state in new[] { "Retreat", "Expansion" })
        {
            var count = rows.Count(row => row.Active.Concat(row.Pending).Contains(state, StringComparer.Ordinal));

            if (count > 0)
            {
                said.Append(CultureInfo.InvariantCulture, $"{state} in {Systems(count)}. ");
            }
        }

        return said.ToString().TrimEnd();
    }

    private static string Systems(int count) => count == 1 ? "1 system" : $"{count} systems";

    private static string Conflict(FactionConflict conflict)
    {
        var against = conflict.Kind switch
        {
            OpponentKind.Named => $" against {conflict.Opponent}",
            OpponentKind.OneOf => $" against one of {conflict.Candidates.Count} factions",
            _ => ", opponent not known yet",
        };

        return $"{conflict.Type} in {conflict.System}{against}{(conflict.Active ? string.Empty : ", pending")}";
    }
}
