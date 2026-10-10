using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Donation;
using D47.App.Panel;
using D47.Core.Diagnostics.Donation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The journal donation, step by step, captured for docs/donate-journals.md with the controls to press;
/// tools/donate-walkthrough.py draws the boxes.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheDonationWalkthroughIsCapturedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 19, 30, 0, TimeSpan.Zero);

    private static readonly string[][] Sessions =
    [
        [
            """{"timestamp":"2026-09-27T18:02:11Z","event":"Fileheader","part":1,"language":"English/UK","Odyssey":true,"gameversion":"4.2.1.0","build":"r312345/r0 "}""",
            """{"timestamp":"2026-09-27T18:02:14Z","event":"Commander","FID":"F9876543","Name":"Jameson"}""",
            """{"timestamp":"2026-09-27T18:02:15Z","event":"LoadGame","FID":"F9876543","Commander":"Jameson","Horizons":true,"Odyssey":true,"Ship":"Python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","FuelLevel":32.0,"FuelCapacity":32.0,"GameMode":"Open","Credits":48210377,"Loan":0}""",
            """{"timestamp":"2026-09-27T18:02:30Z","event":"Location","Docked":true,"StationName":"Jameson Memorial","StationType":"Orbis","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"Body":"Shinrarta Dezhra A 1","BodyID":12,"BodyType":"Planet"}""",
            """{"timestamp":"2026-09-27T18:05:02Z","event":"Undocked","StationName":"Jameson Memorial","StationType":"Orbis"}""",
            """{"timestamp":"2026-09-27T18:09:40Z","event":"FSDJump","StarSystem":"LHS 3447","SystemAddress":2381065031002,"Body":"LHS 3447","BodyID":0,"BodyType":"Star","JumpDist":19.87,"FuelUsed":2.11,"FuelLevel":29.89}""",
            """{"timestamp":"2026-09-27T18:10:05Z","event":"FSSDiscoveryScan","Progress":0.42,"BodyCount":11,"NonBodyCount":4,"SystemName":"LHS 3447","SystemAddress":2381065031002}""",
            """{"timestamp":"2026-09-27T18:14:22Z","event":"ReceiveText","From":"Some Other Pilot","Message":"o7 commander","Channel":"local"}""",
            """{"timestamp":"2026-09-27T18:21:47Z","event":"Docked","StationName":"Bluford Orbital","StationType":"Coriolis","StarSystem":"LHS 3447","SystemAddress":2381065031002,"MarketID":3221522176}""",
            """{"timestamp":"2026-09-27T18:24:10Z","event":"MarketSell","MarketID":3221522176,"Type":"gold","Count":64,"SellPrice":47210,"TotalSale":3021440,"AvgPricePaid":44100}""",
            """{"timestamp":"2026-09-27T18:40:00Z","event":"Shutdown"}""",
        ],
        [
            """{"timestamp":"2026-10-04T20:11:03Z","event":"Fileheader","part":1,"language":"English/UK","Odyssey":true,"gameversion":"4.2.1.0","build":"r312345/r0 "}""",
            """{"timestamp":"2026-10-04T20:11:05Z","event":"Commander","FID":"F9876543","Name":"Jameson"}""",
            """{"timestamp":"2026-10-04T20:11:06Z","event":"LoadGame","FID":"F9876543","Commander":"Jameson","Horizons":true,"Odyssey":true,"Ship":"Python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","FuelLevel":32.0,"FuelCapacity":32.0,"GameMode":"Open","Credits":51231817,"Loan":0}""",
            """{"timestamp":"2026-10-04T20:11:20Z","event":"Location","Docked":true,"StationName":"Bluford Orbital","StationType":"Coriolis","StarSystem":"LHS 3447","SystemAddress":2381065031002}""",
            """{"timestamp":"2026-10-04T20:13:44Z","event":"MissionAccepted","Faction":"LHS 3447 Holdings","Name":"Mission_Courier","LocalisedName":"Deliver data to Shinrarta Dezhra","DestinationSystem":"Shinrarta Dezhra","DestinationStation":"Jameson Memorial","Expiry":"2026-10-05T20:13:44Z","Influence":"++","Reputation":"++","Reward":412000,"MissionID":1042337761}""",
            """{"timestamp":"2026-10-04T20:15:02Z","event":"Undocked","StationName":"Bluford Orbital","StationType":"Coriolis"}""",
            """{"timestamp":"2026-10-04T20:19:51Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"Body":"Shinrarta Dezhra","BodyID":0,"BodyType":"Star","JumpDist":19.87,"FuelUsed":2.11,"FuelLevel":29.89}""",
            """{"timestamp":"2026-10-04T20:27:13Z","event":"Docked","StationName":"Jameson Memorial","StationType":"Orbis","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"MarketID":128666762}""",
            """{"timestamp":"2026-10-04T20:28:40Z","event":"MissionCompleted","Faction":"LHS 3447 Holdings","Name":"Mission_Courier","MissionID":1042337761,"Reward":412000}""",
            """{"timestamp":"2026-10-04T20:45:00Z","event":"Shutdown"}""",
        ],
        [
            """{"timestamp":"2026-10-09T18:40:12Z","event":"Fileheader","part":1,"language":"English/UK","Odyssey":true,"gameversion":"4.2.1.0","build":"r312345/r0 "}""",
            """{"timestamp":"2026-10-09T18:40:14Z","event":"Commander","FID":"F9876543","Name":"Jameson"}""",
            """{"timestamp":"2026-10-09T18:40:15Z","event":"LoadGame","FID":"F9876543","Commander":"Jameson","Horizons":true,"Odyssey":true,"Ship":"Python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","FuelLevel":32.0,"FuelCapacity":32.0,"GameMode":"Open","Credits":51643817,"Loan":0}""",
            """{"timestamp":"2026-10-09T18:40:30Z","event":"Location","Docked":true,"StationName":"Jameson Memorial","StationType":"Orbis","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106}""",
            """{"timestamp":"2026-10-09T18:44:02Z","event":"Undocked","StationName":"Jameson Memorial","StationType":"Orbis"}""",
            """{"timestamp":"2026-10-09T18:49:30Z","event":"FSDJump","StarSystem":"Wolf 359","SystemAddress":8064696292050,"Body":"Wolf 359","BodyID":0,"BodyType":"Star","JumpDist":18.01,"FuelUsed":1.94,"FuelLevel":30.06}""",
            """{"timestamp":"2026-10-09T18:52:18Z","event":"Scan","ScanType":"Detailed","BodyName":"Wolf 359 1","BodyID":2,"StarSystem":"Wolf 359","SystemAddress":8064696292050,"DistanceFromArrivalLS":421.5,"PlanetClass":"Icy body","Landable":true}""",
            """{"timestamp":"2026-10-09T19:02:44Z","event":"Bounty","Rewards":[{"Faction":"Wolf 359 Gold Partners","Reward":184200}],"Target":"cobramkiii","TotalReward":184200,"VictimFaction":"Wolf 359 Crimson Raiders"}""",
            """{"timestamp":"2026-10-09T19:11:05Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"Body":"Sol","BodyID":0,"BodyType":"Star","JumpDist":7.78,"FuelUsed":0.61,"FuelLevel":29.45}""",
            """{"timestamp":"2026-10-09T19:24:51Z","event":"Docked","StationName":"Abraham Lincoln","StationType":"Orbis","StarSystem":"Sol","SystemAddress":10477373803,"MarketID":128016640}""",
        ],
    ];

    private sealed record Box(string Name, double X, double Y, double Width, double Height);

    private sealed record Shot(string File, double Width, double Height, IReadOnlyList<Box> Boxes);

    private readonly List<Shot> _shots = [];

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(found => found.Name == name && found.IsEffectivelyVisible);

    private static Box BoxOf(Window window, string label, Control control)
    {
        var at = control.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Box(label, at.X, at.Y, control.Bounds.Width, control.Bounds.Height);
    }

    private void Save(Window window, string file, params Box[] boxes)
    {
        Jobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(file);

        _shots.Add(new Shot(file, frame.PixelSize.Width, frame.PixelSize.Height, boxes));
    }

    private static string Journals()
    {
        var folder = TempFolders.Create("d47-walkthrough-journals");

        foreach (var lines in Sessions)
        {
            var first = DateTimeOffset.Parse(
                JsonDocument.Parse(lines[0]).RootElement.GetProperty("timestamp").GetString()!,
                CultureInfo.InvariantCulture);

            File.WriteAllLines(
                Path.Combine(folder, $"Journal.{first.UtcDateTime:yyyy-MM-ddTHHmmss}.01.log"),
                lines);
        }

        return folder;
    }

    private static IReadOnlyList<JournalEntry> Entries()
    {
        var log = new JournalLog();

        foreach (var line in Sessions[^1])
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            log.Add([parsed!]);
        }

        return log.Read();
    }

    [AvaloniaFact]
    public void TheJournalHistoryIsDonatedInFiveSteps()
    {
        using var look = AppLook.Put(matrix: GuiColourMatrix.Identity);

        var folder = Journals();
        var paperwork = new ExcerptPaperwork("1.31.0", Now);
        Pseudonyms? names = null;

        Func<ExcerptRequest, (string Text, ExcerptTally Tally)> build = request =>
        {
            var excerpt = IncidentExcerpt.Take(
                IncidentSources.Journals(new DiskFileSystem(), folder, request.From, request.To),
                [],
                request);

            return (ExcerptReport.Render(excerpt, paperwork), excerpt.Tally);
        };

        Func<CorpusScope, IProgress<int>, CancellationToken, Task<HelpImprovePage.CorpusReading>> read =
            (scope, progress, cancel) => Task.Run(
                () =>
                {
                    names = IncidentExcerpt.Seeded(null, null);
                    var survey = CorpusDonation.Survey(new DiskFileSystem(), folder, scope.From(Now), Now, names, null, progress, cancel);
                    return new HelpImprovePage.CorpusReading(survey, CorpusReport.Render(survey, paperwork));
                },
                cancel);

        Func<Stream, IProgress<int>, CancellationToken, Task> write = (_, _, _) => Task.CompletedTask;

        const string Donor = "3f9c2a7be1d04c58a6e2917f0b4d8c13";
        var stored = DonationOutcome.Stored(
            $"corpus/{Donor}/{DonationEnvelope.Stamp(Now)}-8e41c09a7d2b5f63.jsonl.gz");
        var receipt = $@"C:\Users\Commander\AppData\Local\Programs\d47\data\donations\{DonationEnvelope.Stamp(Now)}-corpus.receipt.md";

        var model = new PanelViewModel { JournalSource = _ => Entries() };
        var panel = new PanelView { DataContext = model };

        panel.Furnish(PanelTab.Stories, _ => new TextBlock(), new NavCrumb("stories", "Stories"));
        panel.Furnish(PanelTab.Commander, _ => new TextBlock(), new NavCrumb("checklist", "Checklist"));
        panel.Furnish(
            PanelTab.Assets,
            _ => new TextBlock(),
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("engineers", "Engineers"));
        panel.Furnish(PanelTab.Navigation, _ => new TextBlock(), new NavCrumb("plan", "Plan"));
        panel.EnableSettings(() => new TextBlock());
        panel.EnableSearch();
        panel.EnableRawJournal();

        panel.EnableDonation(() => _ = panel.Open(new HelpImprovePage(
            Now,
            build,
            (_, _) => Task.FromResult(new DonationSent(stored, receipt)),
            "d47-donations.dseelinger.workers.dev",
            read,
            write,
            (_, _, _) => Task.FromResult(new DonationSent(stored, receipt)),
            _ => Task.FromResult("Everything you sent has been deleted."))));

        var window = new Window { Content = panel, Width = 1180, Height = 1040 };
        window.Show();
        Jobs();

        // 1. The Journal File reading, with the button on its bar.
        panel.Page = TranscriptPage.Journal;
        model.RefreshJournal();
        Jobs();

        var donate = Named<Button>(panel, "DonateButton");

        Save(
            window,
            "donate-2-journal.png",
            BoxOf(window, "tab", Named<RadioButton>(panel, "TranscriptTab")),
            BoxOf(window, "journal", Named<Control>(panel, "ModeReadings").GetVisualDescendants().OfType<TextBlock>()
                .First(block => string.Equals(block.Text, "Journal File", StringComparison.OrdinalIgnoreCase))),
            BoxOf(window, "button", donate));

        // 2. The page opens on the excerpt; the history is the checkbox.
        Press(donate);

        var page = panel.GetVisualDescendants().OfType<HelpImprovePage>().Single();

        Save(
            window,
            "donate-3-opened.png",
            BoxOf(window, "history", Named<CheckBox>(page, "IncludeHistory")));

        // 3. History on: how far back, then Read my journals.
        Named<CheckBox>(page, "IncludeHistory").IsChecked = true;
        Jobs();

        var scope = Named<Segment>(page, "Scope");
        scope.SelectedIndex = CorpusScope.All.Count - 1;
        Jobs();

        Save(
            window,
            "donate-4-history.png",
            BoxOf(window, "scope", scope),
            BoxOf(window, "read", Named<Button>(page, "ReadJournals")));

        // 4. Read: the report, opened.
        Press(Named<Button>(page, "ReadJournals"));

        var waited = DateTime.UtcNow;

        while (!Named<Button>(page, "SendCorpus").IsEnabled && DateTime.UtcNow - waited < TimeSpan.FromSeconds(10))
        {
            Jobs();
            Thread.Sleep(20);
        }

        Jobs();

        var disclosure = Named<Button>(page, "DisclosureToggle");
        Press(disclosure);

        Save(
            window,
            "donate-5-report.png",
            BoxOf(window, "disclosure", disclosure),
            BoxOf(window, "report", Named<Control>(page, "DisclosurePane")),
            BoxOf(window, "send", Named<Button>(page, "SendCorpus")));

        // 5. Sent.
        Press(Named<Button>(page, "SendCorpus"));
        Jobs();

        Save(
            window,
            "donate-6-sent.png",
            BoxOf(window, "status", Named<TextBlock>(page, "SendStatus")),
            BoxOf(window, "forget", Named<Button>(page, "ForgetDonations")));

        window.Close();

        if (TestSurface.CapturesWanted)
        {
            File.WriteAllText(
                Path.Combine(TestSurface.CaptureDirectory, "donate-boxes.json"),
                JsonSerializer.Serialize(_shots, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
        }
    }
}
