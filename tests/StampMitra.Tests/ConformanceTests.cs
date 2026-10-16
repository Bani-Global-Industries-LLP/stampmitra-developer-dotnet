using System.Text.Json;
using System.Text.RegularExpressions;
using StampMitra.Resources;
using StampMitra.Webhooks;

namespace StampMitra.Tests;

/// <summary>
/// Black-box conformance run against the spec repo's mock server (scripts/mock-server.mjs) and a tiny real HTTP server.
/// All tests share one mock process, so they run serially (one xUnit collection).
/// </summary>
[Collection("mock")]
[Trait("Category", "Conformance")]
public sealed class ConformanceTests
{
    private static readonly Regex Uuid4 = new("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$");
    private readonly MockServerFixture _mock;
    private readonly List<TimeSpan> _delays = new();

    public ConformanceTests(MockServerFixture mock)
    {
        _mock = mock;
    }

    private void Ready()
    {
        Skip.If(_mock.SkipReason is not null, _mock.SkipReason);
        _mock.Reset();
        _delays.Clear();
    }

    private StampMitraClient Client(Action<StampMitraClientOptions>? configure = null, string? baseUrl = null)
    {
        var o = new StampMitraClientOptions
        {
            ApiKey = Tc.Key,
            BaseUrl = baseUrl ?? _mock.BaseUrl,
            DelayAsync = (d, _) =>
            {
                _delays.Add(d);
                return Task.CompletedTask;
            },
        };
        configure?.Invoke(o);
        return new StampMitraClient(o);
    }

    private static RequestOptions Scenario(string s, string? key = null) =>
        new() { Headers = new Dictionary<string, string> { ["x-mock-scenario"] = s }, IdempotencyKey = key };

    // ---- the 31 operations -------------------------------------------------

    public static IEnumerable<object[]> Operations()
    {
        var order = new CreateOrderRequest { ExternalOrderId = "o1", ExternalCustomerId = "c1", ServiceCode = "estamp.instrument.rent_agreement" };
        var ops = new List<(string Name, Func<StampMitraClient, Task<object>> Call, string Method, string Path)>
        {
            ("verify.pan", async c => await c.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F", Name = "Rahul Kumar" }), "POST", "/v1/verify/pan"),
            ("verify.gstin", async c => await c.Verify.GstinAsync(new GstinVerifyRequest { Gstin = "27ABCDE1234F1Z5" }), "POST", "/v1/verify/gstin"),
            ("verify.rc", async c => await c.Verify.RcAsync(new VehicleRcVerifyRequest { VehicleNumber = "MH12AB1234" }), "POST", "/v1/verify/rc"),
            ("verify.voterId", async c => await c.Verify.VoterIdAsync(new VoterIdVerifyRequest { VoterId = "ABC1234567" }), "POST", "/v1/verify/voter-id"),
            ("verify.drivingLicense", async c => await c.Verify.DrivingLicenseAsync(new DrivingLicenseVerifyRequest { DlNumber = "MH1420110012345", Dob = "1990-01-31" }), "POST", "/v1/verify/driving-license"),
            ("verify.passport", async c => await c.Verify.PassportAsync(new PassportVerifyRequest { FileNumber = "PA1079341954215", Dob = "1990-01-31" }), "POST", "/v1/verify/passport"),
            ("verify.cin", async c => await c.Verify.CinAsync(new CinVerifyRequest { Cin = "U74999DL2015PTC284009" }), "POST", "/v1/verify/cin"),
            ("verify.udyam", async c => await c.Verify.UdyamAsync(new UdyamVerifyRequest { UdyamNumber = "UDYAM-MH-12-1234567" }), "POST", "/v1/verify/udyam"),
            ("verify.panToGstin", async c => await c.Verify.PanToGstinAsync(new PanToGstinRequest { Pan = "ABCDE1234F" }), "POST", "/v1/verify/pan-to-gstin"),
            ("verify.panToUdyam", async c => await c.Verify.PanToUdyamAsync(new PanToUdyamRequest { Pan = "ABCDE1234F" }), "POST", "/v1/verify/pan-to-udyam"),
            ("verify.bankAccount", async c => await c.Verify.BankAccountAsync(new BankAccountVerifyRequest { AccountNumber = "000123456789", Ifsc = "ICIC0001234", Phone = "9876543210" }), "POST", "/v1/verify/bank-account"),
            ("services.list", async c => await c.Services.ListAsync(), "GET", "/v1/services"),
            ("services.get", async c => await c.Services.GetAsync("estamp.instrument.rent_agreement"), "GET", "/v1/services/estamp.instrument.rent_agreement"),
            ("estamp.getSchema", async c => await c.Estamp.GetSchemaAsync("estamp.instrument.rent_agreement"), "GET", "/v1/estamp/schema/estamp.instrument.rent_agreement"),
            ("estamp.listStates", async c => await c.Estamp.ListStatesAsync(), "GET", "/v1/estamp/states"),
            ("estamp.listInstruments", async c => await c.Estamp.ListInstrumentsAsync("MH"), "GET", "/v1/estamp/states/MH/instruments"),
            ("estamp.listArticles", async c => await c.Estamp.ListArticlesAsync("rent_agreement"), "GET", "/v1/estamp/instruments/rent_agreement/articles"),
            ("estamp.calculateDuty", async c => await c.Estamp.CalculateDutyAsync(new EstampDutyCalculateRequest { StateCode = "MH", ArticleCode = "5", StampDutyAmount = 500 }), "POST", "/v1/estamp/duty/calculate"),
            ("estamp.addParties", async c => await c.Estamp.AddPartiesAsync("ord_123", new EstampPartiesRequest { CustomerName = "A", PartyName = "B" }), "POST", "/v1/estamp/orders/ord_123/parties"),
            ("orders.create", async c => await c.Orders.CreateAsync(order), "POST", "/v1/orders"),
            ("orders.get", async c => await c.Orders.GetAsync("o1"), "GET", "/v1/orders/o1"),
            ("orders.getStatus", async c => await c.Orders.GetStatusAsync("o1"), "GET", "/v1/orders/o1/status"),
            ("orders.getPayment", async c => await c.Orders.GetPaymentAsync("o1"), "GET", "/v1/orders/o1/payment"),
            ("orders.listDocuments", async c => await c.Orders.ListDocumentsAsync("o1"), "GET", "/v1/orders/o1/documents"),
            ("orders.getDocumentDownload", async c => await c.Orders.GetDocumentDownloadAsync("o1", "doc_1"), "GET", "/v1/orders/o1/documents/doc_1/download"),
            ("payments.intents.create", async c => await c.Payments.Intents.CreateAsync(new CreatePaymentIntentRequest { ExternalOrderId = "o1", AmountMinor = 50000, Currency = "INR", PaymentConnectionId = "pc_1" }), "POST", "/v1/payments/intents"),
            ("payments.intents.get", async c => await c.Payments.Intents.GetAsync("pi_1"), "GET", "/v1/payments/intents/pi_1"),
            ("usage.summary", async c => await c.Usage.SummaryAsync(), "GET", "/v1/usage/summary"),
            ("logs.list", async c => await c.Logs.ListAsync(new LogsListParams { Limit = 50 }), "GET", "/v1/logs"),
            ("webhooks.events.list", async c => await c.Webhooks.Events.ListAsync(20), "GET", "/v1/webhooks/events"),
            ("webhooks.events.replay", async c => await c.Webhooks.Events.ReplayAsync("evt_1"), "POST", "/v1/webhooks/events/evt_1/replay"),
        };
        foreach (var (name, call, method, path) in ops)
        {
            yield return new object[] { name, new OpCase(call, method, path) };
        }
    }

    public sealed record OpCase(Func<StampMitraClient, Task<object>> Call, string Method, string Path)
    {
        public override string ToString() => Method + " " + Path;
    }

    [Fact]
    public void ThereAreExactlyThirtyOneOperations() => Assert.Equal(31, Operations().Count());

    [SkippableTheory]
    [MemberData(nameof(Operations))]
    public async Task EveryOperationHitsTheRightRouteAndSucceeds(string name, OpCase op)
    {
        Ready();
        using var client = Client();
        var result = await op.Call(client);
        Assert.NotNull(result);
        Assert.NotNull(((Models.ApiModel)result).Response);
        Assert.InRange(((Models.ApiModel)result).Response!.StatusCode, 200, 299);

        var seen = _mock.Recorded();
        var req = Assert.Single(seen);
        Assert.Equal(op.Method, req.GetProperty("method").GetString());
        Assert.Equal(op.Path, req.GetProperty("path").GetString());
        var headers = req.GetProperty("headers");
        Assert.Equal("Bearer " + Tc.Key, headers.GetProperty("authorization").GetString());
        Assert.StartsWith("stampmitra-dotnet/", headers.GetProperty("user-agent").GetString(), StringComparison.Ordinal);
        if (op.Method == "POST" && !op.Path.EndsWith("/replay", StringComparison.Ordinal))
        {
            Assert.Matches(Uuid4, headers.GetProperty("idempotency-key").GetString()!);
        }
        else
        {
            Assert.False(headers.TryGetProperty("idempotency-key", out _), name);
        }
    }

    // ---- error mapping -------------------------------------------------------

    public static IEnumerable<object[]> ErrorStatuses() => new[]
    {
        new object[] { 400, typeof(ValidationException) }, new object[] { 401, typeof(AuthenticationException) },
        new object[] { 402, typeof(PaymentRequiredException) }, new object[] { 403, typeof(PermissionDeniedException) },
        new object[] { 404, typeof(NotFoundException) }, new object[] { 409, typeof(ConflictException) },
        new object[] { 429, typeof(RateLimitException) }, new object[] { 500, typeof(UpstreamException) },
        new object[] { 502, typeof(UpstreamException) }, new object[] { 503, typeof(UpstreamException) },
        new object[] { 504, typeof(UpstreamException) },
    };

    [SkippableTheory]
    [MemberData(nameof(ErrorStatuses))]
    public async Task ErrorStatusMapping(int status, Type type)
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 0);
        var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => client.Usage.SummaryAsync(Scenario("error-" + status)));
        Assert.IsType(type, ex);
        Assert.Equal(status, ex.Status);
        Assert.NotNull(ex.Code);
        Assert.False(string.IsNullOrEmpty(ex.RequestId));
        Assert.DoesNotContain(Tc.Key, ex.ToString(), StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task RateLimitWithRetryAfterHeader()
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 0);
        var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.Usage.SummaryAsync(Scenario("retry-after:7")));
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
    }

    [SkippableFact]
    public async Task RetryAfterIsHonouredAndOverridesBackoff()
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 1);
        await Assert.ThrowsAsync<RateLimitException>(() => client.Usage.SummaryAsync(Scenario("retry-after:3")));
        Assert.Equal(new[] { TimeSpan.FromSeconds(3) }, _delays);
        Assert.Equal(2, _mock.Recorded().Count);
    }

    [SkippableFact]
    public async Task RateLimitWithoutRetryAfterHeaderUsesBackoff()
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 1);
        // GET /v1/estamp/states documents a 429 that carries no Retry-After (the real API sends none).
        var ex = await Assert.ThrowsAsync<RateLimitException>(() => client.Estamp.ListStatesAsync(Scenario("error-429")));
        Assert.Null(ex.RetryAfter);
        var delay = Assert.Single(_delays);
        Assert.InRange(delay.TotalSeconds, 0, 0.5);
    }

    // ---- shared retry-scenarios.json ---------------------------------------

    private static readonly Dictionary<string, Type> Outcomes = new()
    {
        ["UpstreamError"] = typeof(UpstreamException),
        ["RateLimitError"] = typeof(RateLimitException),
        ["NotFoundError"] = typeof(NotFoundException),
        ["ValidationError"] = typeof(ValidationException),
        ["AuthenticationError"] = typeof(AuthenticationException),
        ["PermissionError"] = typeof(PermissionDeniedException),
        ["ConflictError"] = typeof(ConflictException),
        ["TimeoutError"] = typeof(ApiTimeoutException),
    };

    public static IEnumerable<object[]> RetryScenarios()
    {
        using var doc = JsonDocument.Parse(Tc.Fixture("retry-scenarios.json"));
        foreach (var s in doc.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            yield return new object[] { s.GetProperty("name").GetString()! };
        }
    }

    [SkippableTheory]
    [MemberData(nameof(RetryScenarios))]
    public async Task SharedRetryScenario(string name)
    {
        using var doc = JsonDocument.Parse(Tc.Fixture("retry-scenarios.json"));
        var s = doc.RootElement.GetProperty("scenarios").EnumerateArray().First(x => x.GetProperty("name").GetString() == name);
        Ready();

        // The mock's seq: scenarios cannot set response headers, so Retry-After values from sequence items that carry
        // headers are asserted in RetryAfterIsHonouredAndOverridesBackoff and the unit tests instead.
        var headerItems = false;
        var items = new List<string>();
        foreach (var item in s.GetProperty("sequence").EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() == "timeout")
            {
                Skip.If(true, "client-side timeout step: covered by the Timeout* tests");
            }

            items.Add(item.ValueKind switch
            {
                JsonValueKind.String => "conn",
                JsonValueKind.Number => item.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => item.GetProperty("status").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
            headerItems |= item.ValueKind == JsonValueKind.Object && item.TryGetProperty("headers", out _);
        }

        Skip.If(s.TryGetProperty("optional", out var opt) && opt.GetBoolean(), "optional scenario");

        var maxRetries = s.TryGetProperty("maxRetries", out var mr) ? mr.GetInt32() : 2;
        var idem = s.GetProperty("idempotencyKey");
        var key = idem.ValueKind == JsonValueKind.True ? "caller-key-1" : null;
        var auto = idem.ValueKind == JsonValueKind.String;
        var path = s.GetProperty("path").GetString()!;
        var method = new HttpMethod(s.GetProperty("method").GetString()!);
        using var client = Client(o => o.MaxRetries = maxRetries);
        var outcome = s.GetProperty("outcome");

        StampMitraException? thrown = null;
        int? status = null;
        try
        {
            var r = await client.Transport.SendAsync(method, path, null, null, method == HttpMethod.Get ? null : new { }, Scenario($"seq:k{Guid.NewGuid():N}:" + string.Join(",", items), key), auto, default);
            status = r.Status;
        }
        catch (StampMitraException ex)
        {
            thrown = ex;
        }

        if (outcome.ValueKind == JsonValueKind.Number)
        {
            Assert.Null(thrown);
            Assert.InRange(status!.Value, 200, 299);
        }
        else
        {
            var text = outcome.GetString()!;
            Assert.NotNull(thrown);
            var m = Regex.Match(text, @"^(\w+)(?:\((.*)\))?$");
            Assert.Equal(Outcomes[m.Groups[1].Value], thrown.GetType());
            if (m.Groups[2].Success)
            {
                var arg = m.Groups[2].Value;
                if (arg.StartsWith("retryAfter=", StringComparison.Ordinal))
                {
                    if (!headerItems)
                    {
                        Assert.Equal(TimeSpan.FromSeconds(double.Parse(arg["retryAfter=".Length..], System.Globalization.CultureInfo.InvariantCulture)), ((RateLimitException)thrown).RetryAfter);
                    }
                }
                else
                {
                    Assert.Equal(int.Parse(arg, System.Globalization.CultureInfo.InvariantCulture), thrown.Status);
                }
            }
        }

        var mine = _mock.Recorded().Where(r => r.GetProperty("path").GetString() == path).ToList();
        Assert.Equal(s.GetProperty("expectedAttempts").GetInt32(), mine.Count);
        if (s.TryGetProperty("sameIdempotencyKeyAcrossAttempts", out var same) && same.GetBoolean())
        {
            var keys = mine.Select(r => r.GetProperty("headers").GetProperty("idempotency-key").GetString()).Distinct().ToList();
            Assert.Single(keys);
            Assert.False(string.IsNullOrEmpty(keys[0]));
        }

        if (idem.ValueKind == JsonValueKind.False)
        {
            Assert.All(mine, r => Assert.False(r.GetProperty("headers").TryGetProperty("idempotency-key", out _)));
        }

        if (!headerItems && s.TryGetProperty("expectedDelayBeforeAttempt2Seconds", out var d))
        {
            Assert.Equal(TimeSpan.FromSeconds(d.GetDouble()), _delays[0]);
        }
    }

    [SkippableFact]
    public async Task IdempotencyKeyReusedAcrossRetriesViaResourceMethods()
    {
        Ready();
        using var client = Client();
        await client.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F" }, Scenario("seq:idem:503,502,200"));
        var reqs = _mock.Recorded();
        Assert.Equal(3, reqs.Count);
        var keys = reqs.Select(r => r.GetProperty("headers").GetProperty("idempotency-key").GetString()).Distinct().ToList();
        Assert.Single(keys);
        Assert.Matches(Uuid4, keys[0]!);

        _mock.Reset();
        await client.Orders.CreateAsync(new CreateOrderRequest { ExternalOrderId = "o", ExternalCustomerId = "c", ServiceCode = "s" }, Scenario("seq:idem2:503,200", "mine-1"));
        reqs = _mock.Recorded();
        Assert.Equal(2, reqs.Count);
        Assert.All(reqs, r => Assert.Equal("mine-1", r.GetProperty("headers").GetProperty("idempotency-key").GetString()));
    }

    [SkippableFact]
    public async Task NoRetryOn4xxAndRetryExhaustion()
    {
        Ready();
        using var client = Client();
        await Assert.ThrowsAsync<NotFoundException>(() => client.Orders.GetAsync("missing", Scenario("seq:nf:404,200")));
        Assert.Single(_mock.Recorded());

        _mock.Reset();
        var ex = await Assert.ThrowsAsync<UpstreamException>(() => client.Orders.GetAsync("o1", Scenario("seq:ex:503")));
        Assert.Equal(503, ex.Status);
        Assert.Equal(3, _mock.Recorded().Count);
        Assert.Equal(2, _delays.Count);
    }

    // ---- transport failures ----------------------------------------------------

    [SkippableFact]
    public async Task TimeoutRaisesApiTimeoutExceptionWithPerCallTimeout()
    {
        Ready();
        using var client = Client(o =>
        {
            o.MaxRetries = 0;
            o.Timeout = TimeSpan.FromSeconds(30);
        });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<ApiTimeoutException>(() => client.Usage.SummaryAsync(new RequestOptions { Timeout = TimeSpan.FromMilliseconds(300), Headers = new Dictionary<string, string> { ["x-mock-scenario"] = "delay:2000" } }));
        Assert.IsAssignableFrom<ApiConnectionException>(ex);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1.8), "per-call timeout must override the 30s default");
    }

    [SkippableFact]
    public async Task TimeoutIsRetriedForGetAndForPostsWithAKeyButNotForKeylessPosts()
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 1);
        var slow = new RequestOptions { Timeout = TimeSpan.FromMilliseconds(200), Headers = new Dictionary<string, string> { ["x-mock-scenario"] = "delay:1200" } };
        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.Usage.SummaryAsync(slow));
        Assert.Equal(2, _mock.Recorded().Count);

        _mock.Reset();
        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.Orders.CreateAsync(new CreateOrderRequest { ExternalOrderId = "o", ExternalCustomerId = "c", ServiceCode = "s" }, slow));
        var posts = _mock.Recorded();
        Assert.Equal(2, posts.Count);
        Assert.Single(posts.Select(r => r.GetProperty("headers").GetProperty("idempotency-key").GetString()).Distinct());

        _mock.Reset();
        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.Webhooks.Events.ReplayAsync("evt_1", slow));
        Assert.Single(_mock.Recorded());
    }

    [SkippableFact]
    public async Task ConnectionRefusedRaisesApiConnectionException()
    {
        Ready();
        using var client = Client(o => o.MaxRetries = 1, "http://127.0.0.1:1");
        var ex = await Assert.ThrowsAsync<ApiConnectionException>(() => client.Usage.SummaryAsync());
        Assert.IsNotType<ApiTimeoutException>(ex);
        Assert.Single(_delays);
        Assert.DoesNotContain(Tc.Key, ex.ToString(), StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task ConnectionDroppedMidRequestIsRetriedForGet()
    {
        Ready();
        using var client = Client();
        await client.Usage.SummaryAsync(Scenario("seq:drop:conn,200"));
        Assert.Equal(2, _mock.Recorded().Count);
    }

    // ---- bodies the mock cannot produce (real HTTP over loopback) ------------------

    [Theory]
    [InlineData(502, typeof(UpstreamException))]
    [InlineData(429, typeof(RateLimitException))]
    [InlineData(404, typeof(NotFoundException))]
    public async Task NonJsonErrorBodyStillMapsByStatus(int status, Type type)
    {
        using var srv = new TinyServer((_, _) => (status, "<html>Bad Gateway</html>", "text/html", Array.Empty<(string, string)>()));
        using var client = Client(o => o.MaxRetries = 0, srv.BaseUrl);
        var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => client.Usage.SummaryAsync());
        Assert.IsType(type, ex);
        Assert.Equal(status, ex.Status);
    }

    [Fact]
    public async Task UnknownExtraResponseFieldsAreTolerated()
    {
        using var srv = new TinyServer((_, _) => (200, """{"referenceId":"SMV-1","valid":true,"brandNewField":{"x":1},"registeredName":"ACME"}""", "application/json", Array.Empty<(string, string)>()));
        using var client = Client(null, srv.BaseUrl);
        var res = await client.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F" });
        Assert.True(res.Valid);
        Assert.Equal(1, res.AdditionalProperties!["brandNewField"].GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task LogsPaginationAcrossThreePagesOverRealHttp()
    {
        using var srv = new TinyServer((req, n) =>
        {
            var next = n == 1 ? "\"c1\"" : n == 2 ? "\"c2\"" : "null";
            return (200, $$"""{"items":[{"id":"log{{n}}","createdAt":"2026-10-15T09:41:07.000Z","service":"pan.verify"}],"nextCursor":{{next}}}""", "application/json", Array.Empty<(string, string)>());
        });
        using var client = Client(null, srv.BaseUrl);
        var ids = new List<string?>();
        await foreach (var e in client.Logs.ListAllAsync(new LogsListParams { Limit = 1 }))
        {
            ids.Add(e.Id);
        }

        Assert.Equal(new[] { "log1", "log2", "log3" }, ids);
        Assert.Equal(3, srv.Count);
    }

    [SkippableFact]
    public async Task LogsAgainstMockFixtureIsASinglePage()
    {
        Ready();
        using var client = Client();
        var ids = new List<string?>();
        await foreach (var e in client.Logs.ListAllAsync(new LogsListParams { Limit = 50 }))
        {
            ids.Add(e.Id);
        }

        Assert.NotEmpty(ids);
        Assert.Single(_mock.Recorded());
    }

    [Fact]
    public async Task RedirectsAreNotFollowedAndCredentialsNeverForwarded()
    {
        using var target = new TinyServer((_, _) => (200, "{}", "application/json", Array.Empty<(string, string)>()));
        using var srv = new TinyServer((_, _) => (302, string.Empty, "text/plain", new[] { ("Location", target.BaseUrl + "/v1/usage/summary") }));
        using var client = Client(o => o.MaxRetries = 0, srv.BaseUrl);
        var ex = await Assert.ThrowsAsync<StampMitraException>(() => client.Usage.SummaryAsync());
        Assert.Equal(302, ex.Status);
        Assert.Equal(0, target.Count);
    }

    // ---- shared fixtures -------------------------------------------------------

    [Fact]
    public void WebhookVectorsAndValidatorVectorsAreVendored()
    {
        using var w = JsonDocument.Parse(Tc.Fixture("webhook-signatures.json"));
        Assert.True(w.RootElement.GetProperty("vectors").GetArrayLength() >= 20);
        using var v = JsonDocument.Parse(Tc.Fixture("validators.json"));
        Assert.Equal(10, v.RootElement.GetProperty("validators").EnumerateObject().Count());
        Assert.True(WebhookSignature.EventTypes.Count == 8);
    }
}
