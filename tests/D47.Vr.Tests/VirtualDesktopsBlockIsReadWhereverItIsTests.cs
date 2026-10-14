using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using D47.Core.Vr;
using Xunit;

namespace D47.Vr.Tests;

[Trait("Category", "Integration")]
public class VirtualDesktopsBlockIsReadWhereverItIsTests
{
    [Fact]
    public void AMissingBlockReadsUnknownAndThrowsNothing() =>
        Assert.Equal(HeadsetView.Unknown, new VirtualDesktopView(UniqueName()).Read());

    [Fact]
    public void AnOpenBlockIsReadThroughSharedMemory()
    {
        var name = UniqueName();
        using var map = MemoryMappedFile.CreateNew(name, 4096);
        using (var view = map.CreateViewAccessor())
        {
            view.Write(0xF8, Stopwatch.GetTimestamp());
            view.Write(0x204, 1u);
        }

        Assert.Equal(HeadsetView.Vr, new VirtualDesktopView(name).Read());
    }

    private static string UniqueName() => $"D47.Tests.{Guid.NewGuid():N}";
}
