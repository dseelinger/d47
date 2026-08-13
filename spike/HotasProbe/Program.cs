using System.Diagnostics;
using Windows.Gaming.Input;

namespace HotasProbe;

/// <summary>
/// Throwaway. Answers three questions about reading HOTAS switch positions, per spike/README.md:
/// whether Windows.Gaming.Input works from a plain desktop process at all, whether a WinWing
/// throttle base and handle enumerate as one device or two, and what identity survives the
/// 4x32 mode switch.
///
/// Run bare to watch transitions. Run with --ids to dump identity and exit, which is the form
/// to run either side of a 4x32 mode change so the two can be diffed.
/// </summary>
internal static class Program
{
    private const int PollHz = 125;
    private const int EnumerateWaitMs = 6000;
    private const int SettleMs = 1500;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<Device> Devices = [];
    private static volatile bool s_stop;

    private static int Main(string[] args)
    {
        Console.WriteLine("=== d47 HOTAS probe — Windows.Gaming.Input.RawGameController ===");
        Console.WriteLine();

        var controllers = Enumerate();

        if (controllers.Count == 0)
        {
            Console.WriteLine($"No RawGameControllers appeared within {EnumerateWaitMs} ms.");
            Console.WriteLine();
            Console.WriteLine("=> WGI is NOT a usable read path from a plain console process here.");
            Console.WriteLine("   Next step is the Raw Input (WM_INPUT) fallback with a message-only window.");
            Console.WriteLine("   (If the throttle is unplugged or held by another process, rule that out first.)");
            return 1;
        }

        Console.WriteLine($"[enumeration] {controllers.Count} controller(s)");
        Console.WriteLine();

        for (var i = 0; i < controllers.Count; i++)
        {
            Describe(i, controllers[i]);
        }

        if (args.Contains("--ids", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("--ids given; not watching. Run again after the 4x32 change and diff the above.");
            return 0;
        }

        Watch(controllers);
        Summarise();
        return 0;
    }

    /// <summary>
    /// Waits for the device list to stop growing rather than for it to become non-empty.
    /// <para>
    /// The first version of this stopped at the first non-empty read and reported three
    /// controllers on a machine that has six. WGI populates the list over time, so "is it
    /// non-empty yet" is the wrong question and any implementation asking it will silently
    /// miss devices. The timeline below is printed because the shape of the growth is the
    /// finding.
    /// </para>
    /// </summary>
    private static IReadOnlyList<RawGameController> Enumerate()
    {
        var seen = new ManualResetEventSlim(false);
        RawGameController.RawGameControllerAdded += (_, _) => seen.Set();

        var timeline = new List<string>();
        var deadline = Clock.ElapsedMilliseconds + EnumerateWaitMs;
        var count = -1;
        var lastChange = Clock.ElapsedMilliseconds;

        while (Clock.ElapsedMilliseconds < deadline)
        {
            var now = RawGameController.RawGameControllers.Count;

            if (now != count)
            {
                timeline.Add($"{Clock.ElapsedMilliseconds}ms:{now}");
                count = now;
                lastChange = Clock.ElapsedMilliseconds;
            }
            else if (count > 0 && Clock.ElapsedMilliseconds - lastChange > SettleMs)
            {
                break;
            }

            seen.Wait(25);
            seen.Reset();
        }

        Console.WriteLine($"[enumeration timeline] {string.Join("  ", timeline)}");
        Console.WriteLine(
            "  a single read at startup is not a device list — it is a sample of one that is still filling.");
        Console.WriteLine();

        return RawGameController.RawGameControllers;
    }

    private static void Describe(int index, RawGameController controller)
    {
        Console.WriteLine($"  #{index}");
        Console.WriteLine($"    DisplayName   : {Safe(() => controller.DisplayName)}");
        Console.WriteLine($"    Vendor/Product: VID {Safe(() => $"0x{controller.HardwareVendorId:X4}")}  "
                          + $"PID {Safe(() => $"0x{controller.HardwareProductId:X4}")}");
        Console.WriteLine($"    NonRoamableId : {Safe(() => controller.NonRoamableId)}");
        Console.WriteLine($"    Buttons       : {Safe(() => controller.ButtonCount.ToString())}");
        Console.WriteLine($"    Switches      : {Safe(() => controller.SwitchCount.ToString())}");
        Console.WriteLine($"    Axes          : {Safe(() => controller.AxisCount.ToString())}");
        Console.WriteLine($"    Wireless      : {Safe(() => controller.IsWireless.ToString())}");

        var switchCount = Safe(() => controller.SwitchCount, 0);

        for (var s = 0; s < switchCount; s++)
        {
            var kind = Safe(() => controller.GetSwitchKind(s).ToString());
            Console.WriteLine($"      switch {s}    : {kind}");
        }

        Console.WriteLine();
    }

    private static void Watch(IReadOnlyList<RawGameController> controllers)
    {
        // The settled list from Enumerate, not a fresh read: the first version re-read here and
        // watched six devices after describing three, so the indices in the two blocks disagreed.
        foreach (var controller in controllers)
        {
            Devices.Add(new Device(controller));
        }

        foreach (var device in Devices)
        {
            device.ReadInto(device.Buttons, device.Switches, device.Axes);
        }

        Console.WriteLine("[initial state] buttons already held with nothing touched:");

        foreach (var device in Devices)
        {
            var held = device.HeldIndices();
            Console.WriteLine($"  #{device.Index}: {(held.Count == 0 ? "(none)" : string.Join(", ", held))}");

            foreach (var index in held)
            {
                device.Down[index] = Clock.Elapsed.TotalSeconds;
                device.HeldFromStart.Add(index);
            }

            device.SnapshotSwitches();
        }

        Console.WriteLine();
        Console.WriteLine("[watching] flip switches and press buttons. Ctrl+C to stop and summarise.");
        Console.WriteLine();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            s_stop = true;
        };

        var period = TimeSpan.FromMilliseconds(1000.0 / PollHz);

        while (!s_stop)
        {
            foreach (var device in Devices)
            {
                device.PollAndReport();
            }

            Thread.Sleep(period);
        }
    }

    private static void Summarise()
    {
        Console.WriteLine();
        Console.WriteLine("[summary] hold durations decide maintained vs spring-return:");
        Console.WriteLine("  a switch you flipped and left holds for seconds; one that snaps back");
        Console.WriteLine("  releases in a few hundred ms without you doing anything.");
        Console.WriteLine();

        foreach (var device in Devices)
        {
            foreach (var (index, holds) in device.Holds.OrderBy(pair => pair.Key))
            {
                var fromStart = device.HeldFromStart.Contains(index) ? "  (was held at startup)" : string.Empty;
                var stillDown = device.Buttons[index] ? "  (still down at exit)" : string.Empty;
                var ms = holds.Count == 0
                    ? "never released"
                    : string.Join(", ", holds.Select(hold => $"{hold:F0}ms"));

                Console.WriteLine($"  #{device.Index} btn {index,3}: {ms}{fromStart}{stillDown}");
            }

            foreach (var (index, moves) in device.SwitchMoves.OrderBy(pair => pair.Key))
            {
                Console.WriteLine($"  #{device.Index} sw  {index,3}: {string.Join(" -> ", moves)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Paste this back into the design session.");
    }

    private static string Safe(Func<string> read)
    {
        try
        {
            return read() ?? "(null)";
        }
        catch (Exception ex)
        {
            return $"(threw {ex.GetType().Name})";
        }
    }

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    private sealed class Device
    {
        private static int s_next;

        private readonly RawGameController _controller;

        public Device(RawGameController controller)
        {
            _controller = controller;
            Index = s_next++;
            Buttons = new bool[Math.Max(controller.ButtonCount, 1)];
            Previous = new bool[Buttons.Length];
            Switches = new GameControllerSwitchPosition[Math.Max(controller.SwitchCount, 1)];
            PreviousSwitches = new GameControllerSwitchPosition[Switches.Length];
            Axes = new double[Math.Max(controller.AxisCount, 1)];
        }

        public int Index { get; }

        public bool[] Buttons { get; }

        public bool[] Previous { get; }

        public GameControllerSwitchPosition[] Switches { get; }

        public GameControllerSwitchPosition[] PreviousSwitches { get; }

        public double[] Axes { get; }

        /// <summary>Press time in seconds, keyed by button index, for buttons currently down.</summary>
        public Dictionary<int, double> Down { get; } = [];

        public Dictionary<int, List<double>> Holds { get; } = [];

        public Dictionary<int, List<string>> SwitchMoves { get; } = [];

        public HashSet<int> HeldFromStart { get; } = [];

        public void ReadInto(bool[] buttons, GameControllerSwitchPosition[] switches, double[] axes) =>
            _controller.GetCurrentReading(buttons, switches, axes);

        public List<int> HeldIndices() =>
            [.. Enumerable.Range(0, Buttons.Length).Where(index => Buttons[index])];

        public void SnapshotSwitches() => Switches.CopyTo(PreviousSwitches, 0);

        public void PollAndReport()
        {
            Buttons.CopyTo(Previous, 0);
            Switches.CopyTo(PreviousSwitches, 0);

            try
            {
                ReadInto(Buttons, Switches, Axes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  #{Index} read failed: {ex.GetType().Name} — device gone?");
                return;
            }

            var now = Clock.Elapsed.TotalSeconds;

            for (var index = 0; index < Buttons.Length; index++)
            {
                if (Buttons[index] == Previous[index])
                {
                    continue;
                }

                if (Buttons[index])
                {
                    Down[index] = now;
                    Console.WriteLine($"  +{now,7:F3}s  #{Index}  btn {index,3}  DOWN");
                }
                else
                {
                    var held = Down.TryGetValue(index, out var since) ? (now - since) * 1000 : double.NaN;
                    Down.Remove(index);

                    if (!Holds.TryGetValue(index, out var list))
                    {
                        Holds[index] = list = [];
                    }

                    list.Add(held);
                    Console.WriteLine($"  +{now,7:F3}s  #{Index}  btn {index,3}  UP     held {held:F0}ms");
                }

                Holds.TryAdd(index, []);
            }

            for (var index = 0; index < Switches.Length; index++)
            {
                if (Switches[index] == PreviousSwitches[index])
                {
                    continue;
                }

                var move = $"{PreviousSwitches[index]}->{Switches[index]}";
                Console.WriteLine($"  +{now,7:F3}s  #{Index}  sw  {index,3}  {move}");

                if (!SwitchMoves.TryGetValue(index, out var moves))
                {
                    SwitchMoves[index] = moves = [];
                }

                moves.Add(move);
            }
        }
    }
}
