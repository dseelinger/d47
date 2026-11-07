namespace D47.Core.Capabilities.Builtin;

/// <summary>A clipboard that records instead of writing.</summary>
public sealed class RecordingClipboard : IClipboard
{
    private readonly List<string> _written = [];

    public bool Works { get; set; } = true;

    public IReadOnlyList<string> Written => _written;

    public string? Last => _written.Count == 0 ? null : _written[^1];

    public Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Works)
        {
            _written.Add(text);
        }

        return Task.FromResult(Works);
    }
}
