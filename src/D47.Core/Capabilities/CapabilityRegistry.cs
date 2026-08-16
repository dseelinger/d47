using System.Text.RegularExpressions;

namespace D47.Core.Capabilities;

/// <summary>A descriptor plus the canonical schema bytes, computed once at registration.</summary>
public sealed class RegisteredCapability
{
    internal RegisteredCapability(CapabilityDescriptor descriptor)
    {
        Descriptor = descriptor;
        ToolSchemas = descriptor.Tools.ToDictionary(t => t.Name, ToolSchemaWriter.Canonical, StringComparer.Ordinal);
    }

    public CapabilityDescriptor Descriptor { get; }

    /// <summary>Tool name to canonical JSON Schema. Computed once, never recomputed per turn.</summary>
    public IReadOnlyDictionary<string, string> ToolSchemas { get; }
}

public sealed class CapabilityRegistrationException(string message) : Exception(message);

/// <summary>
/// Who is making a tool call. <b>Protected is a property of the caller, not of the modality</b>
/// (architecture.md §7): the same tool the panel and the keyword router may run is one the model
/// may not, and the only thing that can tell those apart is who asked.
/// </summary>
public enum ToolCaller
{
    /// <summary>The panel, a hotkey, or the model-free keyword router. Trusted, because a person.</summary>
    Commander,

    /// <summary>The language model, which reads untrusted text and is therefore refused.</summary>
    Model,
}

/// <summary>One tool call and how it went.</summary>
/// <param name="Succeeded">
/// Whether the capability's own handler came back clean.
/// <para>
/// A call refused before the handler ran — an unknown parameter, a missing required one, a
/// value outside a closed vocabulary — counts as succeeded, because that is the registry's
/// validation doing its job rather than the capability failing. It is the same reading as
/// <c>SettingApplyStatus.Rejected</c> on a settings row: the guard held.
/// </para>
/// </param>
public sealed record ToolInvocation(string Tool, bool Succeeded);

/// <summary>
/// The single source for capabilities (architecture.md §5 D5). Built once at startup from
/// descriptors and never mutated afterwards, because immutability is what keeps tool schemas
/// byte-identical across turns.
/// </summary>
public sealed partial class CapabilityRegistry
{
    private readonly Dictionary<string, RegisteredCapability> _byId;
    private readonly Dictionary<string, (RegisteredCapability Capability, ToolDefinition Tool)> _byToolName;

    /// <summary>
    /// How many times each capability has actually been used this session. Spoken help is
    /// ranked by it (list.md Phase 6, "ranked by real usage"), so the capabilities a Commander
    /// reaches for come first instead of whichever happened to be registered first.
    /// <para>
    /// Session-scoped rather than persisted. Persisting it means a store, a schema and a
    /// migration for a ranking, and the phase that needs help to survive a restart can add
    /// those; what it must not do is invent a usage history that never happened.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, int> _uses = new(StringComparer.Ordinal);

    private CapabilityRegistry(IReadOnlyList<RegisteredCapability> capabilities)
    {
        All = capabilities;
        _byId = capabilities.ToDictionary(c => c.Descriptor.Id, StringComparer.Ordinal);
        _byToolName = capabilities
            .SelectMany(c => c.Descriptor.Tools.Select(t => (Capability: c, Tool: t)))
            .ToDictionary(x => x.Tool.Name, StringComparer.Ordinal);
    }

    /// <summary>Registration order, which is a stable order for anything projected from it.</summary>
    public IReadOnlyList<RegisteredCapability> All { get; }

    public IEnumerable<string> ToolNames => _byToolName.Keys;

    /// <summary>
    /// Validates the whole set and fails at startup rather than at first use. A duplicate tool
    /// name is a bug that would otherwise surface as the model calling the wrong capability.
    /// </summary>
    public static CapabilityRegistry Build(IEnumerable<CapabilityDescriptor> descriptors)
    {
        var registered = new List<RegisteredCapability>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenTools = new HashSet<string>(StringComparer.Ordinal);
        var seenSettingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            if (!SlugPattern().IsMatch(descriptor.Id))
            {
                throw new CapabilityRegistrationException(
                    $"Capability id '{descriptor.Id}' must be kebab-case: it is also a documentation filename and a URL.");
            }

            if (!seenIds.Add(descriptor.Id))
            {
                throw new CapabilityRegistrationException($"Duplicate capability id '{descriptor.Id}'.");
            }

            foreach (var tool in descriptor.Tools)
            {
                if (!ToolNamePattern().IsMatch(tool.Name))
                {
                    throw new CapabilityRegistrationException(
                        $"Tool name '{tool.Name}' on capability '{descriptor.Id}' must be snake_case.");
                }

                // Tool names are a flat namespace as far as the model is concerned.
                if (!seenTools.Add(tool.Name))
                {
                    throw new CapabilityRegistrationException(
                        $"Duplicate tool name '{tool.Name}' (capability '{descriptor.Id}').");
                }
            }

            foreach (var row in descriptor.Settings)
            {
                if (!seenSettingKeys.Add(row.Key))
                {
                    throw new CapabilityRegistrationException(
                        $"Duplicate settings key '{row.Key}' (capability '{descriptor.Id}').");
                }
            }

            registered.Add(new RegisteredCapability(descriptor));
        }

        return new CapabilityRegistry(registered);
    }

    public RegisteredCapability? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Raised once per tool call, after the outcome is known. Exists for the coverage recorder,
    /// which is off unless asked for; nothing in the shipped path subscribes.
    /// <para>
    /// Deliberately after the fact rather than on entry: "it ran" and "it ran and worked" are
    /// different answers, and only the second one tells a person by hand whether to go back to
    /// it. A cancelled turn announces nothing at all, because an interrupted call is not a
    /// verdict on the tool.
    /// </para>
    /// </summary>
    public event Action<ToolInvocation>? ToolInvoked;

    /// <summary>How often a capability has been invoked this session.</summary>
    public int UseCountOf(string capabilityId)
    {
        lock (_uses)
        {
            return _uses.GetValueOrDefault(capabilityId, 0);
        }
    }

    /// <summary>
    /// Runs a tool. Arguments are validated against the declared schema first, so a
    /// hallucinated parameter or an out-of-vocabulary value never reaches capability code.
    /// A handler that throws becomes an error result rather than taking the turn down: a
    /// capability failing is a state, not a crash (list.md Phase 3).
    /// </summary>
    /// <param name="caller">
    /// Who asked. Defaults to <see cref="ToolCaller.Commander"/> because the panel and the
    /// model-free router are the callers that need no qualification; the model path says so
    /// explicitly, and that is what makes <see cref="ToolDefinition.Protected"/> mean anything.
    /// </param>
    public async Task<ToolResult> InvokeAsync(
        string toolName,
        ToolArguments arguments,
        CancellationToken cancellationToken = default,
        ToolCaller caller = ToolCaller.Commander)
    {
        if (!_byToolName.TryGetValue(toolName, out var found))
        {
            return ToolResult.Error($"No such tool '{toolName}'.");
        }

        var tool = found.Tool;

        // Before the use is counted and before anything is validated: a refused call is not a use
        // of the capability, and there is nothing to validate on a call that will not be made.
        if (tool.Protected && caller == ToolCaller.Model)
        {
            return ToolResult.Error(
                $"'{toolName}' is not something I can do on my own — the Commander performs it from the "
                + "panel or by saying so directly.");
        }

        // Counted on the attempt rather than on success. A capability the Commander keeps
        // reaching for is one worth ranking highly even on the days it fails.
        lock (_uses)
        {
            var id = found.Capability.Descriptor.Id;
            _uses[id] = _uses.GetValueOrDefault(id, 0) + 1;
        }

        if (Invalid(toolName, tool, arguments) is { } refusal)
        {
            // Announced, and announced clean: the tool was reached for, and what stopped the
            // call was the guard rather than the capability.
            ToolInvoked?.Invoke(new ToolInvocation(toolName, Succeeded: true));
            return refusal;
        }

        ToolResult result;

        try
        {
            result = await tool.Handler(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result = ToolResult.Error($"{toolName} failed: {ex.Message}");
        }

        ToolInvoked?.Invoke(new ToolInvocation(toolName, !result.IsError));

        return result;
    }

    /// <summary>
    /// Why this call cannot be made, or null if it can. Split out so the invoke path has one
    /// place it announces the outcome from, rather than one per way of being wrong.
    /// </summary>
    private static ToolResult? Invalid(string toolName, ToolDefinition tool, ToolArguments arguments)
    {
        foreach (var parameter in tool.Parameters)
        {
            var present = arguments.TryGetString(parameter.Name, out var value);

            if (parameter.Required && !present)
            {
                return ToolResult.Error($"Tool '{toolName}' requires '{parameter.Name}'.");
            }

            if (present && parameter.AllowedValues.Count > 0 &&
                !parameter.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return ToolResult.Error(
                    $"'{value}' is not a valid {parameter.Name}. Expected one of: {string.Join(", ", parameter.AllowedValues)}.");
            }
        }

        foreach (var supplied in arguments.Values.Keys)
        {
            if (!tool.Parameters.Any(p => string.Equals(p.Name, supplied, StringComparison.Ordinal)))
            {
                return ToolResult.Error($"Tool '{toolName}' has no parameter '{supplied}'.");
            }
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex ToolNamePattern();
}
