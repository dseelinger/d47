namespace D47.Core.Audio;

/// <summary>Standard-format PCM appended as a provider sends it; complete once the provider has finished.</summary>
/// <remarks>
/// One thread appends at a time; any number may read, and a read never waits on an append. Appends after
/// the clip has ended are ignored.
/// </remarks>
public sealed class ArrivingClip
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource<AudioClip> _whole = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Grown by copying, so a reader holding an older array still finds every byte below the length it read.
    private byte[] _pcm;
    private long _length;
    private byte? _carry;
    private volatile bool _complete;
    private long _firstAppendedAt;

    /// <summary>Completed, and replaced, whenever bytes arrive or the clip ends.</summary>
    private TaskCompletionSource _grew = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ArrivingClip(string name)
    {
        Name = name;
        _pcm = new byte[AudioFormat.Standard.SampleRate * AudioFormat.Standard.BytesPerFrame];
    }

    private ArrivingClip(AudioClip clip)
    {
        Name = clip.Name;
        _pcm = clip.Pcm.ToArray();
        _length = _pcm.Length & ~1;
        _complete = true;
        _whole.SetResult(clip);
    }

    /// <summary>A clip that has already arrived in full.</summary>
    public static ArrivingClip Of(AudioClip clip) =>
        clip.Format == AudioFormat.Standard
            ? new ArrivingClip(clip)
            : throw new ArgumentException("An arriving clip is in the standard format.", nameof(clip));

    public string Name { get; }

    public AudioFormat Format => AudioFormat.Standard;

    /// <summary>Bytes readable now; always a whole number of samples.</summary>
    public long Length => Volatile.Read(ref _length);

    /// <summary>Whether everything that will arrive has; once true, <see cref="Length"/> is final.</summary>
    public bool IsComplete => _complete;

    /// <summary>
    /// The <see cref="System.Diagnostics.Stopwatch"/> timestamp of the first append, or null for a clip that
    /// arrived whole or has had nothing appended.
    /// </summary>
    public long? FirstAppendedAt => Volatile.Read(ref _firstAppendedAt) is var at and not 0 ? at : null;

    /// <summary>The finished clip; faults or cancels with it.</summary>
    public Task<AudioClip> Whole => _whole.Task;

    /// <summary>Adds PCM; an odd trailing byte is held until the next append completes its sample.</summary>
    public void Append(ReadOnlySpan<byte> pcm)
    {
        lock (_gate)
        {
            if (_complete || pcm.IsEmpty)
            {
                return;
            }

            if (_firstAppendedAt == 0)
            {
                Volatile.Write(ref _firstAppendedAt, System.Diagnostics.Stopwatch.GetTimestamp());
            }

            var length = _length;
            var held = _carry.HasValue ? 1 : 0;
            var whole = (held + pcm.Length) & ~1;
            var buffer = Reserve(length + whole);

            if (whole > 0)
            {
                var into = buffer.AsSpan((int)length, whole);

                if (_carry is { } carry)
                {
                    into[0] = carry;
                    pcm[..(whole - 1)].CopyTo(into[1..]);
                }
                else
                {
                    pcm[..whole].CopyTo(into);
                }
            }

            _carry = (held + pcm.Length) % 2 == 1 ? pcm[^1] : null;

            Volatile.Write(ref _length, length + whole);

            if (whole > 0)
            {
                Grew();
            }
        }
    }

    /// <summary>Marks the clip as fully arrived.</summary>
    public void Complete()
    {
        AudioClip clip;

        lock (_gate)
        {
            if (_complete)
            {
                return;
            }

            clip = new AudioClip(Name, _pcm.AsSpan(0, (int)_length).ToArray(), Format);
            _complete = true;
            Grew();
        }

        _whole.TrySetResult(clip);
    }

    /// <summary>
    /// Ends the clip where it got to. <see cref="Whole"/> faults with <paramref name="error"/>, or cancels
    /// when it is an <see cref="OperationCanceledException"/>.
    /// </summary>
    public void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_complete)
            {
                return;
            }

            _complete = true;
            Grew();
        }

        if (error is OperationCanceledException cancelled)
        {
            _whole.TrySetCanceled(cancelled.CancellationToken);
        }
        else
        {
            _whole.TrySetException(error);
        }
    }

    /// <summary>Copies what has arrived from <paramref name="position"/> on, without waiting for more.</summary>
    public int Read(long position, Span<byte> buffer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        // Length before the array: an array read afterwards holds at least that many bytes.
        var length = Volatile.Read(ref _length);
        var pcm = Volatile.Read(ref _pcm);

        if (position >= length)
        {
            return 0;
        }

        var count = (int)Math.Min(buffer.Length, length - position);
        pcm.AsSpan((int)position, count).CopyTo(buffer);
        return count;
    }

    /// <summary>
    /// A new clip carrying this one's PCM through <paramref name="filter"/> as it arrives. It ends when this one
    /// does: complete with the filter's tail appended, or failed or cancelled the same way.
    /// </summary>
    public ArrivingClip Through(IPcmFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var treated = new ArrivingClip(Name);
        _ = PumpAsync(filter, treated);
        return treated;
    }

    private async Task PumpAsync(IPcmFilter filter, ArrivingClip treated)
    {
        var buffer = new byte[16 * 1024];
        var position = 0L;

        try
        {
            while (true)
            {
                await ArrivedPast(position).ConfigureAwait(false);

                // Complete before reading: once complete, what is read after it is everything.
                var complete = IsComplete;
                int read;

                while ((read = Read(position, buffer)) > 0)
                {
                    position += read;
                    treated.Append(filter.Push(buffer.AsSpan(0, read)));
                }

                if (complete)
                {
                    break;
                }
            }

            await Whole.ConfigureAwait(false);

            treated.Append(filter.Finish());
            treated.Complete();
        }
        catch (Exception ex)
        {
            treated.Fail(ex);
        }
    }

    /// <summary>Completes once more than <paramref name="position"/> bytes have arrived or the clip has ended.</summary>
    private Task ArrivedPast(long position)
    {
        lock (_gate)
        {
            return _length > position || _complete ? Task.CompletedTask : _grew.Task;
        }
    }

    /// <summary>Wakes whatever is waiting in <see cref="ArrivedPast"/>. Called holding <see cref="_gate"/>.</summary>
    private void Grew()
    {
        var grew = _grew;
        _grew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        grew.TrySetResult();
    }

    private byte[] Reserve(long needed)
    {
        if (needed <= _pcm.Length)
        {
            return _pcm;
        }

        var grown = new byte[Math.Max(needed, (long)_pcm.Length * 2)];
        _pcm.AsSpan(0, (int)_length).CopyTo(grown);
        Volatile.Write(ref _pcm, grown);
        return grown;
    }
}
