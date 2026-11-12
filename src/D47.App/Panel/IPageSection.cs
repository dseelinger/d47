namespace D47.App.Panel;

/// <summary>A page with a section switch that reports the open section for the mini panel's trail.</summary>
public interface IPageSection
{
    /// <summary>The open section's word without any count, or empty for none.</summary>
    string OpenSection { get; }

    /// <summary>Raised when <see cref="OpenSection"/> changes.</summary>
    event EventHandler? OpenSectionChanged;
}

/// <summary>Holds a page's open section word and raises a change only when the word differs.</summary>
public sealed class PageSectionWord
{
    public string Word { get; private set; } = string.Empty;

    public event EventHandler? Changed;

    public void Set(object owner, string word)
    {
        if (word == Word)
        {
            return;
        }

        Word = word;
        Changed?.Invoke(owner, EventArgs.Empty);
    }
}
