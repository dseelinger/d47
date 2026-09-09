using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Diagnostics;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>Reads and writes <see cref="D47Settings"/>.</summary>
public sealed class SettingsStore(AppPaths paths, ILogger<SettingsStore> logger)
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        // This is the "unknown keys are kept and named" requirement (#368).
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Where the settings file is.</summary>
    public string SettingsFile => paths.SettingsFile;

    /// <summary>
    /// The keys the last <see cref="Load"/> found that this build does not know, by their path through
    /// the document ("vr.controlers").
    /// </summary>
    public IReadOnlyList<string> UnknownKeys { get; private set; } = [];

    /// <summary>A missing file yields defaults — that is a first run, not a failure.</summary>
    public D47Settings Load()
    {
        UnknownKeys = [];

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

        // Named, not refused (#368).
        var kept = new List<string>();
        Collect(settings, string.Empty, kept);
        UnknownKeys = kept;

        foreach (var key in kept)
        {
            logger.LogWarning(
                "{Path} holds '{Key}', which this build does not know; kept as written",
                paths.SettingsFile,
                key);
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

        // Subsystem keys come back in whatever casing the file used.
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

        // The ambient interval was in minutes and is now in seconds.
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

        // The panel pitch was the whole tilt angle and is now a trim on top of one derived from distance and
        // drop, so every value already on disk means something else than it did.
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

        // Opacity was one of the six settings each surface kept a copy of, and is now one knob for both.
        if (settings.Vr.OpacityShared < OpacitySharing)
        {
            var shared = Shared(settings.Vr.Panel.Opacity, settings.Vr.Mini.Opacity);

            settings = settings with
            {
                Vr = settings.Vr with { Opacity = shared, OpacityShared = OpacitySharing },
            };

            logger.LogInformation(
                "Panel opacity is now one setting for both surfaces; {Panel:0.00} and {Mini:0.00} became {Shared:0.00}",
                settings.Vr.Panel.Opacity,
                settings.Vr.Mini.Opacity,
                shared);
        }

        logger.LogInformation("Loaded settings from {Path}", paths.SettingsFile);
        return settings;
    }

    /// <summary>Which revision of the shared-opacity repair this build performs.</summary>
    private const int OpacitySharing = 1;

    /// <summary>Which of the two old values becomes the one.</summary>
    private static double Shared(double panel, double mini)
    {
        const double WhatOpacityDefaultsTo = 0.95;

        if (panel != WhatOpacityDefaultsTo)
        {
            return panel;
        }

        return mini != WhatOpacityDefaultsTo ? mini : WhatOpacityDefaultsTo;
    }

    /// <summary>Which revision of the pitch repair this build performs.</summary>
    private const int PitchRepair = 1;

    /// <summary>What the old pitch was in absolute degrees, expressed as a trim on the derived tilt.</summary>
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

    /// <summary>
    /// Every key the document carried that no property claimed, by its path through the document
    /// (#368).
    /// </summary>
    private static void Collect(object node, string path, List<string> found)
    {
        foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (property.GetValue(node) is not { } value)
            {
                continue;
            }

            if (property.IsDefined(typeof(JsonExtensionDataAttribute), inherit: true))
            {
                if (value is IDictionary<string, JsonElement> bag)
                {
                    found.AddRange(bag.Keys.Select(key => path.Length == 0 ? key : $"{path}.{key}"));
                }

                continue;
            }

            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            Descend(value, path.Length == 0 ? name : $"{path}.{name}", found);
        }
    }

    /// <summary>
    /// One value: a settings record is walked, a list of them is walked by index, a dictionary of them
    /// by key, and anything else — a number, a string, an enum — holds no bag and is left alone.
    /// </summary>
    private static void Descend(object value, string path, List<string> found)
    {
        var type = value.GetType();

        if (type.Assembly == typeof(D47Settings).Assembly && !type.IsEnum)
        {
            Collect(value, path, found);
            return;
        }

        // A dictionary's value is where a record — and so a bag — can be: the voices kept per provider are
        // exactly that shape.
        if (value is IDictionary map)
        {
            foreach (DictionaryEntry entry in map)
            {
                if (entry.Value is { } held)
                {
                    Descend(held, $"{path}.{entry.Key}", found);
                }
            }

            return;
        }

        if (value is not IEnumerable items || value is string)
        {
            return;
        }

        var index = 0;

        foreach (var item in items)
        {
            if (item is not null)
            {
                Descend(item, $"{path}[{index}]", found);
            }

            index++;
        }
    }

    public void Save(D47Settings settings)
    {
        AtomicFile.WriteAllText(paths.SettingsFile, JsonSerializer.Serialize(settings, Json));
        logger.LogInformation("Wrote settings to {Path}", paths.SettingsFile);
    }
}
