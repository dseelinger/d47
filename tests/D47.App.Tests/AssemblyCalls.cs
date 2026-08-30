using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace D47.App.Tests;

/// <summary>
/// Reads compiled IL to answer "does anything call this, and what". The technique
/// <see cref="VrPointerTests"/> was built on, lifted out when a second file needed it
/// (<a href="https://github.com/dseelinger/d47/issues/198">#198</a>).
/// <para>
/// <b>It exists because some claims have no behaviour to test.</b> A method written, documented as
/// load-bearing and called by nothing at all has happened twice in the headset path, silently both
/// times. And the withdrawal of the motion controllers is the same shape read backwards: what has
/// to be true is that certain calls are <em>not</em> reachable, which no amount of running d47
/// without a headset can demonstrate.
/// </para>
/// <para>
/// A body is searched for the five bytes of a <c>call</c> or <c>callvirt</c> carrying a token
/// rather than decoded instruction by instruction. A decoder would be larger than everything it
/// serves, and the question is only ever asked one way round: an operand that happens to read as
/// this call is a false positive nothing here can produce, and a real call cannot hide from it.
/// </para>
/// </summary>
internal static class AssemblyCalls
{
    /// <summary>Whether any method body in the assembly issues a call to the named method.</summary>
    public static bool Anything(Assembly assembly, string method) => Callers(assembly, method).Count > 0;

    /// <summary>
    /// Every method in the assembly that calls the named one, as <c>Type.Method</c>.
    /// <para>
    /// The set rather than a yes or no, because the useful assertion is usually neither: it is
    /// that exactly one place reaches something, which is what makes that place the choke point a
    /// switch can be put in front of.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<string> Callers(Assembly assembly, string method) =>
        Callers(assembly, null, method);

    /// <summary>
    /// The same, narrowed to one declaring type.
    /// <para>
    /// <b>Worth reaching for whenever the name is a common one</b>, and <c>Start</c> is the case
    /// that proves it: asking who calls "Start" with no type answers with everything that starts
    /// anything, which is a question nobody meant to ask and an assertion that cannot hold.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Every token a call to this name could carry.
    /// <para>
    /// Both tables, and every match in each rather than the first. A call into another assembly is
    /// a <c>MemberRef</c> while a call within one is a <c>MethodDef</c>, so searching one table
    /// answers a different question than the one that was asked — and an assembly that both
    /// defines and references a name would otherwise have half its calls invisible. That matters
    /// most for the assertions that want a <em>zero</em>: a missed token reads as proof.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The type a member reference hangs off, when that is a plain type. A reference parented by
    /// a method spec or a module has no type name to compare, and answers null — which a
    /// type-qualified search treats as "not this one" and an unqualified one ignores.
    /// </summary>
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

        if (il is null)
        {
            return false;
        }

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
