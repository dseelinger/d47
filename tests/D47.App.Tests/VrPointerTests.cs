using D47.App.Headset;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>Grab-to-move needs the trigger, and the trigger arrives through <c>IVRInput</c>.</summary>
public class VrPointerTests
{
    /// <summary>
    /// The panel is grab-to-move and asks for the pointer; captions are read rather than touched, and
    /// an interactive quad in front of the cockpit is a laser that stops on a label.
    /// </summary>
    [Fact]
    public void ThePanelAsksForThePointerAndTheCaptionsDoNot()
    {
        Assert.True(typeof(VrPanelSurface).IsAssignableTo(typeof(IVrSurfaceSource)));

        Assert.True(Declared<VrPanelSurface>());
        Assert.False(Declared<VrCaptionSurface>());
    }

    [Fact]
    public void SomethingInTheRuntimeActuallyRegistersForTheTrigger()
    {
        Assert.True(
            AssemblyCalls.Anything(typeof(VrActionInput).Assembly, nameof(VrActionInput.Register)),
            $"nothing in {typeof(VrActionInput).Assembly.GetName().Name} calls {nameof(VrActionInput.Register)}");
    }

    [Fact]
    public void SomethingInTheAppActuallyReadsTheTrigger()
    {
        Assert.True(
            AssemblyCalls.Anything(typeof(VrHost).Assembly, nameof(VrActionInput.TriggerHeld)),
            $"nothing in {typeof(VrHost).Assembly.GetName().Name} calls {nameof(VrActionInput.TriggerHeld)}");
    }

    [Fact]
    public void SomethingInTheAppGivesTheControllersBack()
    {
        Assert.True(
            AssemblyCalls.Anything(typeof(VrHost).Assembly, nameof(VrActionInput.Release)),
            $"nothing in {typeof(VrHost).Assembly.GetName().Name} calls {nameof(VrActionInput.Release)}");

        Assert.True(
            AssemblyCalls.Anything(typeof(VrActionInput).Assembly, nameof(VrActionInput.Release)),
            $"nothing in {typeof(VrActionInput).Assembly.GetName().Name} calls {nameof(VrActionInput.Release)}");
    }

    /// <summary>And what is given back is the set at priority zero, not nothing.</summary>
    [Fact]
    public void TheReleaseIsTheSetAtPriorityZeroNotAnEmptyList()
    {
        var claim = VrActionInput.ClaimSet(42);
        var release = VrActionInput.ReleaseSet(42);

        Assert.Equal(42ul, claim.ulActionSet);
        Assert.Equal(42ul, release.ulActionSet);
        Assert.True(claim.nPriority >= Valve.VR.OpenVR.k_nActionSetOverlayGlobalPriorityMin);
        Assert.Equal(0, release.nPriority);

        // And it is what Release hands over.
        Assert.True(
            AssemblyCalls.Calls(typeof(VrActionInput).Assembly, nameof(VrActionInput), nameof(VrActionInput.Release), nameof(VrActionInput.ReleaseSet)),
            $"{nameof(VrActionInput)}.{nameof(VrActionInput.Release)} does not call {nameof(VrActionInput.ReleaseSet)}");
    }

    /// <summary>What a surface source says about the pointer, read off the type's own default.</summary>
    private static bool Declared<T>() =>
        (bool)typeof(T).GetProperty(nameof(IVrSurfaceSource.TakesPointer))!
            .GetGetMethod()!
            .Invoke(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T)), null)!;
}
