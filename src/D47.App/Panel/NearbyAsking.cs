namespace D47.App.Panel;

/// <summary>When the galaxy map asks for the systems around the Commander: once per opening and per arrival, while Unvisited is wanted.</summary>
internal sealed class NearbyAsking
{
    private bool _opened;
    private string? _askedAround;

    public void Opened()
    {
        _opened = true;
        _askedAround = null;
    }

    public void Closed() => _opened = false;

    /// <summary>Lets the next <see cref="Ask"/> ask again in the same system.</summary>
    public void Forget() => _askedAround = null;

    /// <summary>True when the systems around <paramref name="here"/> are to be asked for now; a call that returns false for a system still counts as its ask.</summary>
    public bool Ask(string? here, bool galaxy, bool wanted)
    {
        if (!_opened || string.Equals(here, _askedAround, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _askedAround = here;

        return here is not null && galaxy && wanted;
    }
}
