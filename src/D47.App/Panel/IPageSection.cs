namespace D47.App.Panel;

/// <summary>A page with a section switch: reports the open section and opens one by its id.</summary>
public interface IPageSection
{
    /// <summary>The open section's word without any count, or empty for none.</summary>
    string OpenSection { get; }

    /// <summary>The open section's stable id, or empty for none.</summary>
    string OpenSectionId { get; }

    /// <summary>Whether the last change raised was a section the Commander or the page chose, rather than the page reporting where it is.</summary>
    bool SectionChosen { get; }

    /// <summary>
    /// Opens the section with this id without counting it as a choice. A page whose data is not ready opens it once
    /// the data is. False for an id the page has no section by.
    /// </summary>
    bool OpenSectionById(string id);

    /// <summary>Raised when <see cref="OpenSection"/> or <see cref="OpenSectionId"/> changes.</summary>
    event EventHandler? OpenSectionChanged;
}

/// <summary>Holds a page's open section and raises a change only when it differs.</summary>
public sealed class PageSectionWord
{
    private bool _choosing;

    public string Id { get; private set; } = string.Empty;

    public string Word { get; private set; } = string.Empty;

    /// <summary>Whether the change last raised was chosen.</summary>
    public bool Chosen { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Marks the next section the page reports as chosen; it holds while the page reports none.</summary>
    public void Choose() => _choosing = true;

    /// <summary>Drops a choice not yet reported.</summary>
    public void Unchoose() => _choosing = false;

    public void Set(object owner, string id, string word)
    {
        var chosen = _choosing && id.Length > 0;

        if (id.Length > 0)
        {
            _choosing = false;
        }

        if (id == Id && word == Word)
        {
            return;
        }

        Id = id;
        Word = word;
        Chosen = chosen;
        Changed?.Invoke(owner, EventArgs.Empty);
    }
}
