using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>Remembers how the Stories page was left filtered.</summary>
public sealed class StoryFilterMemory(ViewStateStore store)
{
    private StoryFilter? _filter;

    /// <summary>The filter the page was left under; the default filter if none was.</summary>
    public StoryFilter Filter => _filter ??= store.Load().StoryFilter ?? new StoryFilter();

    /// <summary>Records the filter the page was left under.</summary>
    public void Remember(StoryFilter filter)
    {
        if (Filter == filter)
        {
            return;
        }

        _filter = filter;

        store.Save(store.Load() with { StoryFilter = filter.IsDefault ? null : filter });
    }
}
