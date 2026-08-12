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
    public async Task<ToolResult> InvokeAsync(
        string toolName,
        ToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_byToolName.TryGetValue(toolName, out var found))
        {
            return ToolResult.Error($"No such tool '{toolName}'.");
        }

        var tool = found.Tool;

        // Counted on the attempt rather than on success. A capability the Commander keeps
        // reaching for is one worth ranking highly even on the days it fails.
        lock (_uses)
        {
            var id = found.Capability.Descriptor.Id;
            _uses[id] = _uses.GetValueOrDefault(id, 0) + 1;
        }

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

        try
        {
            return await tool.Handler(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"{toolName} failed: {ex.Message}");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex ToolNamePattern();
}
