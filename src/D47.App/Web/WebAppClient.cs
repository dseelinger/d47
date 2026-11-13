using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Web;
using Microsoft.Extensions.Logging;

namespace D47.App.Web;

/// <summary>
/// The desktop side of the web app's link: a one-request listener on <c>127.0.0.1</c>, the browser opened at
/// <c>/link</c>, the code exchanged at <c>/link/token</c>, <c>PUT /snapshot</c> and <c>DELETE /snapshot</c>. The listener's prefix is
/// loopback only, which needs no elevation. Errors are logged by status code; a response body is never logged.
/// </summary>
public sealed class WebAppClient : IWebAppClient, IDisposable
{
    /// <summary>How long the listener waits for the browser's one request.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMinutes(5);

    private const string Linked = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>D47</title></head>"
        + "<body><p>D47 has the sign-in. You can close this tab and go back to D47.</p></body></html>";

    private readonly Action<string> _open;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly string _address;
    private readonly TimeSpan _wait;

    /// <param name="open">Opens a URL in the default browser.</param>
    public WebAppClient(
        Action<string> open,
        ILogger<WebAppClient> logger,
        HttpMessageHandler? handler = null,
        string address = WebAppSettings.Address,
        TimeSpan? wait = null)
    {
        _open = open;
        _logger = logger;
        _address = address.TrimEnd('/');
        _wait = wait ?? Wait;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47-web");
    }

    /// <summary>The listener prefix for a port: the IPv4 loopback address and nothing else.</summary>
    public static string Prefix(int port) => $"http://127.0.0.1:{port}/";

    public async Task<WebAppSignInResult> SignInAsync(CancellationToken cancellationToken)
    {
        var verifier = Pkce.NewVerifier();
        var challenge = Pkce.Challenge(verifier);

        using var listener = Listen(out var port);

        if (listener is null)
        {
            return WebAppSignInResult.Failed("D47 could not open a port on this PC to receive the sign-in.");
        }

        try
        {
            _open($"{_address}/link?port={port}&challenge={challenge}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning("Could not open the browser for the web app sign-in ({Error})", ex.GetType().Name);
            return WebAppSignInResult.Failed("D47 could not open your browser.");
        }

        var code = await CodeAsync(listener, cancellationToken).ConfigureAwait(false);

        if (code is null)
        {
            return WebAppSignInResult.Failed(cancellationToken.IsCancellationRequested
                ? "The sign-in was stopped."
                : $"No sign-in came back from the browser within {_wait.TotalMinutes:0} minutes.");
        }

        if (code.Length == 0)
        {
            return WebAppSignInResult.Failed("The browser came back without a sign-in code. Press Sign in again.");
        }

        return await ExchangeAsync(code, verifier, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteSnapshotAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, _address + "/snapshot");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 401: the web app no longer knows the token, so it keeps no snapshot under it.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return true;
            }

            _logger.LogInformation("The web app did not delete the snapshot: it returned {Status}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogInformation("The web app snapshot could not be deleted ({Error})", ex.GetType().Name);
            return false;
        }
    }

    public async Task<WebAppPutResult> PutSnapshotAsync(string token, byte[] gzippedJson, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new ByteArrayContent(gzippedJson);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");

            using var request = new HttpRequestMessage(HttpMethod.Put, _address + "/snapshot") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? WebAppPutResult.Done
                : new WebAppPutResult(false, (int)response.StatusCode, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new WebAppPutResult(false, null, ex.GetType().Name);
        }
    }

    public void Dispose() => _http.Dispose();

    /// <summary>A started listener on a free loopback port, or null after three ports were taken first.</summary>
    private HttpListener? Listen(out int port)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add(Prefix(port));

            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException ex)
            {
                _logger.LogInformation("Could not listen on loopback port {Port} ({Code})", port, ex.ErrorCode);
                listener.Close();
            }
        }

        port = 0;
        return null;
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();

        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    /// <summary>
    /// The code from the first request to <c>/linked</c>, empty when it had none; null after the wait, or when
    /// cancelled. Closes the listener.
    /// </summary>
    private async Task<string?> CodeAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(_wait);

        // GetContextAsync takes no token: closing the listener ends the wait.
        await using var closing = stop.Token.Register(listener.Close);

        try
        {
            while (true)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);

                if (context.Request.Url?.AbsolutePath != "/linked")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                var code = context.Request.QueryString["code"];
                await RespondAsync(context.Response).ConfigureAwait(false);

                return code ?? string.Empty;
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            listener.Close();
        }
    }

    private static async Task RespondAsync(HttpListenerResponse response)
    {
        var page = Encoding.UTF8.GetBytes(Linked);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = page.Length;
        await response.OutputStream.WriteAsync(page).ConfigureAwait(false);
        response.Close();
    }

    private async Task<WebAppSignInResult> ExchangeAsync(string code, string verifier, CancellationToken cancellationToken)
    {
        try
        {
            var body = JsonSerializer.Serialize(new Dictionary<string, string> { ["code"] = code, ["verifier"] = verifier });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(_address + "/link/token", content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("The web app refused the sign-in code: it returned {Status}", (int)response.StatusCode);
                return WebAppSignInResult.Failed(response.StatusCode == HttpStatusCode.BadRequest
                    ? "The web app did not accept the sign-in. Press Sign in again."
                    : $"The web app could not finish the sign-in ({(int)response.StatusCode}).");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String
                && document.RootElement.TryGetProperty("customerId", out var customer) && customer.ValueKind == JsonValueKind.String
                && token.GetString() is { Length: > 0 } tokenText
                && customer.GetString() is { Length: > 0 } customerText)
            {
                return new WebAppSignInResult(tokenText, customerText, null);
            }

            _logger.LogInformation("The web app's sign-in answer had no token or customer id");
            return WebAppSignInResult.Failed("The web app's answer could not be read.");
        }
        catch (JsonException)
        {
            _logger.LogInformation("The web app's sign-in answer was not JSON");
            return WebAppSignInResult.Failed("The web app's answer could not be read.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogInformation("The sign-in code could not be sent to the web app ({Error})", ex.GetType().Name);
            return WebAppSignInResult.Failed("D47 could not reach the web app.");
        }
    }
}
