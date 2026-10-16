using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using StampMitra;

namespace StampMitra.Tests;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string? Body)
{
    public CancellationToken Ct { get; init; }
}

/// <summary>An HttpMessageHandler that replays scripted responses and records every request.</summary>
internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, int, Task<HttpResponseMessage>> _respond;

    public FakeHandler(Func<RecordedRequest, int, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public List<RecordedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in request.Headers)
        {
            headers[h.Key] = string.Join(h.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) ? " " : ", ", h.Value);
        }

        string? body = null;
        if (request.Content is not null)
        {
            foreach (var h in request.Content.Headers)
            {
                headers[h.Key] = string.Join(", ", h.Value);
            }

            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        var rec = new RecordedRequest(request.Method, request.RequestUri!, headers, body) { Ct = cancellationToken };
        Requests.Add(rec);
        return await _respond(rec, Requests.Count);
    }

    public static HttpResponseMessage Json(int status, string json, params (string, string)[] headers)
    {
        var r = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var (k, v) in headers)
        {
            r.Headers.TryAddWithoutValidation(k, v);
        }

        return r;
    }

    public static HttpResponseMessage Text(int status, string text, string mediaType = "text/html") =>
        new((HttpStatusCode)status) { Content = new StringContent(text, Encoding.UTF8, mediaType) };

    private static readonly JsonSerializerOptions Opts = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static string Error(string code, string message, string requestId = "req_test_1", string? field = null) =>
        JsonSerializer.Serialize(new { error = new { code, message, requestId, field } }, Opts);
}

internal static class Tc
{
    public const string Key = "sk_test_abcdefgh12345678";

    public static (StampMitraClient Client, FakeHandler Handler, List<TimeSpan> Delays) Make(Func<RecordedRequest, int, Task<HttpResponseMessage>> respond, Action<StampMitraClientOptions>? configure = null)
    {
        var handler = new FakeHandler(respond);
        var delays = new List<TimeSpan>();
        var options = new StampMitraClientOptions
        {
            ApiKey = Key,
            HttpClient = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan },
            DelayAsync = (d, _) =>
            {
                delays.Add(d);
                return Task.CompletedTask;
            },
        };
        configure?.Invoke(options);
        return (new StampMitraClient(options), handler, delays);
    }

    /// <summary>Replays the given responses in order (the last one repeats); each call gets a fresh copy.</summary>
    public static (StampMitraClient Client, FakeHandler Handler, List<TimeSpan> Delays) Make(params HttpResponseMessage[] sequence)
    {
        var snapshots = sequence.Select(r => (Status: (int)r.StatusCode, Body: r.Content.ReadAsStringAsync().GetAwaiter().GetResult(), Type: r.Content.Headers.ContentType?.MediaType ?? "application/json", Headers: r.Headers.Select(h => (h.Key, string.Join(", ", h.Value))).ToArray())).ToArray();
        return Make((_, n) =>
        {
            var s = snapshots[Math.Min(n, snapshots.Length) - 1];
            var copy = new HttpResponseMessage((System.Net.HttpStatusCode)s.Status) { Content = new StringContent(s.Body, System.Text.Encoding.UTF8, s.Type) };
            foreach (var (k, v) in s.Headers)
            {
                copy.Headers.TryAddWithoutValidation(k, v);
            }

            return Task.FromResult(copy);
        });
    }

    public static string Fixture(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory, "fixtures" }.Concat(parts).ToArray()));
}
