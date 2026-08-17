using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Diagnostics;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// Reads and writes <see cref="D47Settings"/>. Separate from the secret store because one
/// loader cannot both fail loudly and shrug (architecture.md §5 D6).
/// </summary>
public sealed class SettingsStore(AppPaths paths, ILogger<SettingsStore> logger)
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        // This is the "unknown keys are rejected" requirement. A typo in a hand-edited
        // settings file surfaces as an error naming the key, not as a silently ignored setting.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// A missing file yields defaults — that is a first run, not a failure. Anything else
    /// wrong with the file throws, because silently continuing on defaults would discard
    /// the Commander's configuration without saying so.
    /// </summary>
    public D47Settings Load()
    {
        if (!File.Exists(paths.SettingsFile))
        {
            logger.LogInformation("No settings file at {Path}; using defaults", paths.SettingsFile);
            return new D47Settings();
        }

        string text;
        try
        {
            text = File.ReadAllText(paths.SettingsFile);
        }
        catch (IOException ex)
        {
            throw new SettingsLoadException(paths.SettingsFile, "the file could not be read", ex);
        }

        D47Settings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<D47Settings>(text, Json);
        }
        catch (JsonException ex)
        {
            throw new SettingsLoadException(paths.SettingsFile, ex.Message, ex);
        }

        if (settings is null)
        {
            throw new SettingsLoadException(paths.SettingsFile, "the file contained only null");
        }

        var unknown = settings.Logging.Subsystems.Keys
            .Where(k => Subsystems.Canonical(k) is null)
            .ToArray();

        if (unknown.Length > 0)
        {
            throw new SettingsLoadException(
                paths.SettingsFile,
                $"unknown subsystem(s) under logging.subsystems: {string.Join(", ", unknown)}");
        }

        // Subsystem keys come back in whatever casing the file used. Normalising here means
        // every consumer can look them up by the canonical Subsystems constant.
        settings = settings with
        {
            Logging = settings.Logging with
            {
                Subsystems = settings.Logging.Subsystems.ToDictionary(
                    pair => Subsystems.Canonical(pair.Key)!,
                    pair => pair.Value,
                    StringComparer.Ordinal),
            },
        };

        // The ambient interval was in minutes and is now in seconds. A file written before that
        // carries the old key, so its value is converted rather than dropped, and cleared as it
        // is converted so this happens once.
        //
        // A file still holding the old default is taken to have not chosen: fifteen minutes was
        // what every file got without anybody asking for it, and carrying it forward would mean
        // the new default reached nobody who had ever run d47. A value that differs is a
        // decision and is kept, to the second.
        if (settings.Callouts.AmbientMinutes is { } minutes)
        {
            const int WhatMinutesUsedToDefaultTo = 15;

            settings = settings with
            {
                Callouts = settings.Callouts with
                {
                    AmbientSeconds = minutes == WhatMinutesUsedToDefaultTo
                        ? new CalloutSettings().AmbientSeconds
                        : minutes * 60,
                    AmbientMinutes = null,
                },
            };

            logger.LogInformation(
                "The ambient interval is now in seconds; {Minutes} became {Seconds}",
                minutes,
                settings.Callouts.AmbientSeconds);
        }

        // The panel pitch was the whole tilt angle and is now a trim on top of one derived from
        // distance and drop, so every value already on disk means something else than it did.
        // Converted rather than dropped, once, and counted by a revision so a corrected repair can
        // still reach a file this one has stamped.
        if (settings.Vr.PitchRepaired < PitchRepair)
        {
            settings = settings with
            {
                Vr = settings.Vr with
                {
                    Panel = Retrim(settings.Vr.Panel),
                    Mini = Retrim(settings.Vr.Mini),
                    PitchRepaired = PitchRepair,
                },
            };

            logger.LogInformation(
                "The VR panel pitch is now a trim on a derived tilt; panel became {Panel:0.0}° and mini {Mini:0.0}°",
                settings.Vr.Panel.Pitch,
                settings.Vr.Mini.Pitch);
        }

        logger.LogInformation("Loaded settings from {Path}", paths.SettingsFile);
        return settings;
    }

    /// <summary>
    /// Which revision of the pitch repair this build performs. Raised only if the repair below is
    /// found to be wrong, never routinely — each raise re-decides an angle a Commander may have
    /// since set deliberately.
    /// </summary>
    private const int PitchRepair = 1;

    /// <summary>
    /// What the old pitch was in absolute degrees, expressed as a trim on the derived tilt.
    /// <para>
    /// A file still holding the old default is taken to have <em>not</em> chosen: 12° is what
    /// every file got without anybody asking, and carrying it forward as a trim would tilt every
    /// panel 12° past the eyes it now aims at. Anything else is a decision, and is preserved
    /// exactly — the trim that reproduces that angle is the angle minus the derived one, so the
    /// Commander's panel does not move.
    /// </para>
    /// </summary>
    private static VrSurfaceSettings Retrim(VrSurfaceSettings surface)
    {
        const double WhatPitchUsedToDefaultTo = 12;

        if (surface.Pitch == WhatPitchUsedToDefaultTo)
        {
            return surface with { Pitch = 0 };
        }

        var derived = Vr.VrPlacementMath.EyeFacingPitch(
            (float)surface.Distance,
            (float)surface.Drop) * 180d / Math.PI;

        return surface with { Pitch = surface.Pitch - derived };
    }

    public void Save(D47Settings settings)
    {
        AtomicFile.WriteAllText(paths.SettingsFile, JsonSerializer.Serialize(settings, Json));
        logger.LogInformation("Wrote settings to {Path}", paths.SettingsFile);
    }
}
