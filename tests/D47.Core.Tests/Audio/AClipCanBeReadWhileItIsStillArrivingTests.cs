using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AClipCanBeReadWhileItIsStillArrivingTests
{
    private static byte[] Ramp(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i * 7))];

    [Fact]
    public void AReaderOnAnotherThreadGetsEveryByteInOrderThenNothingOnceItIsComplete()
    {
        var expected = Ramp(200_001);
        var clip = new ArrivingClip("arriving");

        var writer = new Thread(() =>
        {
            var sizes = new[] { 1, 3, 997, 2, 4_099, 5, 12_345 };
            var at = 0;

            for (var i = 0; at < expected.Length; i++)
            {
                var size = Math.Min(sizes[i % sizes.Length], expected.Length - at);
                clip.Append(expected.AsSpan(at, size));
                at += size;
            }

            clip.Complete();
        });

        var received = new List<byte>();
        var buffer = new byte[1_013];
        writer.Start();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (true)
        {
            var complete = clip.IsComplete;
            var read = clip.Read(received.Count, buffer);
            received.AddRange(buffer.AsSpan(0, read));

            if (read == 0 && complete)
            {
                break;
            }

            Assert.True(DateTime.UtcNow < deadline, "the reader never saw the clip complete");
        }

        writer.Join();

        // The odd final byte never completed a sample.
        Assert.Equal(expected[..200_000], received);
        Assert.Equal(0, clip.Read(received.Count, buffer));
    }

    [Fact]
    public void AnOddAppendHoldsItsLastByteUntilTheNextAppend()
    {
        var clip = new ArrivingClip("odd");

        clip.Append([1, 2, 3]);

        Assert.Equal(2, clip.Length);

        clip.Append([4]);

        Assert.Equal(4, clip.Length);

        var buffer = new byte[4];
        Assert.Equal(4, clip.Read(0, buffer));
        Assert.Equal([1, 2, 3, 4], buffer);
    }

    [Fact]
    public async Task TheWholeClipIsEverythingAppended()
    {
        var pcm = Ramp(10_000);
        var clip = new ArrivingClip("whole");

        clip.Append(pcm.AsSpan(0, 3_333));
        clip.Append(pcm.AsSpan(3_333));
        clip.Complete();

        var whole = await clip.Whole;

        Assert.Equal("whole", whole.Name);
        Assert.Equal(AudioFormat.Standard, whole.Format);
        Assert.Equal(pcm, whole.Pcm.ToArray());
    }

    [Fact]
    public async Task AFailedClipFaultsTheWholeAndEndsWhereItGotTo()
    {
        var clip = new ArrivingClip("failed");
        clip.Append([1, 2, 3, 4]);

        clip.Fail(new InvalidOperationException("the provider hung up"));
        clip.Append([5, 6]);

        Assert.True(clip.IsComplete);
        Assert.Equal(4, clip.Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() => clip.Whole);
    }

    [Fact]
    public async Task ACancelledClipCancelsTheWhole()
    {
        var clip = new ArrivingClip("cancelled");

        clip.Fail(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clip.Whole);
        Assert.True(clip.Whole.IsCanceled);
    }

    [Fact]
    public async Task AClipThatHasAlreadyArrivedIsCompleteFromTheStart()
    {
        var source = new AudioClip("ready", Ramp(480), AudioFormat.Standard);

        var clip = ArrivingClip.Of(source);

        Assert.True(clip.IsComplete);
        Assert.Equal(480, clip.Length);
        Assert.Same(source, await clip.Whole);
    }

    [Fact]
    public void AClipThatHasAlreadyArrivedReadsOnlyWholeSamples()
    {
        var clip = ArrivingClip.Of(new AudioClip("odd", Ramp(481), AudioFormat.Standard));

        Assert.Equal(480, clip.Length);
    }
}
