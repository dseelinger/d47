namespace D47.App.Panel;

/// <summary>A page that acts on the panel's search when Enter is pressed in it, instead of stepping through matches.</summary>
public interface ISubmitSearchPage
{
    /// <summary>Acts on <paramref name="query"/>; false when the page does nothing with it.</summary>
    bool Submit(string query);
}
