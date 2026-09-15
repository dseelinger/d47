using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>Every <see cref="VrHandle"/> maps to the cursor that shows its drag direction (#191).</summary>
public class TheCursorFollowsTheHandleTests
{
    [Theory]
    [InlineData(VrHandle.None, VrCursor.Ring)]
    [InlineData(VrHandle.Left, VrCursor.Horizontal)]
    [InlineData(VrHandle.Right, VrCursor.Horizontal)]
    [InlineData(VrHandle.Top, VrCursor.Vertical)]
    [InlineData(VrHandle.Bottom, VrCursor.Vertical)]
    [InlineData(VrHandle.Top | VrHandle.Left, VrCursor.DiagonalDown)]
    [InlineData(VrHandle.Bottom | VrHandle.Right, VrCursor.DiagonalDown)]
    [InlineData(VrHandle.Top | VrHandle.Right, VrCursor.DiagonalUp)]
    [InlineData(VrHandle.Bottom | VrHandle.Left, VrCursor.DiagonalUp)]
    public void AHandleMapsToItsCursor(VrHandle handle, VrCursor expected) =>
        Assert.Equal(expected, VrCursors.For(handle));
}
