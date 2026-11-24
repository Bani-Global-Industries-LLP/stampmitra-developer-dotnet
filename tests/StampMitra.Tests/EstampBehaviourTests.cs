using System.Net.Http;
using System.Text.Json;
using StampMitra.Resources;

namespace StampMitra.Tests;

/// <summary>Idempotency, retry, multipart, party-model and error-detail behaviour of the e-Stamp surface (fake handler, no network).</summary>
public sealed class EstampBehaviourTests
{
    private const string OrderJson = """{"id":"estord_1","status":"PENDING_REVIEW","stamps":[{"id":"eststamp_1"}]}""";

    private static HttpResponseMessage Err(int status, string code, params (string, string)[] headers) =>
        FakeHandler.Json(status, FakeHandler.Error(code, "m"), headers);

    private static EstampOrderRequest Order => EstampScenarios.SampleOrder;

    // ---- idempotency key -----------------------------------------------------

    [Fact]
    public async Task CreateOrderGeneratesAnIdempotencyKeyWhenOmittedAndReusesItAcrossRetries()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n < 3 ? Err(503, "SERVICE_UNAVAILABLE") : FakeHandler.Json(201, OrderJson)));
        var order = await c.Estamp.Orders.CreateAsync(Order);
        Assert.Equal("estord_1", order.Id);
        Assert.Equal(3, h.Requests.Count);
        var keys = h.Requests.Select(r => r.Headers["Idempotency-Key"]).Distinct().ToList();
        Assert.Single(keys);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", keys[0]);
    }

    [Fact]
    public async Task EachCreateCallGetsADifferentGeneratedKeyAndACallerKeyWins()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(201, OrderJson)));
        await c.Estamp.Orders.CreateAsync(Order);
        await c.Estamp.Orders.CreateAsync(Order);
        await c.Estamp.Orders.CreateAsync(Order, new RequestOptions { IdempotencyKey = "mine-1" });
        Assert.NotEqual(h.Requests[0].Headers["Idempotency-Key"], h.Requests[1].Headers["Idempotency-Key"]);
        Assert.Equal("mine-1", h.Requests[2].Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task ValidateSendsNoKeyAndIsRetriedOnTransientFailures()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(503, "SERVICE_UNAVAILABLE") : FakeHandler.Json(200, """{"valid":true,"status":"READY_TO_PLACE"}""")));
        var r = await c.Estamp.Orders.ValidateAsync(Order);
        Assert.True(r.Valid);
        Assert.Equal(2, h.Requests.Count);
        Assert.All(h.Requests, q => Assert.False(q.Headers.ContainsKey("Idempotency-Key")));
    }

    // ---- Retry-After -----------------------------------------------------------

    [Theory]
    [InlineData(429, "TOO_MANY_REQUESTS")]
    [InlineData(503, "SERVICE_UNAVAILABLE")]
    public async Task RetryAfterSecondsAreHonouredOnGetsFor429And503(int status, string code)
    {
        var (c, _, delays) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(status, code, ("Retry-After", "2")) : FakeHandler.Json(200, """{"balance":"1.00","currency":"INR"}""")));
        await c.Wallets.StampDuty.GetBalanceAsync();
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, delays);
    }

    [Fact]
    public async Task IdempotencyInProgressIsRetriedAfterRetryAfterWithTheSameKey()
    {
        var (c, h, delays) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(409, "IDEMPOTENCY_IN_PROGRESS", ("Retry-After", "1")) : FakeHandler.Json(201, OrderJson)));
        await c.Estamp.Orders.CreateAsync(Order);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, delays);
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.Requests[0].Headers["Idempotency-Key"], h.Requests[1].Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task AStateConflict409IsNeverRetriedAndExposesRetryAfterWhenSent()
    {
        var (c, h, delays) = Tc.Make((_, _) => Task.FromResult(Err(409, "CONFLICT", ("Retry-After", "5"))));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => c.Estamp.Orders.CreateAsync(Order));
        Assert.Single(h.Requests);
        Assert.Empty(delays);
        Assert.Equal(TimeSpan.FromSeconds(5), ex.RetryAfter);
    }

    [Fact]
    public async Task RetryAfterIsCappedAtSixtySeconds()
    {
        var (c, _, delays) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(429, "TOO_MANY_REQUESTS", ("Retry-After", "600")) : FakeHandler.Json(200, "{}")));
        await c.Account.GetBalanceAsync();
        Assert.Equal(new[] { TimeSpan.FromSeconds(60) }, delays);
    }

    [Fact]
    public async Task CreateOrderTimeoutIsRetriedWithTheSameKey()
    {
        var (c, h, _) = Tc.Make((_, n) => n == 1 ? throw new TaskCanceledException("timeout") : Task.FromResult(FakeHandler.Json(201, OrderJson)));
        await c.Estamp.Orders.CreateAsync(Order);
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.Requests[0].Headers["Idempotency-Key"], h.Requests[1].Headers["Idempotency-Key"]);

        var (c2, h2, _) = Tc.Make((_, _) => throw new TaskCanceledException("timeout"));
        await Assert.ThrowsAsync<ApiTimeoutException>(() => c2.Estamp.Stamps.CancelAsync("eststamp_1"));
        Assert.Single(h2.Requests);
    }

    // ---- the never-retry set -----------------------------------------------------

    public static IEnumerable<object[]> NeverRetried() => new[]
    {
        new object[] { "order cancel", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Estamp.Orders.CancelAsync("estord_1", o)) },
        new object[] { "stamp cancel", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Estamp.Stamps.CancelAsync("eststamp_1", o)) },
        new object[] { "stamp replace", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Estamp.Stamps.ReplaceAsync("eststamp_1", o)) },
        new object[] { "pan-card link", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Estamp.Stamps.SetPanCardAsync("eststamp_1", new EstampPanCardRequest { PanCardImage = "https://cdn.example.com/p.jpg" }, o)) },
        new object[] { "stamp duty top-up", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Wallets.StampDuty.TopUpAsync(new WalletTopupRequest { BaseAmount = 1000 }, o)) },
        new object[] { "service fee top-up", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Wallets.ServiceFee.TopUpAsync(new WalletTopupRequest { BaseAmount = 500 }, o)) },
        new object[] { "webhook replay", (Func<StampMitraClient, RequestOptions, Task>)((c, o) => c.Webhooks.Events.ReplayAsync("evt_1", o)) },
    };

    [Theory]
    [MemberData(nameof(NeverRetried))]
    public async Task NeverAutoRetried(string name, Func<StampMitraClient, RequestOptions, Task> call)
    {
        foreach (var (status, code) in new[] { (503, "SERVICE_UNAVAILABLE"), (502, "INTERNAL_ERROR"), (429, "TOO_MANY_REQUESTS") })
        {
            var (c, h, delays) = Tc.Make((_, _) => Task.FromResult(Err(status, code, ("Retry-After", "1"))));
            // even a caller-supplied key must neither be sent nor make the call retryable
            var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => call(c, new RequestOptions { IdempotencyKey = "caller-key", MaxRetries = 5 }));
            Assert.Equal(status, ex.Status);
            Assert.Single(h.Requests);
            Assert.Empty(delays);
            Assert.False(h.Requests[0].Headers.ContainsKey("Idempotency-Key"), name);
            if (status == 429)
            {
                Assert.Equal(TimeSpan.FromSeconds(1), ((RateLimitException)ex).RetryAfter);
            }
        }
    }

    [Fact]
    public async Task CancelAndReplaceSendNoBodyAndNoContentType()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, OrderJson)));
        await c.Estamp.Orders.CancelAsync("estord_1");
        await c.Estamp.Stamps.ReplaceAsync("eststamp_1");
        Assert.All(h.Requests, r =>
        {
            Assert.Null(r.Body);
            Assert.False(r.Headers.ContainsKey("Content-Type"));
        });
    }

    [Fact]
    public async Task PanCardLinkAccepts202AndReadsTheStatus()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(202, """{"stampId":"eststamp_1","panCard":{"status":"PROCESSING","code":null,"message":null}}""")));
        var r = await c.Estamp.Stamps.SetPanCardAsync("eststamp_1", new EstampPanCardRequest { PanCardImage = "https://cdn.example.com/pan.jpg" });
        Assert.Equal(202, r.Response!.StatusCode);
        Assert.Equal("PROCESSING", r.PanCard!.Status);
        Assert.Equal("""{"panCardImage":"https://cdn.example.com/pan.jpg"}""", h.Requests[0].Body);
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Stamps.SetPanCardAsync("eststamp_1", new EstampPanCardRequest { PanCardImage = "http://x/y.jpg" }));
    }

    // ---- multipart upload ------------------------------------------------------------

    [Fact]
    public async Task UploadSendsMultipartWithFileAndTypePartsAndAKey()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(201, """{"documentId":"estdoc_1","orderId":"estord_1","type":"pan_card","fileName":"pan.pdf","mimeType":"application/pdf","sizeBytes":14,"receivedAt":"2026-10-29T09:12:03.000Z","status":"RECEIVED"}""")));
        var doc = await c.Estamp.Orders.UploadDocumentAsync("estord_1", EstampScenarios.SampleFile());
        Assert.Equal("estdoc_1", doc.DocumentId);
        var req = h.Requests[0];
        Assert.StartsWith("multipart/form-data; boundary=", req.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.Contains("""name="file"; filename="pan.pdf" """.TrimEnd(), req.Body!, StringComparison.Ordinal);
        Assert.Contains("Content-Type: application/pdf", req.Body, StringComparison.Ordinal);
        Assert.Contains("%PDF-1.4", req.Body, StringComparison.Ordinal);
        Assert.Contains("""name="type" """.TrimEnd(), req.Body, StringComparison.Ordinal);
        Assert.Contains("pan_card", req.Body, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f-]{36}$", req.Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task UploadRetryResendsTheWholeBodyWithTheSameKey()
    {
        var (c, h, _) = Tc.Make((_, n) => Task.FromResult(n == 1 ? Err(502, "INTERNAL_ERROR") : FakeHandler.Json(201, "{}")));
        await c.Estamp.Orders.UploadDocumentAsync("estord_1", EstampScenarios.SampleFile());
        Assert.Equal(2, h.Requests.Count);
        Assert.Equal(h.Requests[0].Headers["Idempotency-Key"], h.Requests[1].Headers["Idempotency-Key"]);
        Assert.Contains("%PDF-1.4", h.Requests[1].Body, StringComparison.Ordinal);
        Assert.NotEqual(h.Requests[0].Headers["Content-Type"], h.Requests[1].Headers["Content-Type"]); // fresh boundary
    }

    [Fact]
    public async Task UploadIsValidatedLocallyAndStreamsAndPathsWork()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(201, "{}")));
        var big = EstampDocumentFile.FromBytes(new byte[EstampDocumentFile.MaxBytes + 1], "pan.pdf");
        var ex = await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.UploadDocumentAsync("estord_1", big));
        Assert.Equal("file", ex.Field);
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.UploadDocumentAsync("estord_1", EstampDocumentFile.FromBytes(new byte[3], "pan.txt")));
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.UploadDocumentAsync("estord_1", EstampDocumentFile.FromBytes(Array.Empty<byte>(), "pan.pdf")));
        Assert.Empty(h.Requests);

        using var ms = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var png = await EstampDocumentFile.FromStreamAsync(ms, "pan.PNG");
        Assert.Equal("image/png", png.ContentType);
        var path = Path.Combine(Path.GetTempPath(), "pan-" + Guid.NewGuid().ToString("N") + ".jpg");
        await File.WriteAllBytesAsync(path, new byte[] { 0xFF, 0xD8, 0xFF });
        try
        {
            var jpg = await EstampDocumentFile.FromPathAsync(path);
            Assert.Equal("image/jpeg", jpg.ContentType);
            Assert.Equal(3, jpg.Length);
            await c.Estamp.Orders.UploadDocumentAsync("estord_1", jpg);
            Assert.Single(h.Requests);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- the party model ----------------------------------------------------------------

    [Fact]
    public void AStringConvertsToAPartyAndPartiesSerialiseAsObjects()
    {
        var line = new EstampStampLine
        {
            State = "MH",
            ArticleCode = "ART1",
            Denomination = 500,
            FirstParty = "Jane Doe",
            SecondParty = new EstampParty { Name = "John", Address = "Pune", Phone = "9", FatherHusbandName = "Raj", Gender = EstampGender.Male },
            PropertyArea = new EstampPropertyArea { Unit = EstampAreaUnit.SquareFeet, Value = 120.5m },
            Quantity = 2,
        };
        var json = JsonSerializer.Serialize(line, Json.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Jane Doe", root.GetProperty("firstParty").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("firstParty").ValueKind);
        Assert.Equal("MALE", root.GetProperty("secondParty").GetProperty("gender").GetString());
        Assert.Equal("Raj", root.GetProperty("secondParty").GetProperty("fatherHusbandName").GetString());
        Assert.Equal("Square Feet", root.GetProperty("propertyArea").GetProperty("unit").GetString());
    }

    [Fact]
    public void APartyIsReadFromAStringOrAnObject()
    {
        var line = JsonSerializer.Deserialize<EstampStampLine>("""{"state":"MH","firstParty":"Jane Doe","secondParty":{"name":"John","gender":"OTHER"}}""", Json.Options)!;
        Assert.Equal("Jane Doe", line.FirstParty!.Name);
        Assert.Equal("John", line.SecondParty!.Name);
        Assert.Equal("OTHER", line.SecondParty.Gender);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EstampStampLine>("""{"firstParty":5}""", Json.Options));
    }

    // ---- order validation on the client ----------------------------------------------------

    [Fact]
    public async Task OrdersAreCheckedLocallyForSizeQuantityAndOneState()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(201, OrderJson)));
        EstampStampLine Line(string state = "MH", int? q = null) => new() { State = state, ArticleCode = "A", Denomination = 1, FirstParty = "X", Quantity = q };
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(new EstampOrderRequest { Stamps = Array.Empty<EstampStampLine>() }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(new EstampOrderRequest { Stamps = Enumerable.Repeat(Line(), 5001).ToList() }));
        var qty = await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(new EstampOrderRequest { Stamps = new[] { Line(q: 1001) } }));
        Assert.Equal("stamps[0].quantity", qty.Field);
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(new EstampOrderRequest { Stamps = new[] { Line("MH"), Line("KA") } }));
        Assert.Empty(h.Requests);
        await c.Estamp.Orders.CreateAsync(new EstampOrderRequest { Stamps = Enumerable.Repeat(Line("MH", 1000), 5000).ToList() });
        Assert.Single(h.Requests);
    }

    // ---- error envelope -----------------------------------------------------------------------

    [Fact]
    public async Task ValidationDetailsAreTypedOnTheException()
    {
        var body = """{"error":{"code":"BAD_REQUEST","message":"VALIDATION_FAILED: x","details":{"errors":[{"field":"stamps[1].articleCode","code":"REQUIRED","message":"articleCode is required","index":1},{"field":"","code":"MIXED_STATE","message":"one state per order"}]},"requestId":"r1"}}""";
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(400, body)));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(Order));
        Assert.Equal("BAD_REQUEST", ex.Code);
        Assert.Equal(2, ex.Errors.Count);
        Assert.Equal(new ErrorDetail("stamps[1].articleCode", "REQUIRED", "articleCode is required", 1), ex.Errors[0]);
        Assert.Null(ex.Errors[1].Index);
        Assert.Null(ex.Wallet);
    }

    [Theory]
    [InlineData("STAMP_DUTY")]
    [InlineData("SERVICE_FEE")]
    public async Task WalletIsTypedOnA402(string wallet)
    {
        var body = """{"error":{"code":"PAYMENT_REQUIRED","message":"short","details":{"wallet":"WALLET"},"requestId":"r1"}}""".Replace("WALLET", wallet, StringComparison.Ordinal);
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(402, body)));
        var ex = await Assert.ThrowsAsync<PaymentRequiredException>(() => c.Estamp.Orders.CreateAsync(Order));
        Assert.Equal(wallet, ex.Wallet);
        Assert.Single(h.Requests); // 402 is never retried
        Assert.Empty(ex.Errors);
    }

    [Fact]
    public async Task Status422IsAValidationException()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(422, FakeHandler.Error("IDEMPOTENCY_KEY_REUSED", "reused"))));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.Orders.CreateAsync(Order));
        Assert.Equal(422, ex.Status);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ex.Code);
        Assert.Single(h.Requests);
    }

    // ---- money and numbers: read from the response, never computed -----------------------------

    [Fact]
    public async Task StampDutyTopUpReadsNumbersAndStringsExactlyAsSent()
    {
        var body = """{"baseAmount":1000,"gstAmount":10.8,"totalCharged":1070.8,"topupId":"cmwtopup1","processingFeePercent":"6.00","processingFeeAmount":"60.00","creditAmount":"1000.00","razorpayOrderId":"order_1","razorpayKeyId":"rzp_1"}""";
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, body)));
        var t = await c.Wallets.StampDuty.TopUpAsync(new WalletTopupRequest { BaseAmount = 1000 });
        Assert.Equal(1000m, t.BaseAmount);
        Assert.Equal(10.8m, t.GstAmount);
        Assert.Equal(1070.8m, t.TotalCharged);
        Assert.Equal("6.00", t.ProcessingFeePercent);
        Assert.Equal("60.00", t.ProcessingFeeAmount);
        Assert.Equal("1000.00", t.CreditAmount);
        Assert.Equal("cmwtopup1", t.TopupId);
        Assert.Equal("""{"baseAmount":1000}""", h.Requests[0].Body);
        Assert.Null((await Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, body.Replace("\"topupId\":\"cmwtopup1\",", string.Empty)))).Client.Wallets.StampDuty.TopUpAsync(new WalletTopupRequest { BaseAmount = 1000 })).TopupId);
    }

    [Fact]
    public async Task ServiceFeeTopUpAmountsAreStrings()
    {
        var body = """{"baseAmount":"500.00","gstAmount":"90.00","processingFeePercent":"0.00","processingFeeAmount":"0.00","creditAmount":"500.00","totalCharged":"590.00"}""";
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, body)));
        var t = await c.Wallets.ServiceFee.TopUpAsync(new WalletTopupRequest { BaseAmount = 500 });
        Assert.Equal("500.00", t.BaseAmount);
        Assert.Equal("590.00", t.TotalCharged);
        await Assert.ThrowsAsync<ValidationException>(() => c.Wallets.ServiceFee.TopUpAsync(new WalletTopupRequest { BaseAmount = 0 }));
    }

    [Fact]
    public async Task WalletHeadersAreAvailableOnTheResponse()
    {
        var (c, _, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, """{"balance":{"paise":100,"rupees":1},"state":"LOW_BALANCE"}""", ("X-Wallet-State", "LOW_BALANCE"), ("X-Wallet-Balance-Low", "true"))));
        var b = await c.Account.GetBalanceAsync();
        Assert.Equal("LOW_BALANCE", b.State);
        Assert.Equal(100, b.Balance!.Paise);
        Assert.Equal("LOW_BALANCE", b.Response!.Headers["X-Wallet-State"]);
    }

    // ---- paging -------------------------------------------------------------------------------------

    [Fact]
    public async Task OrdersAndLedgerPagesAreFollowedByCursor()
    {
        using var srv = new TinyServer((req, _) =>
        {
            var cursor = System.Web.HttpUtility.ParseQueryString(req.Url!.Query)["cursor"];
            var n = cursor is null ? 1 : int.Parse(cursor[1..], System.Globalization.CultureInfo.InvariantCulture) + 1;
            var next = n < 3 ? "\"c" + n + "\"" : "null";
            var path = req.Url!.AbsolutePath;
            var body = path.EndsWith("/transactions", StringComparison.Ordinal)
                ? "{\"items\":[{\"id\":\"t" + n + "\"}],\"nextCursor\":" + next + "}"
                : "{\"items\":[{\"id\":\"o" + n + "\",\"status\":\"PENDING_REVIEW\"}],\"nextCursor\":" + next + ",\"data\":[],\"pagination\":{\"page\":1,\"limit\":1,\"total\":null}}";
            return (200, body, "application/json", Array.Empty<(string, string)>());
        });
        using var client = new StampMitraClient(new StampMitraClientOptions { ApiKey = Tc.Key, BaseUrl = srv.BaseUrl });
        var orders = new List<string?>();
        await foreach (var o in client.Estamp.Orders.ListAllAsync(new EstampOrdersListParams { Limit = 1 }))
        {
            orders.Add(o.Id);
        }

        Assert.Equal(new[] { "o1", "o2", "o3" }, orders);
        var rows = new List<string?>();
        await foreach (var t in client.Wallets.StampDuty.ListAllTransactionsAsync())
        {
            rows.Add(t.Id);
        }

        Assert.Equal(6, srv.Count);
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task RequirementsQueryAndStatesAvailabilityFilterAreEncoded()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, """{"exampleBody":{"stamps":[{"state":"MH","firstParty":"Jane Doe"}]}}""")));
        var r = await c.Estamp.GetStateRequirementsAsync("MH", new EstampRequirementsParams { ArticleCode = "ART 1", Denomination = 500.5m, Category = EstampCategory.EStamp });
        Assert.Equal("Jane Doe", r.ExampleBody!.Stamps![0].FirstParty!.Name);
        Assert.Equal("/v1/estamp/states/MH/requirements?articleCode=ART%201&denomination=500.5&category=e_stamp", h.Requests[0].Uri.PathAndQuery);
        await c.Estamp.GetStatesAvailabilityAsync(true);
        await c.Estamp.GetStatesAvailabilityAsync(null);
        Assert.Equal("/v1/estamp/states/availability?active=true", h.Requests[1].Uri.PathAndQuery);
        Assert.Equal("/v1/estamp/states/availability", h.Requests[2].Uri.PathAndQuery);
    }
}
