namespace D47.Core.Tests;

/// <summary>Polls for a result that code under test produces on another thread.</summary>
public static class Wait
{
    public static async Task Until(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 400; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        throw new TimeoutException(what);
    }
}
