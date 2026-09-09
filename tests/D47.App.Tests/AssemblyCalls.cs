using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace D47.App.Tests;

/// <summary>Reads compiled IL to answer "does anything call this, and what".</summary>
internal static class AssemblyCalls
{
    /// <summary>Whether any method body in the assembly issues a call to the named method.</summary>
    public static bool Anything(Assembly assembly, string method) => Callers(assembly, method).Count > 0;

    /// <summary>Every method in the assembly that calls the named one, as <c>Type.Method</c>.</summary>
    public static IReadOnlyCollection<string> Callers(Assembly assembly, string method) =>
        Callers(assembly, null, method);

    /// <summary>The same, narrowed to one declaring type.</summary>
    public static IReadOnlyCollection<string> Callers(Assembly assembly, string? declaredOn, string method)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);

        var metadata = pe.GetMetadataReader();
        var tokens = TokensFor(metadata, declaredOn, method);

        if (tokens.Count == 0)
        {
            return [];
        }

        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var handle in metadata.MethodDefinitions)
        {
            var definition = metadata.GetMethodDefinition(handle);

            if (tokens.Any(token => BodyCalls(pe, definition, token)))
            {
                found.Add(Name(metadata, definition));
            }
        }

        return found;
    }

    /// <summary>
    /// Every method that reaches the named one through a chain of calls, as <c>Type.Method</c>. A method
    /// written as async or as an iterator is named as it was written rather than as its state machine.
    /// </summary>
    public static IReadOnlyCollection<string> Reaching(Assembly assembly, string? declaredOn, string method)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);

        var metadata = pe.GetMetadataReader();
        var bodies = Bodies(pe, metadata);
        var written = StateMachines(assembly);

        var found = new SortedSet<string>(StringComparer.Ordinal);
        var asked = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<(string? On, string Name)>();

        pending.Enqueue((declaredOn, method));

        while (pending.Count > 0)
        {
            var (on, name) = pending.Dequeue();

            if (!asked.Add($"{on}.{name}"))
            {
                continue;
            }

            var tokens = TokensFor(metadata, on, name);

            if (tokens.Count == 0)
            {
                continue;
            }

            foreach (var (caller, il) in bodies)
            {
                if (!tokens.Any(token => Calls(il, token)))
                {
                    continue;
                }

                // A state machine is reached through the method it was written as: nothing calls its
                // MoveNext by name.
                var reached = written.TryGetValue(caller, out var source) ? source : caller;

                if (!found.Add(reached))
                {
                    continue;
                }

                var dot = reached.LastIndexOf('.');
                pending.Enqueue((reached[..dot], reached[(dot + 1)..]));
            }
        }

        return found;
    }

    /// <summary>Every method body in the assembly, read once, as <c>Type.Method</c> and its IL.</summary>
    private static List<(string Name, byte[] Il)> Bodies(PEReader pe, MetadataReader metadata)
    {
        var bodies = new List<(string, byte[])>();

        foreach (var handle in metadata.MethodDefinitions)
        {
            var definition = metadata.GetMethodDefinition(handle);

            if (definition.RelativeVirtualAddress == 0)
            {
                continue;
            }

            if (pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes() is { } il)
            {
                bodies.Add((Name(metadata, definition), il));
            }
        }

        return bodies;
    }

    /// <summary>Every state machine's <c>MoveNext</c>, against the method it was written as.</summary>
    private static Dictionary<string, string> StateMachines(Assembly assembly)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in assembly.GetTypes())
        {
            foreach (var member in type.GetMethods(Declared))
            {
                var machine =
                    member.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                    ?? member.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType;

                if (machine is not null)
                {
                    written[$"{machine.Name}.MoveNext"] = $"{type.Name}.{member.Name}";
                }
            }
        }

        return written;
    }

    /// <summary>Whether one named method's body issues a call to another, wherever that one lives.</summary>
    public static bool Calls(Assembly assembly, string type, string caller, string callee) =>
        Callers(assembly, callee).Contains($"{type}.{caller}");

    /// <summary>Whether the assembly so much as names a method — defined or referenced.</summary>
    public static bool Knows(Assembly assembly, string method)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);

        return TokensFor(pe.GetMetadataReader(), null, method).Count > 0;
    }

    /// <summary>Every token a call to this name could carry.</summary>
    private static List<int> TokensFor(MetadataReader metadata, string? declaredOn, string method)
    {
        var tokens = new List<int>();

        foreach (var handle in metadata.MethodDefinitions)
        {
            var definition = metadata.GetMethodDefinition(handle);

            if (metadata.GetString(definition.Name) == method
                && Matches(declaredOn, metadata.GetString(metadata.GetTypeDefinition(definition.GetDeclaringType()).Name)))
            {
                tokens.Add(MetadataTokens.GetToken(handle));
            }
        }

        foreach (var handle in metadata.MemberReferences)
        {
            var reference = metadata.GetMemberReference(handle);

            if (metadata.GetString(reference.Name) == method && Matches(declaredOn, Owner(metadata, reference)))
            {
                tokens.Add(MetadataTokens.GetToken(handle));
            }
        }

        return tokens;
    }

    private static bool Matches(string? wanted, string? found) =>
        wanted is null || string.Equals(wanted, found, StringComparison.Ordinal);

    /// <summary>The type a member reference hangs off, when that is a plain type.</summary>
    private static string? Owner(MetadataReader metadata, MemberReference reference) => reference.Parent.Kind switch
    {
        HandleKind.TypeReference =>
            metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)reference.Parent).Name),
        HandleKind.TypeDefinition =>
            metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)reference.Parent).Name),
        _ => null,
    };

    private static string Name(MetadataReader metadata, MethodDefinition definition) =>
        $"{metadata.GetString(metadata.GetTypeDefinition(definition.GetDeclaringType()).Name)}"
        + $".{metadata.GetString(definition.Name)}";

    private static bool BodyCalls(PEReader pe, MethodDefinition definition, int token)
    {
        if (definition.RelativeVirtualAddress == 0)
        {
            return false;
        }

        var il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes();

        return il is not null && Calls(il, token);
    }

    /// <summary>Whether one body issues a call carrying this token.</summary>
    private static bool Calls(byte[] il, int token)
    {
        var wanted = new byte[5];
        BitConverter.TryWriteBytes(wanted.AsSpan(1), token);

        foreach (var opcode in new byte[] { 0x28, 0x6F })
        {
            wanted[0] = opcode;

            if (il.AsSpan().IndexOf(wanted) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
