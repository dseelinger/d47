using D47.Core.Capabilities;

namespace D47.App.Panel;

/// <summary>What a module's page reads and calls.</summary>
public sealed class ModulePageServices
{
    /// <summary>The text of resource <c>uri</c> of module <c>id</c>, or null when it did not answer.</summary>
    public required Func<string, string, CancellationToken, Task<string?>> ReadAsync { get; init; }

    /// <summary>The names of the tools module <c>id</c> lists now.</summary>
    public required Func<string, CancellationToken, Task<IReadOnlyList<string>>> ToolsAsync { get; init; }

    /// <summary>Calls a tool through the capability registry.</summary>
    public required Func<string, ToolArguments, ToolCaller, CancellationToken, Task<ToolResult>> InvokeAsync { get; init; }

    /// <summary>The name of whoever module <c>id</c> speaks as.</summary>
    public required Func<string, string> SpeakerName { get; init; }

    /// <summary>Logs a page that was refused or a press that failed: module id, what, why.</summary>
    public required Action<string, string, string> Warn { get; init; }

    /// <summary>Starts calling the handler, on any thread, with a module id and the uri it updated.</summary>
    public Action<Action<string, string>>? Watch { get; init; }
}
