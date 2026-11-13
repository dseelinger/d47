namespace D47.App.Panel;

/// <summary>How the Galaxy map's rail divides its kinds into pages: eight row slots, the last a pager past eight kinds.</summary>
public static class GalaxyMapRailPages
{
    public const int Slots = 8;
    public const int PageSize = Slots - 1;

    public static bool Pages(int count) => count > Slots;

    public static int PageCount(int count) => Pages(count) ? (count + PageSize - 1) / PageSize : 1;

    /// <summary>The page holding the row at <paramref name="index"/>.</summary>
    public static int PageOf(int count, int index) => Pages(count) ? Math.Clamp(index, 0, count - 1) / PageSize : 0;

    /// <summary>The first row and the number of rows on <paramref name="page"/>, which is clamped to the pages there are.</summary>
    public static (int First, int Count) Rows(int count, int page)
    {
        if (!Pages(count))
        {
            return (0, count);
        }

        var first = Math.Clamp(page, 0, PageCount(count) - 1) * PageSize;

        return (first, Math.Min(PageSize, count - first));
    }

    /// <summary>The pager's two lines: the rows shown and the total.</summary>
    public static (string Range, string Of) Readout(int count, int page)
    {
        var (first, shown) = Rows(count, page);

        return ($"{first + 1}\u2013{first + shown}", $"of {count}");
    }
}
