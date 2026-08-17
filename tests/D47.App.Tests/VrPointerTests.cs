using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using D47.App.Headset;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Grab-to-move needs the trigger, and the trigger arrives through <c>IVRInput</c> (list.md
/// Phase 9).
/// <para>
/// None of that can be asserted from here — it is a conversation with a running SteamVR. What can
/// be asserted is the shape of the defect that has now happened twice in this file's subject:
/// a method written, documented as load-bearing, and called by nothing at all. First it was
/// <c>VrOverlay.TakePointer</c>, and the panel could not be picked up. The road it opened turned
/// out to be a dead end regardless — SteamVR only runs its laser over its own dashboard, so those
/// events never arrive while Elite holds the headset — and the registration that replaced it has
/// exactly the same failure mode, silently, if nothing calls it.
/// </para>
/// <para>
/// A method nobody calls has no behaviour to be wrong about, so no behavioural test can see it.
/// That is why these reason about the assembly instead.
/// </para>
/// </summary>
public class VrPointerTests
{
    /// <summary>
    /// The panel is grab-to-move and asks for the pointer; captions are read rather than
    /// touched, and an interactive quad in front of the cockpit is a laser that stops on a label.
    /// </summary>
    [Fact]
    public void ThePanelAsksForThePointerAndTheCaptionsDoNot()
    {
        Assert.True(typeof(VrPanelSurface).IsAssignableTo(typeof(IVrSurfaceSource)));

        Assert.True(Declared<VrPanelSurface>());
        Assert.False(Declared<VrCaptionSurface>());
    }

    /// <summary>
    /// Something registers with SteamVR. Without this the action manifest is never loaded, and
    /// every downstream call succeeds while reporting that nothing is pressed — which is
    /// indistinguishable, from inside d47, from a Commander not touching the trigger.
    /// </summary>
    [Fact]
    public void SomethingInTheRuntimeActuallyRegistersForTheTrigger()
    {
        Assert.True(
            IsCalledInside(typeof(VrActionInput).Assembly, nameof(VrActionInput.Register)),
            $"nothing in {typeof(VrActionInput).Assembly.GetName().Name} calls {nameof(VrActionInput.Register)}");
    }

    /// <summary>
    /// And something reads it. An action set is active only for the frame it is passed in, so a
    /// registration nobody follows up on is a set that is never activated — the panel would be
    /// pointable and never grabbable.
    /// </summary>
    [Fact]
    public void SomethingInTheAppActuallyReadsTheTrigger()
    {
        Assert.True(
            IsCalledInside(typeof(VrHost).Assembly, nameof(VrActionInput.TriggerHeld)),
            $"nothing in {typeof(VrHost).Assembly.GetName().Name} calls {nameof(VrActionInput.TriggerHeld)}");
    }

    /// <summary>What a surface source says about the pointer, read off the type's own default.</summary>
    private static bool Declared<T>() =>
        (bool)typeof(T).GetProperty(nameof(IVrSurfaceSource.TakesPointer))!
            .GetGetMethod()!
            .Invoke(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T)), null)!;

    /// <summary>
    /// Whether any method body in the assembly issues a call to the named method.
    /// <para>
    /// The body is searched for the five bytes of a <c>call</c> or <c>callvirt</c> carrying the
    /// method's own token rather than decoded instruction by instruction. A decoder would be the
    /// larger half of this file and the question is only ever asked one way round: an operand
    /// that happens to read as a call to this token is a false positive nothing here can
    /// produce, and a real call cannot hide from it.
    /// </para>
    /// </summary>
    private static bool IsCalledInside(Assembly assembly, string method)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);

        var metadata = pe.GetMetadataReader();

        // A call to a method in another assembly is a MemberRef rather than a MethodDef, so both
        // tables are searched. Without the second, asking whether the app calls into D47.Vr always
        // answers "that method is not here" — a passing assertion about the wrong question.
        var declared = metadata.MethodDefinitions.FirstOrDefault(
            handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == method);

        var reference = metadata.MemberReferences.FirstOrDefault(
            handle => metadata.GetString(metadata.GetMemberReference(handle).Name) == method);

        Assert.False(
            declared.IsNil && reference.IsNil,
            $"{method} is neither defined nor referenced in {assembly.GetName().Name}");

        var token = MetadataTokens.GetToken(declared.IsNil ? reference : declared);

        var wanted = new byte[5];
        BitConverter.TryWriteBytes(wanted.AsSpan(1), token);

        foreach (var handle in metadata.MethodDefinitions)
        {
            var definition = metadata.GetMethodDefinition(handle);

            if (definition.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes();

            if (il is null)
            {
                continue;
            }

            foreach (var opcode in new byte[] { 0x28, 0x6F })
            {
                wanted[0] = opcode;

                if (il.AsSpan().IndexOf(wanted) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
