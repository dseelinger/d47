using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using D47.Core.Configuration;
using D47.Donations.R2;
using D47.Donations.Store;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Donations;

/// <summary>The whole of the utility's surface.</summary>
public partial class MainWindow : Window
{
    private readonly UtilityPaths _paths;
    private readonly SecretStore _secrets;
    private readonly UtilityState _state;
    private readonly DownloadFolder _downloads;
    private readonly DonationInbox _inbox;
    private readonly DateTimeOffset _openedAt = DateTimeOffset.UtcNow;

    private R2Client? _client;

    public MainWindow()
    {
        InitializeComponent();

        _paths = UtilityPaths.ForCurrentUser();
        _paths.EnsureCreated();

        // No log sink at all, which is how the credential stays out of one.
        _secrets = new SecretStore(_paths.Secrets, new DpapiSecretProtector(), NullLogger<SecretStore>.Instance);
        _state = UtilityState.Read(_paths.StateFile);
        _downloads = new DownloadFolder(_paths.Downloads);
        _inbox = new DonationInbox(_downloads, _state, _paths.StateFile);

        FolderText.Text = _downloads.Folder;

        RefreshButton.Click += async (_, _) => await RefreshAsync();
        OpenFolderButton.Click += (_, _) => OpenFolder();
        DownloadButton.Click += async (_, _) => await DownloadSelectedAsync();
        SaveCredentialsButton.Click += async (_, _) => await SaveCredentialsAsync();

        Opened += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        if (!R2Credentials.TryRead(_secrets, out var credentials))
        {
            ShowSetup(true);
            StatusText.Text = "No R2 credential is stored yet.";
            return;
        }

        _client = new R2Client(credentials);
        await RefreshAsync();
    }

    private async Task SaveCredentialsAsync()
    {
        var account = AccountIdBox.Text?.Trim() ?? string.Empty;
        var access = AccessKeyIdBox.Text?.Trim() ?? string.Empty;
        var secret = SecretBox.Text?.Trim() ?? string.Empty;

        if (account.Length == 0 || access.Length == 0 || secret.Length == 0)
        {
            StatusText.Text = "All three parts are needed.";
            return;
        }

        var credentials = new R2Credentials(account, access, secret);
        credentials.Save(_secrets);

        // Not left on screen once stored.
        AccountIdBox.Text = AccessKeyIdBox.Text = SecretBox.Text = string.Empty;

        _client?.Dispose();
        _client = new R2Client(credentials);

        ShowSetup(false);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_client is null)
        {
            return;
        }

        RefreshButton.IsEnabled = DownloadButton.IsEnabled = false;
        StatusText.Text = "Listing…";
        DeletedText.Text = string.Empty;

        try
        {
            var outcome = await _inbox.RefreshAsync(
                cancel => _client.ListAsync(cancel),
                _openedAt,
                CancellationToken.None);

            if (!outcome.Succeeded)
            {
                StatusText.Text = $"Listing failed: {outcome.Problem} Nothing was deleted.";
                return;
            }

            await DescribeAsync(outcome);
        }
        finally
        {
            RefreshButton.IsEnabled = DownloadButton.IsEnabled = true;
        }
    }

    private async Task DescribeAsync(RefreshOutcome outcome)
    {
        // The build lives in custom metadata, which a listing does not carry.
        await Parallel.ForEachAsync(
            outcome.Objects,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (stored, cancel) =>
            {
                try
                {
                    stored.Build = (await _client!.HeadAsync(stored.Key.Key, cancel)).GetValueOrDefault("build");
                }
                catch (Exception ex) when (ex is R2Exception or HttpRequestException or TaskCanceledException)
                {
                    // The listing stands; this one row shows no build.
                }
            });

        DonationList.ItemsSource = outcome.Objects
            .OrderByDescending(stored => stored.LastModified)
            .Select(stored => new DonationRow(stored, _state.Downloaded.ContainsKey(stored.Key.Key)))
            .ToArray();

        var arrived = outcome.Arrived is { } count
            ? $"{count} since it was last opened"
            : "first opening";

        StatusText.Text = $"{outcome.Objects.Count} donations in the store, {arrived}.";

        if (outcome.Deleted.Count > 0)
        {
            DeletedText.Text =
                $"Gone from the store, so deleted here: {string.Join(", ", outcome.Deleted)}";
        }
    }

    private async Task DownloadSelectedAsync()
    {
        if (_client is null || DonationList.SelectedItems is not { Count: > 0 } selected)
        {
            return;
        }

        var rows = selected.OfType<DonationRow>().ToArray();

        RefreshButton.IsEnabled = DownloadButton.IsEnabled = false;
        DeletedText.Text = string.Empty;

        var written = 0;
        var problems = new List<string>();

        try
        {
            foreach (var row in rows)
            {
                StatusText.Text = $"Downloading {row.Stored.Key.ZipName}…";

                try
                {
                    var fetched = await _client.GetAsync(row.Stored.Key.Key, CancellationToken.None);
                    var payload = fetched.Gzipped ? DownloadFolder.Decompress(fetched.Body) : fetched.Body;
                    var result = _downloads.Save(row.Stored.Key, payload, fetched.Sha256);

                    if (result.Written)
                    {
                        _state.Downloaded[row.Stored.Key.Key] = result.ZipName;
                        written++;
                    }
                    else
                    {
                        problems.Add($"{result.ZipName}: {result.Problem}");
                    }
                }
                catch (Exception ex) when (ex is R2Exception or HttpRequestException or InvalidDataException)
                {
                    problems.Add($"{row.Stored.Key.ZipName}: {ex.Message}");
                }
            }

            _state.Write(_paths.StateFile);

            StatusText.Text = problems.Count == 0
                ? $"Downloaded {written} of {rows.Length}."
                : $"Downloaded {written} of {rows.Length}. Not written — {string.Join("; ", problems)}";

            DonationList.ItemsSource = DonationList.ItemsSource?
                .OfType<DonationRow>()
                .Select(row => new DonationRow(row.Stored, _state.Downloaded.ContainsKey(row.Stored.Key.Key)))
                .ToArray();
        }
        finally
        {
            RefreshButton.IsEnabled = DownloadButton.IsEnabled = true;
        }
    }

    private void OpenFolder()
    {
        Directory.CreateDirectory(_downloads.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_downloads.Folder}\"") { UseShellExecute = true });
    }

    private void ShowSetup(bool showing)
    {
        SetupPanel.IsVisible = showing;
        DonationList.IsVisible = !showing;
        DownloadButton.IsEnabled = !showing;
        RefreshButton.IsEnabled = !showing;
    }
}

/// <summary>One row, formatted for a fixed-width list.</summary>
internal sealed record DonationRow(StoredObject Stored, bool Downloaded)
{
    public override string ToString() =>
        string.Join(
            "  ",
            Stored.Key.Kind.PadRight(7),
            Stored.Key.ShortDonor,
            Stored.LastModified.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Size().PadLeft(9),
            (Stored.Build ?? "—").PadRight(16),
            Downloaded ? "downloaded" : string.Empty);

    private string Size() =>
        Stored.CompressedBytes >= 1024 * 1024
            ? $"{Stored.CompressedBytes / (1024.0 * 1024.0):F1} MB"
            : $"{Stored.CompressedBytes / 1024.0:F1} KB";
}
