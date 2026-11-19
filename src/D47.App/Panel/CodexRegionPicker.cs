using System.Globalization;
using D47.App.Controls;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Region page the Codex's Another region segment opens: every region with the kinds known and on record there.</summary>
public static class CodexRegionPicker
{
    public const string Key = "codex-region-choice";

    public const string Word = "Region";

    public const string Help = "Choose a region. The Codex then shows the kinds known there, and which of them you have found there.";

    public const string AnotherRegion = "Another region";

    /// <summary>The regions the Another region segment offers.</summary>
    public static int RegionCount => GalacticRegions.Names.Count;

    /// <summary>"Choose from 42": the Another region segment's status line.</summary>
    public static string ChooseFrom() => $"Choose from {CodexModel.Number(RegionCount)}";

    /// <summary>"Use the region I am in (Inner Orion Spur)".</summary>
    public static string UseHere(string name) => $"Use the region I am in ({name})";

    /// <summary>"1,012 kinds known · 143 on record", "none on record" when nothing is, and " · you are here" for the Commander's region.</summary>
    public static string Line(int kinds, int onRecord, bool here) =>
        $"{CodexModel.Number(kinds)} {(kinds == 1 ? "kind" : "kinds")} known · "
        + (onRecord > 0 ? $"{CodexModel.Number(onRecord)} on record" : "none on record")
        + (here ? " · you are here" : string.Empty);

    /// <summary>The aside of a region row.</summary>
    public static string Aside(int missing) => $"{CodexModel.Number(missing)} missing";

    /// <summary>
    /// The request listing every region: the Commander's first, then the regions with a kind on record, most first, then
    /// the rest by name. <paramref name="current"/> is the region the Codex counts in now, null for every region.
    /// </summary>
    public static PickerRequest Request(CodexLedger ledger, CodexTree tree, int? here, int? current, bool headset)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(tree);

        var rows = GalacticRegions.Names
            .Select(name => GalacticRegions.IdOf(name))
            .OfType<int>()
            .Select(id =>
            {
                var found = CodexNodeDiscoveries.For(ledger, tree.Root, id);

                return (Id: id, Name: GalacticRegions.NameOf(id)!, found.Counted, found.OnRecord, found.Missing);
            })
            .OrderBy(row => row.Id == here ? 0 : row.OnRecord > 0 ? 1 : 2)
            .ThenByDescending(row => row.Id == here ? 0 : row.OnRecord)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var byValue = rows.ToDictionary(row => row.Id.ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal);
        var hereName = here is { } at ? GalacticRegions.NameOf(at) : null;

        return new PickerRequest
        {
            Prompt = Word,
            Help = Help,
            Choices = [.. rows.Select(row => row.Id.ToString(CultureInfo.InvariantCulture))],
            Describe = value => byValue[value].Name,
            Current = current?.ToString(CultureInfo.InvariantCulture),
            DefaultDisplay = hereName,
            DefaultText = hereName is null ? null : UseHere(hereName),
            DetailOf = value => Line(byValue[value].Counted, byValue[value].OnRecord, byValue[value].Id == here),
            AsideOf = value => Aside(byValue[value].Missing),
            IsMarked = value => byValue[value].Id == here,
            ChoiceNoun = "regions",
            PageSize = headset ? FactionModel.PageSize : null,
        };
    }
}
