using System.Net.Http;
using StampMitra.Resources;

namespace StampMitra.Tests;

public sealed class RedactionTests
{
    [Fact]
    public void HeadersAreRedacted()
    {
        var h = Redactor.Headers(new Dictionary<string, string> { ["Authorization"] = "Bearer sk_live_secret", ["X-API-Key"] = "sk_live_secret", ["Accept"] = "application/json", ["Cookie"] = "a=b" });
        Assert.Equal("[REDACTED]", h["authorization"]);
        Assert.Equal("[REDACTED]", h["x-api-key"]);
        Assert.Equal("[REDACTED]", h["cookie"]);
        Assert.Equal("application/json", h["Accept"]);
    }

    [Fact]
    public void BodiesMaskIdentifiersAndRemovePersonalData()
    {
        var masked = Redactor.Body("""{"pan":"ABCDE1234F","gstin":"27ABCDE1234F1Z5","accountNumber":"000123456789","name":"Rahul Kumar","dob":"1990-01-31","nested":{"registeredName":"ACME","phone":"9876543210"},"list":[{"ifsc":"ICIC0001234"}],"valid":true,"referenceId":"SMV-1"}""");
        Assert.Contains("******234F", masked, StringComparison.Ordinal);
        Assert.Contains("\"valid\":true", masked, StringComparison.Ordinal);
        Assert.Contains("SMV-1", masked, StringComparison.Ordinal);
        foreach (var leaked in new[] { "ABCDE1234F", "27ABCDE1234F1Z5", "000123456789", "Rahul", "1990-01-31", "ACME", "9876543210", "ICIC0001234" })
        {
            Assert.DoesNotContain(leaked, masked, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NonJsonBecomesLengthMarker()
    {
        Assert.Equal("[non-JSON body, 5 chars]", Redactor.Body("<html"[..5]));
        Assert.Equal(string.Empty, Redactor.Body(null));
    }
}

public sealed class LoggingTests
{
    [Fact]
    public async Task DebugLogIsOffByDefaultAndRedactedWhenOn()
    {
        var lines = new List<string>();
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, """{"referenceId":"SMV-1","pan":"ABCDE1234F","valid":true,"registeredName":"Rahul Kumar"}""")), o => o.DebugLog = lines.Add);
        await c.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F", Name = "Rahul Kumar" });
        Assert.Equal(2, lines.Count);
        var all = string.Join("\n", lines);
        Assert.Contains("[REDACTED]", all, StringComparison.Ordinal);
        Assert.DoesNotContain(Tc.Key, all, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefgh12345678", all, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCDE1234F", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Rahul", all, StringComparison.Ordinal);
        Assert.Contains("/v1/verify/pan", all, StringComparison.Ordinal);

        // Nothing is logged without a sink (no exception, nothing to assert on): the default path must work.
        var (quiet, _, _) = Tc.Make(FakeHandler.Json(200, "{}"));
        await quiet.Usage.SummaryAsync();
    }

    [Fact]
    public async Task ErrorBodiesAreMaskedInLogs()
    {
        var lines = new List<string>();
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Text(502, "<html>upstream</html>")), o =>
        {
            o.DebugLog = lines.Add;
            o.MaxRetries = 0;
        });
        await Assert.ThrowsAsync<UpstreamException>(() => c.Usage.SummaryAsync());
        Assert.Contains(lines, l => l.Contains("non-JSON body", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains(Tc.Key, StringComparison.Ordinal));
    }
}

public sealed class PaginationTests
{
    private static HttpResponseMessage Page(string id, string? next) =>
        FakeHandler.Json(200, $$"""{"items":[{"id":"{{id}}","createdAt":"2026-10-15T09:41:07.000Z","service":"pan.verify"}],"nextCursor":{{(next is null ? "null" : "\"" + next + "\"")}}}""");

    [Fact]
    public async Task ListAllFollowsThreePagesLazily()
    {
        var pages = new[] { Page("a", "c1"), Page("b", "c2"), Page("c", null) };
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(pages[n - 1]));
        var ids = new List<string?>();
        await foreach (var e in c.Logs.ListAllAsync(new LogsListParams { Limit = 1, Service = "pan.verify", Status = "success" }))
        {
            ids.Add(e.Id);
        }

        Assert.Equal(new[] { "a", "b", "c" }, ids);
        Assert.Equal(3, h.Requests.Count);
        Assert.Null(System.Web.HttpUtility.ParseQueryString(h.Requests[0].Uri.Query)["cursor"]);
        Assert.Equal("c1", System.Web.HttpUtility.ParseQueryString(h.Requests[1].Uri.Query)["cursor"]);
        Assert.Equal("c2", System.Web.HttpUtility.ParseQueryString(h.Requests[2].Uri.Query)["cursor"]);
        Assert.Equal("1", System.Web.HttpUtility.ParseQueryString(h.Requests[2].Uri.Query)["limit"]);
        Assert.Equal("pan.verify", System.Web.HttpUtility.ParseQueryString(h.Requests[2].Uri.Query)["service"]);
    }

    [Fact]
    public async Task ListAllStopsFetchingWhenTheConsumerBreaks()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(Page("a", "more")));
        await foreach (var e in c.Logs.ListAllAsync())
        {
            Assert.Equal("a", e.Id);
            break;
        }

        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task ListAllHonoursAStartingCursorAndCancellation()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(Page("a", null)));
        await foreach (var unused in c.Logs.ListAllAsync(new LogsListParams { Cursor = "start" }))
        {
        }

        Assert.Contains("cursor=start", h.Requests[0].Uri.Query, StringComparison.Ordinal);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var unused in c.Logs.ListAllAsync(null, null, cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task ListAllGuardsAgainstAStuckCursor()
    {
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(Page("a", "same")));
        var ex = await Assert.ThrowsAsync<StampMitraException>(async () =>
        {
            await foreach (var unused in c.Logs.ListAllAsync(new LogsListParams { Cursor = "same" }))
            {
            }
        });
        Assert.Equal("INVALID_RESPONSE", ex.Code);
    }

    [Fact]
    public async Task ListFormatsDatesAsUtcIso()
    {
        var (c, h, _) = Tc.Make(Page("a", null));
        var page = await c.Logs.ListAsync(new LogsListParams { From = new DateTimeOffset(2026, 10, 1, 5, 30, 0, TimeSpan.FromHours(5.5)), To = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero) });
        Assert.Single(page.Items!);
        var q = System.Web.HttpUtility.ParseQueryString(h.Requests[0].Uri.Query);
        Assert.Equal("2026-10-01T00:00:00.000Z", q["from"]);
        Assert.Equal("2026-10-02T00:00:00.000Z", q["to"]);
    }
}
