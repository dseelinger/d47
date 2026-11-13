using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using D47.App.Web;
using D47.Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class TheWebAppSignInListensOnLoopbackOnlyTests
{
    private const string Address = "https://web.example.test";

    private sealed class TokenRoute : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public Uri? Url { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"token\":\"upload-token\",\"customerId\":\"1234\"}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private static Dictionary<string, string> Query(Uri url) =>
        url.Query.TrimStart('?').Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));

    private static IEnumerable<IPAddress> OtherAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(card => card.OperationalStatus == OperationalStatus.Up)
            .SelectMany(card => card.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => !IPAddress.IsLoopback(address) && address.AddressFamily == AddressFamily.InterNetwork);

    private static async Task<bool> Connects(IPAddress address, int port)
    {
        using var client = new TcpClient(address.AddressFamily);
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            await client.ConnectAsync(address, port, limit.Token);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    [Fact]
    public async Task TheBrowsersOneRequestOnLoopbackIsExchangedForTheTokenAndTheListenerCloses()
    {
        var route = new TokenRoute();
        Uri? opened = null;
        Task? browser = null;
        var otherAddressesRefused = new List<bool>();
        int port = 0;

        using var browserClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        using var client = new WebAppClient(
            url =>
            {
                opened = new Uri(url);
                port = int.Parse(Query(opened)["port"], System.Globalization.CultureInfo.InvariantCulture);
                browser = Task.Run(async () =>
                {
                    foreach (var address in OtherAddresses())
                    {
                        otherAddressesRefused.Add(!await Connects(address, port));
                    }

                    using var stray = await browserClient.GetAsync($"http://127.0.0.1:{port}/favicon.ico");
                    Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);

                    using var linked = await browserClient.GetAsync($"http://127.0.0.1:{port}/linked?code=the-code");
                    Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
                });
            },
            NullLogger<WebAppClient>.Instance,
            route,
            Address);

        var result = await client.SignInAsync(CancellationToken.None);
        await browser!;

        Assert.Equal(new WebAppSignInResult("upload-token", "1234", null), result);

        Assert.Equal($"{Address}/link", opened!.GetLeftPart(UriPartial.Path));
        var challenge = Query(opened)["challenge"];
        Assert.Equal(43, challenge.Length);

        Assert.Equal(new Uri($"{Address}/link/token"), route.Url);
        using var sent = JsonDocument.Parse(route.Body!);
        Assert.Equal("the-code", sent.RootElement.GetProperty("code").GetString());
        Assert.Equal(challenge, Pkce.Challenge(sent.RootElement.GetProperty("verifier").GetString()!));

        Assert.All(otherAddressesRefused, Assert.True);
        Assert.Equal("http://127.0.0.1:" + port + "/", WebAppClient.Prefix(port));

        var afterwards = await browserClient.GetAsync($"http://127.0.0.1:{port}/linked?code=again", TestContext.Current.CancellationToken)
            .ContinueWith(sent => sent.IsCompletedSuccessfully && sent.Result.IsSuccessStatusCode, TaskScheduler.Default);
        Assert.False(afterwards);
    }

    [Fact]
    public async Task WithNoRequestTheListenerClosesAfterTheWaitAndNothingIsExchanged()
    {
        var route = new TokenRoute();

        using var client = new WebAppClient(_ => { }, NullLogger<WebAppClient>.Instance, route, Address, TimeSpan.FromMilliseconds(200));

        var result = await client.SignInAsync(CancellationToken.None);

        Assert.Null(result.Token);
        Assert.StartsWith("No sign-in came back from the browser", result.Failure, StringComparison.Ordinal);
        Assert.Null(route.Url);
    }
}
