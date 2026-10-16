using System.Net.Http;
using System.Text.RegularExpressions;
using StampMitra.Models;

namespace StampMitra.Tests;

public sealed class ErrorMappingTests
{
    public static IEnumerable<object[]> Statuses() => new[]
    {
        new object[] { 400, typeof(ValidationException) },
        new object[] { 401, typeof(AuthenticationException) },
        new object[] { 402, typeof(PaymentRequiredException) },
        new object[] { 403, typeof(PermissionDeniedException) },
        new object[] { 404, typeof(NotFoundException) },
        new object[] { 409, typeof(ConflictException) },
        new object[] { 429, typeof(RateLimitException) },
        new object[] { 500, typeof(UpstreamException) },
        new object[] { 502, typeof(UpstreamException) },
        new object[] { 503, typeof(UpstreamException) },
        new object[] { 504, typeof(UpstreamException) },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task MapsStatusToTypedException(int status, Type type)
    {
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(status, FakeHandler.Error("SOME_CODE", "Server said no", "req_77", "pan"))), o => o.MaxRetries = 0);
        var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => c.Usage.SummaryAsync());
        Assert.IsType(type, ex);
        Assert.Equal(status, ex.Status);
        Assert.Equal("SOME_CODE", ex.Code);
        Assert.Equal("Server said no", ex.Message);
        Assert.Equal("req_77", ex.RequestId);
        Assert.Equal("pan", ex.Field);
        Assert.Contains("Server said no", ex.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(Tc.Key, ex.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(502, typeof(UpstreamException))]
    [InlineData(429, typeof(RateLimitException))]
    [InlineData(404, typeof(NotFoundException))]
    [InlineData(418, typeof(StampMitraException))]
    public async Task NonJsonErrorBodyStillMapsByStatus(int status, Type type)
    {
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Text(status, "<html>Bad Gateway</html>")), o => o.MaxRetries = 0);
        var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => c.Usage.SummaryAsync());
        Assert.Equal(type, ex.GetType());
        Assert.Equal(status, ex.Status);
        Assert.Equal($"HTTP {status}", ex.Message);
    }

    [Fact]
    public async Task EmptyAndJsonButNotErrorShapedBodies()
    {
        var (c, _, _) = Tc.Make((_, n) => Task.FromResult(n == 1 ? FakeHandler.Text(404, string.Empty) : FakeHandler.Json(404, """["x"]""")), o => o.MaxRetries = 0);
        Assert.IsType<NotFoundException>(await Assert.ThrowsAnyAsync<StampMitraException>(() => c.Usage.SummaryAsync()));
        Assert.IsType<NotFoundException>(await Assert.ThrowsAnyAsync<StampMitraException>(() => c.Usage.SummaryAsync()));
    }

    [Fact]
    public async Task PermissionMessageIsVerbatim()
    {
        var msg = "This endpoint requires an IP-allowlisted API key. Allowlist your server IP in the dashboard.";
        var (c, _, _) = Tc.Make(FakeHandler.Json(403, FakeHandler.Error("FORBIDDEN", msg)));
        var ex = await Assert.ThrowsAsync<PermissionDeniedException>(() => c.Verify.PassportAsync(new PassportVerifyRequest { FileNumber = "PA1079341954215", Dob = "1990-01-31" }));
        Assert.Equal(msg, ex.Message);
    }

    [Fact]
    public async Task RateLimitCarriesRetryAfterSecondsAndHttpDates()
    {
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(429, FakeHandler.Error("TOO_MANY_REQUESTS", "slow down"), ("Retry-After", "7"))), o => o.MaxRetries = 0);
        var ex = await Assert.ThrowsAsync<RateLimitException>(() => c.Usage.SummaryAsync());
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);

        var (c2, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(429, "{}")), o => o.MaxRetries = 0);
        Assert.Null((await Assert.ThrowsAsync<RateLimitException>(() => c2.Usage.SummaryAsync())).RetryAfter);

        var date = DateTimeOffset.UtcNow.AddSeconds(30).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var (c3, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(429, "{}", ("Retry-After", date))), o => o.MaxRetries = 0);
        var ra = (await Assert.ThrowsAsync<RateLimitException>(() => c3.Usage.SummaryAsync())).RetryAfter!.Value;
        Assert.InRange(ra.TotalSeconds, 20, 31);
    }

    [Fact]
    public async Task RequestIdFallsBackToHeader()
    {
        var (c, _, _) = Tc.Make(FakeHandler.Json(500, """{"error":{"code":"INTERNAL_ERROR","message":"boom"}}""", ("x-request-id", "hdr_1")));
        var ex = await Assert.ThrowsAsync<UpstreamException>(() => c.Usage.SummaryAsync());
        Assert.Equal("hdr_1", ex.RequestId);
    }
}

public sealed class RetryTests
{
    private static readonly Regex Uuid4 = new("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$");

    private static HttpResponseMessage Err(int status, params (string, string)[] h) => FakeHandler.Json(status, FakeHandler.Error("E", "e"), h);

    private static HttpResponseMessage Ok() => FakeHandler.Json(200, """{"externalOrderId":"o1","status":"ok"}""");

    [Fact]
    public async Task GetIsRetriedOnTransientStatusesWithBackoff()
    {
        var seq = new[] { Err(503), Err(502), Ok() };
        var (c, h, delays) = Tc.Make((_, n) => Task.FromResult(seq[n - 1]));
        var r = await c.Orders.GetStatusAsync("o1");
        Assert.Equal(3, h.Requests.Count);
        Assert.Equal(3, r.Response!.Attempts);
        Assert.Equal(2, delays.Count);
        Assert.InRange(delays[0].TotalSeconds, 0, 0.5);
        Assert.InRange(delays[1].TotalSeconds, 0, 1.0);
    }

    [Fact]
    public async Task BackoffCapsAtEightSecondsAndIsJittered()
    {
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            Assert.InRange(Transport.Backoff(attempt).TotalSeconds, 0, 8);
        }

        var samples = Enumerable.Range(0, 50).Select(_ => Transport.Backoff(5).TotalMilliseconds).Distinct().Count();
        Assert.True(samples > 10);
        var (c, h, delays) = Tc.Make((_, n) => Task.FromResult(n <= 6 ? Err(503) : Ok()), o => o.MaxRetries = 6);
        await c.Orders.GetStatusAsync("o1");
        Assert.Equal(7, h.Requests.Count);
        Assert.All(delays, d => Assert.InRange(d.TotalSeconds, 0, 8));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    public async Task ClientErrorsAndPlain500AreNotRetried(int status)
    {
        var (c, h, delays) = Tc.Make(Err(status), Ok());
        await Assert.ThrowsAnyAsync<StampMitraException>(() => c.Orders.GetStatusAsync("o1"));
        Assert.Single(h.Requests);
        Assert.Empty(delays);
    }

    [Fact]
    public async Task ExhaustionThrowsLastErrorAfterMaxRetriesPlusOneAttempts()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(Err(503)));
        var ex = await Assert.ThrowsAsync<UpstreamException>(() => c.Orders.GetStatusAsync("o1"));
        Assert.Equal(503, ex.Status);
        Assert.Equal(3, h.Requests.Count);

        var (c0, h0, _) = Tc.Make((_, _) => Task.FromResult(Err(503)), o => o.MaxRetries = 0);
        await Assert.ThrowsAsync<UpstreamException>(() => c0.Orders.GetStatusAsync("o1"));
        Assert.Single(h0.Requests);

        var (c5, h5, _) = Tc.Make((_, _) => Task.FromResult(Err(503)));
        await Assert.ThrowsAsync<UpstreamException>(() => c5.Orders.GetStatusAsync("o1", new RequestOptions { MaxRetries = 5 }));
        Assert.Equal(6, h5.Requests.Count);
    }

    [Fact]
    public async Task RetryAfterHeaderOverridesBackoffAndIsCapped()
    {
        var (c, _, delays) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(429, ("Retry-After", "2")) : n == 2 ? Err(429, ("Retry-After", "3600")) : Ok()));
        await c.Orders.GetStatusAsync("o1");
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60) }, delays);
    }

    [Fact]
    public async Task RateLimitWithoutRetryAfterUsesComputedBackoff()
    {
        var (c, h, delays) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(429) : Ok()));
        await c.Orders.GetStatusAsync("o1");
        Assert.Equal(2, h.Requests.Count);
        Assert.InRange(delays.Single().TotalSeconds, 0, 0.5);
    }

    [Fact]
    public async Task PostsGetAnAutoKeyReusedAcrossRetries()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n < 3 ? Err(n == 1 ? 502 : 503) : FakeHandler.Json(201, """{"externalOrderId":"o1","createdAt":"x"}""")));
        var r = await c.Orders.CreateAsync(new CreateOrderRequest { ExternalOrderId = "o1", ExternalCustomerId = "c1", ServiceCode = "s" });
        Assert.Equal("o1", r.ExternalOrderId);
        var keys = h.Requests.Select(x => x.Headers["Idempotency-Key"]).Distinct().ToList();
        Assert.Equal(3, h.Requests.Count);
        Assert.Single(keys);
        Assert.Matches(Uuid4, keys[0]);
    }

    [Fact]
    public async Task EachCallGetsAFreshKey()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, "{}")));
        for (var i = 0; i < 20; i++)
        {
            await c.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F" });
        }

        Assert.Equal(20, h.Requests.Select(x => x.Headers["Idempotency-Key"]).Distinct().Count());
    }

    [Fact]
    public async Task CallerKeyIsUsedAndReused()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(503) : FakeHandler.Json(200, "{}")));
        await c.Verify.GstinAsync(new GstinVerifyRequest { Gstin = "27ABCDE1234F1Z5" }, new RequestOptions { IdempotencyKey = "mine-1" });
        Assert.Equal(new[] { "mine-1", "mine-1" }, h.Requests.Select(x => x.Headers["Idempotency-Key"]).ToArray());
    }

    [Fact]
    public async Task AllPostsExceptReplayAutoKeyAndGetsNeverDo()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, "{}")));
        await c.Estamp.CalculateDutyAsync(new EstampDutyCalculateRequest { StateCode = "MH", ArticleCode = "5", StampDutyAmount = 100 });
        await c.Estamp.AddPartiesAsync("o1", new EstampPartiesRequest { PartyName = "B" });
        await c.Payments.Intents.CreateAsync(new CreatePaymentIntentRequest { ExternalOrderId = "o", AmountMinor = 100, Currency = "INR", PaymentConnectionId = "pc" });
        await c.Webhooks.Events.ReplayAsync("evt_1");
        await c.Usage.SummaryAsync();
        Assert.All(h.Requests.Take(3), r => Assert.True(r.Headers.ContainsKey("Idempotency-Key")));
        Assert.False(h.Requests[3].Headers.ContainsKey("Idempotency-Key"));
        Assert.False(h.Requests[4].Headers.ContainsKey("Idempotency-Key"));
        Assert.Equal("{}", h.Requests[3].Body);
    }

    [Fact]
    public async Task ReplayWithoutKeyIsNeverRetriedButWithKeyIs()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n <= 2 ? Err(503) : FakeHandler.Json(200, "{}")));
        await Assert.ThrowsAsync<UpstreamException>(() => c.Webhooks.Events.ReplayAsync("evt_1"));
        Assert.Single(h.Requests);
        await c.Webhooks.Events.ReplayAsync("evt_1", new RequestOptions { IdempotencyKey = "k1" });
        Assert.Equal(3, h.Requests.Count);
    }

    [Fact]
    public async Task ConnectionErrorsAreRetriedThenSurfaceWithoutTheKey()
    {
        var (c, h, delays) = Tc.Make((_, _) => throw new HttpRequestException("Connection refused to 127.0.0.1:1"));
        var ex = await Assert.ThrowsAsync<ApiConnectionException>(() => c.Orders.GetStatusAsync("o1"));
        Assert.IsNotType<ApiTimeoutException>(ex);
        Assert.Equal(3, h.Requests.Count);
        Assert.Equal(2, delays.Count);
        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.DoesNotContain(Tc.Key, ex.ToString(), StringComparison.Ordinal);
        Assert.Null(ex.Status);
    }

    [Fact]
    public async Task ConnectionErrorThenSuccess()
    {
        var (c, h, _) = Tc.Make((_, n) => n == 1 ? throw new HttpRequestException("reset") : Task.FromResult(Ok()));
        await c.Orders.GetStatusAsync("o1");
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task TimeoutRaisesApiTimeoutExceptionAndIsRetried()
    {
        var (c, h, _) = Tc.Make(async (r, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), r.Ct);
            return Ok();
        }, o => o.MaxRetries = 1);
        var ex = await Assert.ThrowsAsync<ApiTimeoutException>(() => c.Orders.GetStatusAsync("o1", new RequestOptions { Timeout = TimeSpan.FromMilliseconds(100) }));
        Assert.IsAssignableFrom<ApiConnectionException>(ex);
        Assert.Equal(2, h.Requests.Count);
    }

    [Fact]
    public async Task CallerCancellationIsNotWrappedAndNotRetried()
    {
        using var cts = new CancellationTokenSource();
        var (c, h, _) = Tc.Make(async (r, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), r.Ct);
            return Ok();
        });
        var task = c.Orders.GetStatusAsync("o1", cts.Token);
        cts.CancelAfter(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task CancellationDuringRetryDelayStopsRetrying()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler((_, _) => Task.FromResult(Err(503)));
        var c = new StampMitraClient(new StampMitraClientOptions
        {
            ApiKey = Tc.Key,
            HttpClient = new HttpClient(handler),
            DelayAsync = (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => c.Orders.GetStatusAsync("o1", cts.Token));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PerCallTimeoutOverridesDefault()
    {
        var (c, _, _) = Tc.Make(async (r, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), r.Ct);
            return Ok();
        }, o =>
        {
            o.Timeout = TimeSpan.FromSeconds(60);
            o.MaxRetries = 0;
        });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<ApiTimeoutException>(() => c.Usage.SummaryAsync(new RequestOptions { Timeout = TimeSpan.FromMilliseconds(150) }));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10));
    }
}
