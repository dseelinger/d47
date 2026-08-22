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
    /// And something reads it. A registration nobody follows up on is a set that is never
    /// activated — the panel would be pointable and never grabbable.
    /// </summary>
    [Fact]
    public void SomethingInTheAppActuallyReadsTheTrigger()
    {
        Assert.True(
            IsCalledInside(typeof(VrHost).Assembly, nameof(VrActionInput.TriggerHeld)),
            $"nothing in {typeof(VrHost).Assembly.GetName().Name} calls {nameof(VrActionInput.TriggerHeld)}");
    }

    /// <summary>
    /// And something gives them back. A claim at overlay priority stands until the next
    /// <c>UpdateActionState</c> from this process, so a host that reads the trigger and never
    /// calls <see cref="VrActionInput.Release"/> is a host that takes the Commander's controllers
    /// from the game the first time a ray crosses the panel and keeps them — which is what
    /// "Motion Controller appears hung" was (2026-08-21).
    /// </summary>
    [Fact]
    public void SomethingInTheAppGivesTheControllersBack()
    {
        Assert.True(
            IsCalledInside(typeof(VrHost).Assembly, nameof(VrActionInput.Release)),
            $"nothing in {typeof(VrHost).Assembly.GetName().Name} calls {nameof(VrActionInput.Release)}");

        Assert.True(
            IsCalledInside(typeof(VrActionInput).Assembly, nameof(VrActionInput.Release)),
            $"nothing in {typeof(VrActionInput).Assembly.GetName().Name} calls {nameof(VrActionInput.Release)}");
    }

    /// <summary>
    /// And what is given back is the set at priority zero, not nothing. SteamVR refuses an empty
    /// list outright — <c>NoActiveActionSet</c> — and leaves the last list standing, which is
    /// how 0.48.6 shipped a hand-back that never handed anything back: the installed log of
    /// 2026-08-22 says so six times in thirty seconds. Priority zero is below the overlay range,
    /// where a set takes inputs from no other application.
    /// </summary>
    [Fact]
    public void TheReleaseIsTheSetAtPriorityZeroNotAnEmptyList()
    {
        var claim = VrActionInput.ClaimSet(42);
        var release = VrActionInput.ReleaseSet(42);

        Assert.Equal(42ul, claim.ulActionSet);
        Assert.Equal(42ul, release.ulActionSet);
        Assert.True(claim.nPriority >= Valve.VR.OpenVR.k_nActionSetOverlayGlobalPriorityMin);
        Assert.Equal(0, release.nPriority);

        // And it is what Release hands over. A shape nobody passes is the empty list again.
        Assert.True(
            CallsFrom(typeof(VrActionInput).Assembly, nameof(VrActionInput), nameof(VrActionInput.Release), nameof(VrActionInput.ReleaseSet)),
            $"{nameof(VrActionInput)}.{nameof(VrActionInput.Release)} does not call {nameof(VrActionInput.ReleaseSet)}");
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

        foreach (var handle in metadata.MethodDefinitions)
        {
            if (BodyCalls(pe, metadata.GetMethodDefinition(handle), token))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether one named method's body issues a call to another method defined in the same
    /// assembly. The narrower question than <see cref="IsCalledInside"/>: not "does anything
    /// call it" but "does <em>this</em> method", which is what ties a release to its shape.
    /// </summary>
    private static bool CallsFrom(Assembly assembly, string type, string caller, string callee)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);

        var metadata = pe.GetMetadataReader();

        var target = metadata.MethodDefinitions.FirstOrDefault(
            handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == callee);

        Assert.False(target.IsNil, $"{callee} is not defined in {assembly.GetName().Name}");

        var token = MetadataTokens.GetToken(target);

        foreach (var handle in metadata.MethodDefinitions)
        {
            var definition = metadata.GetMethodDefinition(handle);

            if (metadata.GetString(definition.Name) != caller
                || metadata.GetString(metadata.GetTypeDefinition(definition.GetDeclaringType()).Name) != type)
            {
                continue;
            }

            if (BodyCalls(pe, definition, token))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a method body carries the five bytes of a <c>call</c> or <c>callvirt</c> with
    /// the token. Not decoded instruction by instruction: the question is only ever asked one
    /// way round, and an operand that happens to read as this call is a false positive nothing
    /// here can produce, while a real call cannot hide from it.
    /// </summary>
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
