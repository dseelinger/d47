using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;
using static D47.Scenarios.Tests.ModelComparison.ModelComparisonTests;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// Sends d47's Responses request with web search to xAI, with and without registered function tools, and writes what
/// came back. Paid and opt-in: <c>D47_COMPARE=1</c>, the key in <c>D47_COMPARE_XAI_KEY</c>, the output folder in
/// <c>D47_COMPARE_OUT</c>, stopped by <c>D47_COMPARE_CAP</c> dollars (default 1).
/// </summary>
[Trait("Category", "Integration")]
public class XaiWebSearchTests
{
    private const string Endpoint = "https://api.x.ai/v1";
    private const decimal InputPerToken = 1.25m / 1_000_000;
    private const decimal OutputPerToken = 2.50m / 1_000_000;
    private const decimal PerSearch = 5m / 1_000;

    private static readonly string[] Questions =
    [
        "What are the patch notes for the most recent Elite Dangerous update?",
        "What is the current Elite Dangerous Powerplay merit cap per week, according to recent sources?",
        "What did Frontier Developments announce for Elite Dangerous in the last month?",
    ];

    private const string ToolSchema =
        """{"type":"object","properties":{"system":{"type":"string","description":"System name."}},"required":["system"]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WebSearchAgainstXai()
    {
        Assert.SkipUnless(Env("D47_COMPARE") == "1", "set D47_COMPARE=1 to compare models; this spends money");

        var key = Required("D47_COMPARE_XAI_KEY");
        var output = Required("D47_COMPARE_OUT");
        Directory.CreateDirectory(output);

        var model = Env("D47_COMPARE_XAI_MODEL") ?? "grok-4.20-0309-reasoning";
        var cap = decimal.Parse(Env("D47_COMPARE_CAP") ?? "1", CultureInfo.InvariantCulture);
        var spent = 0m;
        var lines = new List<string>();

        foreach (var withTools in new[] { false, true })
        {
            foreach (var question in Questions)
            {
                Assert.True(spent < cap, $"spent {spent:F4} dollars, at the cap of {cap}");

                var wire = new WireLog();
                using var http = new HttpClient(wire);
                using var provider = new ResponsesLlmProvider(key, Endpoint, http);

                var request = new LlmRequest
                {
                    Model = model,
                    Effort = ThinkingEffort.Low,
                    Sampling = LlmSampling.Conversation,
                    WebSearch = true,
                    ToolCallsAllowed = true,
                    MaxOutputTokens = 3000,
                    Prompt = new PromptAssembly
                    {
                        Persona = null,
                        AboutMe = null,
                        Tools = withTools
                            ? [new ToolAdvertisement("get_system_info", "Looks up a star system.", ToolSchema)]
                            : [],
                        History = [new ConversationMessage(ConversationRole.User, question)],
                    },
                };

                var clock = Stopwatch.StartNew();
                double? first = null;
                var text = new StringBuilder();
                LlmStreamEvent.Completed? completed = null;
                string? failure = null;
                var toolCalls = 0;

                await foreach (var step in provider.StreamAsync(request, Token))
                {
                    switch (step)
                    {
                        case LlmStreamEvent.TextDelta delta:
                            first ??= clock.Elapsed.TotalSeconds;
                            text.Append(delta.Text);
                            break;
                        case LlmStreamEvent.ToolUse:
                            first ??= clock.Elapsed.TotalSeconds;
                            toolCalls++;
                            break;
                        case LlmStreamEvent.Completed done:
                            completed = done;
                            break;
                        case LlmStreamEvent.Failed failed:
                            failure = failed.Message;
                            break;
                    }
                }

                var total = clock.Elapsed.TotalSeconds;
                var usage = completed?.Usage;
                var searches = usage?.WebSearchRequests ?? 0;
                spent += (usage?.InputTokens ?? 0) * InputPerToken
                    + (usage?.OutputTokens ?? 0) * OutputPerToken
                    + searches * PerSearch;

                var raw = wire.ResponseText;
                lines.Add(JsonSerializer.Serialize(new
                {
                    model,
                    withTools,
                    question,
                    attempts = wire.Attempts,
                    failure,
                    searchesCounted = searches,
                    rawSearchItems = Count(raw, "\"type\":\"web_search_call\""),
                    toolCalls,
                    usage,
                    citations = Count(raw, "url_citation") + Count(raw, "\"citations\""),
                    secondsToFirstOutput = first,
                    secondsTotal = total,
                    stop = completed?.StopReason.ToString(),
                    answer = text.ToString(),
                }));
            }
        }

        File.WriteAllLines(Path.Combine(output, "xai-web-search.jsonl"), lines);
        File.WriteAllText(Path.Combine(output, "xai-web-search-spend.txt"), spent.ToString("F4", CultureInfo.InvariantCulture));
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Records each request's status and error body, and copies the successful response as it streams.</summary>
    private sealed class WireLog : DelegatingHandler
    {
        private readonly StringBuilder _response = new();
        private bool _stripped;

        public WireLog()
            : base(new HttpClientHandler())
        {
        }

        public List<object> Attempts { get; } = [];

        public string ResponseText => _response.ToString();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Attempts.Add(new { status, request = body, error });

                // d47 does not recognise this refusal, so the test drops the field itself and records that it did.
                if (error.Contains("reasoningEffort", StringComparison.Ordinal) && !_stripped)
                {
                    _stripped = true;
                    var node = System.Text.Json.Nodes.JsonNode.Parse(body)!.AsObject();
                    node.Remove("reasoning");
                    using var retry = new HttpRequestMessage(request.Method, request.RequestUri)
                    {
                        Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json"),
                    };
                    retry.Headers.Authorization = request.Headers.Authorization;
                    retry.Headers.Accept.ParseAdd("text/event-stream");
                    return await SendAsync(retry, cancellationToken);
                }

                return response;
            }

            Attempts.Add(new { status, request = body, error = (string?)null });

            var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            var content = new StreamContent(new TeeStream(source, _response));

            foreach (var header in response.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            response.Content = content;
            return response;
        }
    }

    private sealed class TeeStream(Stream inner, StringBuilder copy) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            copy.Append(Encoding.UTF8.GetString(buffer[..read]));
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            copy.Append(Encoding.UTF8.GetString(buffer.Span[..read]));
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
