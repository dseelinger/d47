namespace D47.Core.Knowledge;

/// <summary>The <c>find_body</c> arguments a codex entry's Search tile runs.</summary>
public static class CodexSearch
{
    /// <summary>Canonn entries the service has no <c>landmark_subtype</c> value for: Green Gas Giant with Water Life and Green Class V Gas Giant.</summary>
    private static readonly long[] NoLandmark = [1200402, 1200902];

    /// <summary>
    /// A Canonn entry's codex search, or a kind's body search on its own fields, in the region where D47 can name it;
    /// null for an entry with no search, which is a kind group or a Canonn entry the service does not record.
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Arguments(CodexEntry entry, int? regionId)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        if (entry.Kind is not { } kind)
        {
            if (entry.EntryId is { } id && NoLandmark.Contains(id))
            {
                return null;
            }

            arguments["codex_entry"] = entry.Name;
        }
        else
        {
            if (entry.Regions.Count == 0)
            {
                return null;
            }

            arguments["body_type"] = kind.SubType;

            if (kind.Band is { } band)
            {
                arguments["luminosity"] = BodyCatalogue.MatchLuminosity(band) ?? band;
            }

            if (kind.Terraformable is "yes" or "no")
            {
                arguments["terraformable"] = kind.Terraformable == "yes" ? "true" : "false";
            }

            if (kind.Atmosphere is { } atmosphere)
            {
                arguments["atmosphere"] = atmosphere;
            }

            if (kind.Rings is { } rings)
            {
                arguments["ring_type"] = string.Join(", ", rings.Split(',', StringSplitOptions.TrimEntries));

                if (rings != BodyCatalogue.NoRings)
                {
                    arguments["rings_exactly"] = "true";
                }
            }
        }

        if (regionId is { } region && GalacticRegions.NameOf(region) is { } regionName)
        {
            arguments["region"] = regionName;
        }

        return arguments;
    }
}
