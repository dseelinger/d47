using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class EachTouchInputReachesItsHandsRecordTests : IDisposable
{
    private const string Left = "/user/hand/left";
    private const string Right = "/user/hand/right";

    private readonly LayerRig rig = new();

    [Theory]
    [InlineData("/user/hand/right/input/a/click", "primary")]
    [InlineData("/user/hand/left/input/x/click", "primary")]
    [InlineData("/user/hand/right/input/b/click", "secondary")]
    [InlineData("/user/hand/left/input/y/click", "secondary")]
    [InlineData("/user/hand/left/input/menu/click", "menu")]
    [InlineData("/user/hand/left/input/thumbstick/click", "stick_click")]
    [InlineData("/user/hand/right/input/thumbstick/click", "stick_click")]
    [InlineData("/user/hand/left/input/thumbstick", "stick")]
    [InlineData("/user/hand/right/input/thumbstick", "stick")]
    public void EachTouchInputIsSuggestedForItsAction(string binding, string action)
    {
        Attach();

        var suggestion = Assert.Single(rig.Next.Suggestions);
        var bound = Assert.Single(suggestion.Bindings, b => rig.Next.PathName(b.Binding) == binding);
        Assert.Equal(rig.Next.Actions[action].Handle, bound.Action);
    }

    [Theory]
    [InlineData("primary", Right, 1, nameof(XrButtons.Primary))]
    [InlineData("primary", Left, 0, nameof(XrButtons.Primary))]
    [InlineData("secondary", Right, 1, nameof(XrButtons.Secondary))]
    [InlineData("secondary", Left, 0, nameof(XrButtons.Secondary))]
    [InlineData("menu", Left, 0, nameof(XrButtons.Menu))]
    [InlineData("stick_click", Left, 0, nameof(XrButtons.StickClick))]
    [InlineData("stick_click", Right, 1, nameof(XrButtons.StickClick))]
    public void AHeldButtonSetsItsBitOnItsHandOnly(string action, string hand, int index, string bitName)
    {
        Attach();
        var bit = Enum.Parse<XrButtons>(bitName);
        rig.Next.Held.Add((action, hand));

        Frame();

        var hands = rig.Published.Hands;
        Assert.Equal(bit, hands[index].Buttons);
        Assert.Equal(XrButtons.None, hands[1 - index].Buttons);
    }

    [Fact]
    public void EachStickReachesItsOwnHandsAxes()
    {
        Attach();
        rig.Next.Sticks[("stick", Left)] = (-0.5f, 0.25f);
        rig.Next.Sticks[("stick", Right)] = (0.75f, -1f);

        Frame();

        var hands = rig.Published.Hands;
        Assert.Equal((-0.5f, 0.25f), (hands[0].StickX, hands[0].StickY));
        Assert.Equal((0.75f, -1f), (hands[1].StickX, hands[1].StickY));
    }

    [Fact]
    public void AnInactiveActionReadsAsNotHeldAndAtRest()
    {
        Attach();
        rig.Next.Held.Add(("primary", Right));
        rig.Next.Sticks[("stick", Right)] = (1f, 1f);
        rig.Next.Inactive.Add(("primary", Right));
        rig.Next.Inactive.Add(("stick", Right));

        Frame();

        var right = rig.Published.Hands[1];
        Assert.Equal(1u, right.Valid);
        Assert.Equal(XrButtons.None, right.Buttons);
        Assert.Equal((0f, 0f), (right.StickX, right.StickY));
    }

    [Fact]
    public void AStickPastItsRangeOrNotANumberIsKeptInRange()
    {
        Attach();
        rig.Next.Sticks[("stick", Left)] = (3f, -2f);
        rig.Next.Sticks[("stick", Right)] = (float.NaN, 0.5f);

        Frame();

        var hands = rig.Published.Hands;
        Assert.Equal((1f, -1f), (hands[0].StickX, hands[0].StickY));
        Assert.Equal((0f, 0f), (hands[1].StickX, hands[1].StickY));
    }

    public void Dispose() => rig.Dispose();

    private void Attach()
    {
        rig.Point(true);
        rig.EliteAttaches(ControllerCalls.ElitesSet);
    }

    private void Frame()
    {
        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(0);
    }
}
