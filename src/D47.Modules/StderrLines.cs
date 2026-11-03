using System.Text;

namespace D47.Modules;

/// <summary>Splits a module's stderr into lines, cutting each at <see cref="MaxLine"/> characters and dropping the rest of it.</summary>
internal static class StderrLines
{
    public const int MaxLine = 500;

    /// <summary>Reads until the stream ends, handing each line to <paramref name="line"/> without its line break.</summary>
    public static async Task ReadAsync(TextReader reader, Action<string> line, CancellationToken cancellation)
    {
        var buffer = new char[1024];
        var current = new StringBuilder();
        var cut = false;

        void Flush()
        {
            if (current.Length > 0 || cut)
            {
                line(cut ? current.Append('…').ToString() : current.ToString());
            }

            current.Clear();
            cut = false;
        }

        int read;

        while ((read = await reader.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];

                if (c == '\n')
                {
                    Flush();
                }
                else if (c != '\r')
                {
                    if (current.Length < MaxLine)
                    {
                        current.Append(c);
                    }
                    else
                    {
                        cut = true;
                    }
                }
            }
        }

        Flush();
    }
}
