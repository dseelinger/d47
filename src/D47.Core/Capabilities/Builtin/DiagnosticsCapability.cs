using System.Text;
using D47.Core.Configuration;
using D47.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The first capability, and the one that makes "Turn a subsystem up without restarting"
/// reachable rather than merely implemented (list.md Phase 1). It needs no game, no model,
/// no audio device and no headset, which is why it is the one Phase 1 ships.
/// </summary>
public static class DiagnosticsCapability
{
    public const string Id = "diagnostics";

    /// <summary>
    /// A closed vocabulary, emitted into the tool schema as an enum and validated before the
    /// handler runs. Also what the settings rows offer as choices.
    /// </summary>
    public static readonly IReadOnlyList<string> LogLevelNames =
        Enum.GetNames<LogLevel>();

    /// <summary>The settings row a subsystem's level lives in. One key, whoever is asking.</summary>
    public static string LevelRowFor(string subsystem) => $"logging.subsystems.{subsystem.ToLowerInvariant()}";

    public static CapabilityDescriptor Create(
        AppPaths paths,
        ILogVerbosityControl verbosity,
        SettingsService settings,
        string version)
    {
        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Foundation",
            Name = "Diagnostics",
            Summary = "Report where d47 keeps its files, and turn a subsystem's logging up or down without a restart.",
            Examples =
            [
                "what's your status",
                "turn journal logging up to debug",
                "set voice logging back to information",
            ],
            // Phrases rather than words, for the same reason as the journal capability: a bare
            // "status" hijacks any sentence containing it and answers a question nobody asked.
            Keywords =
            [
                "your status",
                "status report",
                "app status",
                "diagnostics",
                "log level",
                "logging level",
            ],
            Display = new CapabilityDisplay { PanelTitle = "Diagnostics", Order = 10 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_app_status",
                    Description =
                        "Report d47's version, where it keeps its writable files, and the current log level of every subsystem.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(DescribeStatus(paths, verbosity, version))),
                },
                new ToolDefinition
                {
                    Name = "set_log_verbosity",
                    Description =
                        "Change one subsystem's minimum log level. Takes effect immediately, with no restart.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "subsystem",
                            Type = ToolParameterType.String,
                            Description = "Which subsystem to change the log level for.",
                            Required = true,
                            AllowedValues = Subsystems.All,
                        },
                        new ToolParameter
                        {
                            Name = "level",
                            Type = ToolParameterType.String,
                            Description = "The new minimum level. Trace is the most detailed; None silences the subsystem.",
                            Required = true,
                            AllowedValues = LogLevelNames,
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(SetVerbosity(arguments, settings)),
                },
            ],
            Settings = BuildSettingRows(),
        };
    }

    private static string DescribeStatus(AppPaths paths, ILogVerbosityControl verbosity, string version)
    {
        var report = new StringBuilder();
        report.AppendLine($"d47 {version}");
        report.AppendLine($"Installed at: {paths.InstallRoot}");
        report.AppendLine($"Writable data: {paths.Data}");
        report.AppendLine($"Logs: {paths.Logs}");
        report.AppendLine("Log levels:");

        foreach (var subsystem in Subsystems.All)
        {
            var level = verbosity.Levels.TryGetValue(subsystem, out var known) ? known.ToString() : "(default)";
            report.AppendLine($"  {subsystem}: {level}");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// Writes the settings row rather than the level switch directly. The row is the one path:
    /// it persists the change and, through the verbosity control's subscription, makes it live
    /// on the next log line. A tool that set only the switch would look identical from the
    /// outside and be forgotten at the next restart.
    /// </summary>
    private static ToolResult SetVerbosity(ToolArguments arguments, SettingsService settings)
    {
        // The registry has already checked both values against the declared vocabularies, so
        // anything failing here is a genuine mismatch rather than a bad model guess.
        if (!arguments.TryGetString("subsystem", out var requested) ||
            Subsystems.Canonical(requested) is not { } subsystem)
        {
            return ToolResult.Error($"'{arguments.Values.GetValueOrDefault("subsystem")}' is not a known subsystem.");
        }

        if (!arguments.TryGetString("level", out var levelName) ||
            !Enum.TryParse<LogLevel>(levelName, ignoreCase: true, out var level))
        {
            return ToolResult.Error($"'{levelName}' is not a log level.");
        }

        var applied = settings.Apply(LevelRowFor(subsystem), level.ToString(), SettingsCaller.Model);

        return applied.Ok
            ? ToolResult.Ok($"{subsystem} logging is now at {level}.")
            : ToolResult.Error(applied.Message);
    }

    private static IReadOnlyList<SettingRow> BuildSettingRows()
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = "logging.default",
                Label = "Default log level",
                Help = "Applies to any subsystem without its own level below.",
                Kind = SettingKind.Choice,
                Choices = LogLevelNames,
                DefaultDisplay = nameof(LogLevel.Information),
                DocsAnchor = "default-log-level",
                Binding = new SettingBinding
                {
                    Read = s => s.Logging.Default.ToString(),
                    Write = (s, v) => s with
                    {
                        Logging = s.Logging with { Default = ParseLevel(v) ?? LogLevel.Information },
                    },
                },
            },
        };

        // One row per subsystem, projected from the same closed set the tool schema uses.
        // There is no second list to keep in step.
        rows.AddRange(Subsystems.All.Select(subsystem => new SettingRow
        {
            Key = LevelRowFor(subsystem),
            Label = $"{subsystem} log level",
            Help = $"Minimum level for the {subsystem} subsystem. Changes apply immediately.",
            Kind = SettingKind.Choice,
            Choices = LogLevelNames,
            DocsAnchor = "subsystem-log-levels",
            Binding = new SettingBinding
            {
                // Absent from the dictionary is the "no override" state, which is what the
                // placeholder advertises — so clearing the row removes the entry rather than
                // writing the default level back as if it had been chosen.
                Read = s => s.Logging.Subsystems.TryGetValue(subsystem, out var level) ? level.ToString() : null,
                Write = (s, v) =>
                {
                    var levels = new Dictionary<string, LogLevel>(s.Logging.Subsystems, StringComparer.Ordinal);

                    if (ParseLevel(v) is { } level)
                    {
                        levels[subsystem] = level;
                    }
                    else
                    {
                        levels.Remove(subsystem);
                    }

                    return s with { Logging = s.Logging with { Subsystems = levels } };
                },
            },
        }));

        return rows;
    }

    private static LogLevel? ParseLevel(string? value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : null;
}
