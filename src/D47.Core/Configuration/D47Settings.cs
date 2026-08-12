using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// The settings store's whole shape. Anything not declared here is an unknown key and is
/// rejected on load (list.md Phase 1).
/// </summary>
public sealed record D47Settings
{
    public int SchemaVersion { get; init; } = 1;

    public LoggingSettings Logging { get; init; } = new();
}

public sealed record LoggingSettings
{
    /// <summary>Applies to any subsystem with no explicit entry below.</summary>
    public LogLevel Default { get; init; } = LogLevel.Information;

    /// <summary>
    /// Per-subsystem overrides, keyed by <see cref="Diagnostics.Subsystems"/> name. Unknown
    /// subsystem names are rejected on load along with any other unknown key.
    /// </summary>
    public IReadOnlyDictionary<string, LogLevel> Subsystems { get; init; } =
        new Dictionary<string, LogLevel>();
}
