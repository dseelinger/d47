namespace D47.EliteVad;

/// <summary>
/// Speech starts after <see cref="StartChunks"/> consecutive chunks at or over the threshold,
/// and stops after <see cref="StopChunks"/> consecutive chunks under it.
/// </summary>
internal sealed class OnsetRule(int startChunks, int stopChunks)
{
    private int _above;
    private int _below;
    private long _firstAbove;

    public int StartChunks { get; } = startChunks;

    public int StopChunks { get; } = stopChunks;

    public bool Speaking { get; private set; }

    /// <summary>Chunk index of the first over-threshold chunk of the onset just reported.</summary>
    public long OnsetFirstChunk { get; private set; }

    /// <summary>Chunk index of the first under-threshold chunk of the offset just reported.</summary>
    public long OffsetFirstChunk { get; private set; }

    /// <summary>Feeds one chunk; returns +1 on an onset, -1 on an offset, otherwise 0.</summary>
    public int Feed(long chunkIndex, bool over)
    {
        if (over)
        {
            if (_above == 0)
            {
                _firstAbove = chunkIndex;
            }

            _above++;
            _below = 0;
            if (!Speaking && _above >= StartChunks)
            {
                Speaking = true;
                OnsetFirstChunk = _firstAbove;
                return 1;
            }

            return 0;
        }

        _above = 0;
        _below++;
        if (Speaking && _below >= StopChunks)
        {
            Speaking = false;
            OffsetFirstChunk = chunkIndex - StopChunks + 1;
            return -1;
        }

        return 0;
    }
}
