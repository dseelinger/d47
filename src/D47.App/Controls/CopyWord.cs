using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace D47.App.Controls;

/// <summary>
/// The one button that puts a system name on the clipboard, through the seam every draw site shares
/// instead of reaching for a mechanism of its own (#157).
/// </summary>
public static class CopyWord
{
    public const string Word = "COPY";
    public const string Copied = "COPIED";
    public const string Failed = "COPY FAILED";

    private static readonly TimeSpan Shown = TimeSpan.FromSeconds(2);

    /// <summary>A quiet <c>COPY</c> that copies <paramref name="value"/> through <paramref name="copy"/>.</summary>
    public static Button For(string value, Func<string, Task<bool>> copy)
    {
        var button = Glyphs.Quiet(
            new Button { VerticalAlignment = VerticalAlignment.Center }, Word, $"Copy {value}");

        button.Click += async (_, _) =>
        {
            bool worked;

            try
            {
                worked = await copy(value);
            }
            catch (Exception)
            {
                worked = false;
            }

            Show(button, worked);
        };

        return button;
    }

    /// <summary>Says a copy's result on <paramref name="button"/>, and goes back to <c>COPY</c> after two seconds.</summary>
    public static void Show(Button button, bool worked)
    {
        button.Content = worked ? Copied : Failed;

        var reset = new DispatcherTimer { Interval = Shown };

        reset.Tick += (_, _) =>
        {
            reset.Stop();
            button.Content = Word;
        };

        reset.Start();
    }
}
