using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Web;

/// <summary>
/// Signs this install in to the web app for the Commander who is flying, and deletes every Commander's snapshot
/// when <see cref="WebAppSettings.Enabled"/> is turned off or is found off at start. An upload token is a Commander
/// secret, read at each use. Sign-in runs on a press and deletes run through <see cref="Dispatch"/>; nothing here
/// runs on the tick thread. A build that cannot send makes no request and keeps every token.
/// </summary>
public sealed class WebAppLink
{
    /// <summary>The secret an upload token is stored under, named for its Commander by <see cref="CommanderSecret.Name"/>.</summary>
    public const string TokenSecretName = "webapp.uploadToken";

    private static readonly string TokenPrefix = CommanderSecret.Name(TokenSecretName, string.Empty);

    private readonly Func<D47Settings> _settings;
    private readonly SecretStore _secrets;
    private readonly Func<CommanderIdentity?> _flying;
    private readonly IWebAppClient _client;
    private readonly bool _canSend;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private bool _wasEnabled;
    private bool _deleting;
    private volatile string? _refusedFor;

    /// <param name="canSend">False on a test drive: nothing is sent and every token is kept.</param>
    public WebAppLink(
        Func<D47Settings> settings,
        SecretStore secrets,
        Func<CommanderIdentity?> flying,
        IWebAppClient client,
        bool canSend,
        ILogger<WebAppLink> logger)
    {
        _settings = settings;
        _secrets = secrets;
        _flying = flying;
        _client = client;
        _canSend = canSend;
        _logger = logger;
        _wasEnabled = settings().WebApp.Enabled;
    }

    /// <summary>Runs the deletes; must return without waiting.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>Raised on any thread when <see cref="Note"/> may have changed.</summary>
    public event Action? Changed;

    /// <summary>Whether a Frontier customer id is the journal's FID without its leading F (#2371).</summary>
    public static bool BelongsTo(string customerId, string fid) =>
        fid.Length > 1 && fid[0] == 'F' && string.Equals(fid[1..], customerId, StringComparison.Ordinal);

    /// <summary>Whether an upload token is stored for this FID; the token is not read.</summary>
    public bool SignedIn(string fid)
    {
        lock (_gate)
        {
            return _secrets.Names.Contains(CommanderSecret.Name(TokenSecretName, fid));
        }
    }

    /// <summary>The Web app row's line: off, on and not signed in, or on and signed in, naming the Commander.</summary>
    public string Note(D47Settings settings)
    {
        if (!settings.WebApp.Enabled)
        {
            bool deleting;
            int left;

            lock (_gate)
            {
                deleting = _deleting;
                left = TokenNames().Count;
            }

            return deleting ? "Off. Deleting the snapshot from the web app."
                : left > 0 ? "Off. The snapshot is not deleted from the web app yet. D47 tries again at the next start."
                : "Off. Nothing is sent to the web app.";
        }

        if (_flying() is not { } commander)
        {
            return "On, and not signed in. Sign in once D47 has read your Commander from the journal.";
        }

        if (SignedIn(commander.FrontierId))
        {
            return $"On, and signed in as CMDR {commander.Name}.";
        }

        return string.Equals(_refusedFor, commander.FrontierId, StringComparison.Ordinal)
            ? $"On, and not signed in. The Frontier account that signed in is not CMDR {commander.Name}'s, so nothing was stored."
            : $"On, and not signed in. Press Sign in to link CMDR {commander.Name}.";
    }

    /// <summary>Whether Sign in can be pressed.</summary>
    public bool CanSignIn(D47Settings settings) => settings.WebApp.Enabled && _canSend;

    /// <summary>Signs in for the Commander flying when it is pressed, and stores the token for them.</summary>
    public async Task<string?> SignInAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!_settings().WebApp.Enabled)
        {
            return "Turn on Web app first.";
        }

        if (!_canSend)
        {
            return "This is a test drive, so nothing is sent to the web app.";
        }

        if (_flying() is not { } commander)
        {
            return "D47 has not read a Commander from the journal yet. Load a game in Elite, then sign in.";
        }

        var result = await _client.SignInAsync(cancellationToken).ConfigureAwait(false);

        if (result is not { Token: { } token, CustomerId: { } customer })
        {
            return result.Failure ?? "The sign-in did not finish.";
        }

        if (!BelongsTo(customer, commander.FrontierId))
        {
            _logger.LogInformation("A web app sign-in was for another Frontier account than the flying Commander's; nothing was stored");
            _refusedFor = commander.FrontierId;
            Changed?.Invoke();
            return $"The Frontier account that signed in is not CMDR {commander.Name}'s, so nothing was stored.";
        }

        lock (_gate)
        {
            _secrets.Set(CommanderSecret.Name(TokenSecretName, commander.FrontierId), token);
        }

        _refusedFor = null;
        Changed?.Invoke();

        if (!_settings().WebApp.Enabled)
        {
            StartDeletes();
            return "Web app was turned off during the sign-in, so the link is being deleted.";
        }

        return $"Signed in as CMDR {commander.Name}.";
    }

    /// <summary>Call when any setting changes; turning Web app off starts the deletes.</summary>
    public void SettingsChanged()
    {
        var enabled = _settings().WebApp.Enabled;
        bool turnedOff;

        lock (_gate)
        {
            turnedOff = _wasEnabled && !enabled;
            _wasEnabled = enabled;
        }

        if (turnedOff)
        {
            StartDeletes();
        }
    }

    /// <summary>Call once at start: while Web app is off, deletes what an earlier run could not.</summary>
    public void Start()
    {
        if (!_settings().WebApp.Enabled)
        {
            StartDeletes();
        }
    }

    private void StartDeletes()
    {
        if (!_canSend)
        {
            return;
        }

        lock (_gate)
        {
            if (_deleting || TokenNames().Count == 0)
            {
                return;
            }

            _deleting = true;
        }

        Changed?.Invoke();
        Dispatch(DeleteAllAsync);
    }

    private async Task DeleteAllAsync()
    {
        try
        {
            List<string> names;

            lock (_gate)
            {
                names = TokenNames();
            }

            foreach (var name in names)
            {
                string? token;

                lock (_gate)
                {
                    _secrets.TryGet(name, out token);
                }

                if (token is null)
                {
                    _logger.LogWarning("The web app upload token {Name} could not be read, so its snapshot was not deleted", name);
                    continue;
                }

                bool deleted;

                try
                {
                    deleted = await _client.DeleteSnapshotAsync(token, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Deleting a web app snapshot failed ({Error})", ex.GetType().Name);
                    deleted = false;
                }

                if (deleted)
                {
                    lock (_gate)
                    {
                        _secrets.Remove(name);
                    }
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _deleting = false;
            }

            Changed?.Invoke();
        }
    }

    // Callers hold _gate.
    private List<string> TokenNames() =>
        [.. _secrets.Names.Where(name => name.StartsWith(TokenPrefix, StringComparison.OrdinalIgnoreCase))];
}
