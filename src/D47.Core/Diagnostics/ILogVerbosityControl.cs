using Microsoft.Extensions.Logging;

namespace D47.Core.Diagnostics;

/// <summary>
/// Runtime per-subsystem verbosity (list.md Phase 1, "Turn a subsystem up without
/// restarting"). Implemented in D47.App over Serilog level switches — Core needs to read
/// and change levels without knowing what a sink is.
/// </summary>
public interface ILogVerbosityControl
{
    IReadOnlyDictionary<string, LogLevel> Levels { get; }

    /// <summary>Takes effect on the next log call. No restart, no file reload.</summary>
    void Set(string subsystem, LogLevel level);
}
