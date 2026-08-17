using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using D47.App.Headset;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Grab-to-move needs SteamVR to point a laser at the quad and send back what the hand does
/// with it, and that is two flags on the overlay (list.md Phase 9).
/// <para>
/// The flags themselves cannot be asserted from here — they are an OpenVR call on a handle that
/// only a running session has. What can be asserted is the half that was actually wrong:
/// <see cref="VrOverlay.TakePointer"/> was written, documented as load-bearing, and called by
/// nothing at all, so no overlay was ever interactive and the panel could not be picked up
/// (bugs.md 4). A method nobody calls is invisible to every test that reasons about behaviour,
/// which is why one of these reasons about the assembly instead.
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
    /// And something calls it. This is the shape of the defect rather than a proof the grab
    /// works — that needs a headset — but it is the one property no behavioural test could
    /// have: an uncalled method has no behaviour to be wrong about.
    /// </summary>
    [Fact]
    public void SomethingInTheRuntimeActuallyTakesThePointer()
    {
        Assert.True(
            IsCalledInside(typeof(VrOverlay).Assembly, nameof(VrOverlay.TakePointer)),
            $"nothing in {typeof(VrOverlay).Assembly.GetName().Name} calls {nameof(VrOverlay.TakePointer)}");
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

        var target = metadata.MethodDefinitions.FirstOrDefault(
            handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == method);

        Assert.False(target.IsNil, $"{method} is not in {assembly.GetName().Name}");

        var token = MetadataTokens.GetToken(target);

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
