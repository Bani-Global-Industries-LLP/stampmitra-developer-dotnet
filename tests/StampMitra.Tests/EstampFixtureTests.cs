using System.Text.Json;
using System.Text.RegularExpressions;
using StampMitra.Resources;

namespace StampMitra.Tests;

/// <summary>
/// Table-driven replay of the spec repo's golden fixtures (fixtures/operations) for the 31 operations added in contract 1.2.0:
/// each fixture's request is sent through the SDK to the mock server and checked on the wire (method, path, query, JSON or
/// multipart body, Idempotency-Key), the response is deserialised into the typed model and compared with the fixture
/// (no field dropped, none unmodelled), and every documented error fixture is mapped to the typed exception.
/// </summary>
[Collection("mock")]
[Trait("Category", "Conformance")]
public sealed class EstampFixtureTests
{
    private static readonly Regex Uuid4 = new("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$");
    private readonly MockServerFixture _mock;

    public EstampFixtureTests(MockServerFixture mock)
    {
        _mock = mock;
    }

    private sealed record Op(string Slug, string Method, string Path, bool AutoKey, Func<StampMitraClient, JsonElement, RequestOptions, Task<ApiModel>> Call);

    private static string[] Segs(JsonElement req) => req.GetProperty("path").GetString()!.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string? Q(JsonElement req, string name) =>
        req.TryGetProperty("query", out var q) && q.TryGetProperty(name, out var v) ? v.GetString() : null;

    private static T Body<T>(JsonElement req) => req.GetProperty("body").Deserialize<T>(Json.Options)!;

    private static readonly Op[] Ops =
    {
        new("get-v1-estamp-states-availability", "GET", "/v1/estamp/states/availability", false, async (c, r, o) => await c.Estamp.GetStatesAvailabilityAsync(Q(r, "active") == "true", o)),
        new("get-v1-estamp-states-statecode", "GET", "/v1/estamp/states/{}", false, async (c, r, o) => await c.Estamp.GetStateAsync(Segs(r)[3], o)),
        new("get-v1-estamp-states-statecode-requirements", "GET", "/v1/estamp/states/{}/requirements", false, async (c, r, o) => await c.Estamp.GetStateRequirementsAsync(Segs(r)[3], new EstampRequirementsParams { ArticleCode = Q(r, "articleCode"), Denomination = decimal.Parse(Q(r, "denomination")!, System.Globalization.CultureInfo.InvariantCulture), Category = Q(r, "category") }, o)),
        new("get-v1-estamp-states-statecode-districts", "GET", "/v1/estamp/states/{}/districts", false, async (c, r, o) => await c.Estamp.ListDistrictsAsync(Segs(r)[3], o)),
        new("get-v1-estamp-states-statecode-districts-districtcode-sros", "GET", "/v1/estamp/states/{}/districts/{}/sros", false, async (c, r, o) => await c.Estamp.ListSrosAsync(Segs(r)[3], Segs(r)[5], o)),
        new("post-v1-estamp-orders-validate", "POST", "/v1/estamp/orders/validate", false, async (c, r, o) => await c.Estamp.Orders.ValidateAsync(Body<EstampOrderRequest>(r), o)),
        new("post-v1-estamp-orders", "POST", "/v1/estamp/orders", true, async (c, r, o) => await c.Estamp.Orders.CreateAsync(Body<EstampOrderRequest>(r), o)),
        new("get-v1-estamp-orders", "GET", "/v1/estamp/orders", false, async (c, r, o) => await c.Estamp.Orders.ListAsync(new EstampOrdersListParams { Page = int.Parse(Q(r, "page")!), Limit = int.Parse(Q(r, "limit")!) }, o)),
        new("get-v1-estamp-orders-id", "GET", "/v1/estamp/orders/{}", false, async (c, r, o) => await c.Estamp.Orders.GetAsync(Segs(r)[3], o)),
        new("post-v1-estamp-orders-id-cancel", "POST", "/v1/estamp/orders/{}/cancel", false, async (c, r, o) => await c.Estamp.Orders.CancelAsync(Segs(r)[3], o)),
        new("get-v1-estamp-orders-id-export", "GET", "/v1/estamp/orders/{}/export", false, async (c, r, o) => await c.Estamp.Orders.ExportAsync(Segs(r)[3], o)),
        new("get-v1-estamp-orders-id-stamps", "GET", "/v1/estamp/orders/{}/stamps", false, async (c, r, o) => await c.Estamp.Orders.ListStampsAsync(Segs(r)[3], o)),
        new("post-v1-estamp-orders-orderid-documents", "POST", "/v1/estamp/orders/{}/documents", true, async (c, r, o) =>
        {
            var file = r.GetProperty("multipart").GetProperty("parts").EnumerateArray().First(p => p.GetProperty("name").GetString() == "file");
            return await c.Estamp.Orders.UploadDocumentAsync(Segs(r)[3], EstampDocumentFile.FromBytes(System.Text.Encoding.UTF8.GetBytes(file.GetProperty("content").GetString()!), file.GetProperty("filename").GetString()!, file.GetProperty("contentType").GetString()), o);
        }),
        new("get-v1-estamp-stamps-stampid", "GET", "/v1/estamp/stamps/{}", false, async (c, r, o) => await c.Estamp.Stamps.GetAsync(Segs(r)[3], o)),
        new("post-v1-estamp-stamps-stampid-cancel", "POST", "/v1/estamp/stamps/{}/cancel", false, async (c, r, o) => await c.Estamp.Stamps.CancelAsync(Segs(r)[3], o)),
        new("post-v1-estamp-stamps-stampid-replace", "POST", "/v1/estamp/stamps/{}/replace", false, async (c, r, o) => await c.Estamp.Stamps.ReplaceAsync(Segs(r)[3], o)),
        new("post-v1-estamp-stamps-stampid-pan-card", "POST", "/v1/estamp/stamps/{}/pan-card", false, async (c, r, o) => await c.Estamp.Stamps.SetPanCardAsync(Segs(r)[3], Body<EstampPanCardRequest>(r), o)),
        new("get-v1-estamp-stamps-stampid-certificate", "GET", "/v1/estamp/stamps/{}/certificate", false, async (c, r, o) => await c.Estamp.Stamps.GetCertificateAsync(Segs(r)[3], o)),
        new("get-v1-estamp-wallet", "GET", "/v1/estamp/wallet", false, async (c, r, o) => await c.Estamp.GetWalletAsync(o)),
        new("get-v1-stamp-duty-wallet", "GET", "/v1/stamp-duty-wallet", false, async (c, r, o) => await c.Wallets.StampDuty.GetBalanceAsync(o)),
        new("post-v1-stamp-duty-wallet-topup", "POST", "/v1/stamp-duty-wallet/topup", false, async (c, r, o) => await c.Wallets.StampDuty.TopUpAsync(Body<WalletTopupRequest>(r), o)),
        new("get-v1-stamp-duty-wallet-transactions", "GET", "/v1/stamp-duty-wallet/transactions", false, async (c, r, o) => await c.Wallets.StampDuty.ListTransactionsAsync(new WalletListParams { Limit = int.Parse(Q(r, "limit")!) }, o)),
        new("get-v1-stamp-duty-wallet-invoices", "GET", "/v1/stamp-duty-wallet/invoices", false, async (c, r, o) => await c.Wallets.StampDuty.ListInvoicesAsync(new WalletListParams { Limit = int.Parse(Q(r, "limit")!) }, o)),
        new("get-v1-stamp-duty-wallet-invoices-id", "GET", "/v1/stamp-duty-wallet/invoices/{}", false, async (c, r, o) => await c.Wallets.StampDuty.GetInvoiceAsync(Segs(r)[3], o)),
        new("get-v1-service-fee-wallet", "GET", "/v1/service-fee-wallet", false, async (c, r, o) => await c.Wallets.ServiceFee.GetBalanceAsync(o)),
        new("post-v1-service-fee-wallet-topup", "POST", "/v1/service-fee-wallet/topup", false, async (c, r, o) => await c.Wallets.ServiceFee.TopUpAsync(Body<WalletTopupRequest>(r), o)),
        new("get-v1-service-fee-wallet-transactions", "GET", "/v1/service-fee-wallet/transactions", false, async (c, r, o) => await c.Wallets.ServiceFee.ListTransactionsAsync(new WalletListParams { Limit = int.Parse(Q(r, "limit")!) }, o)),
        new("get-v1-service-fee-wallet-invoices", "GET", "/v1/service-fee-wallet/invoices", false, async (c, r, o) => await c.Wallets.ServiceFee.ListInvoicesAsync(new WalletListParams { Limit = int.Parse(Q(r, "limit")!) }, o)),
        new("get-v1-service-fee-wallet-invoices-id", "GET", "/v1/service-fee-wallet/invoices/{}", false, async (c, r, o) => await c.Wallets.ServiceFee.GetInvoiceAsync(Segs(r)[3], o)),
        new("get-v1-account-balance", "GET", "/v1/account/balance", false, async (c, r, o) => await c.Account.GetBalanceAsync(o)),
        new("get-v1-account-usage", "GET", "/v1/account/usage", false, async (c, r, o) => await c.Account.GetUsageAsync(o)),
    };

    private static string? FixtureDir()
    {
        var dir = Environment.GetEnvironmentVariable("STAMPMITRA_SPEC_DIR");
        dir = string.IsNullOrEmpty(dir) ? "/home/user/stampmitra-developer-openapi" : dir;
        var d = Path.Combine(dir, "fixtures", "operations");
        return Directory.Exists(d) ? d : null;
    }

    private static IEnumerable<string> Scenarios(Op op)
    {
        var dir = FixtureDir();
        if (dir is null)
        {
            return new[] { "success" };
        }

        return Directory.GetFiles(dir, op.Slug + ".*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)[(op.Slug.Length + 1)..])
            .OrderBy(x => x, StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> SuccessCases() =>
        Ops.SelectMany(op => Scenarios(op).Where(s => s.StartsWith("success", StringComparison.Ordinal)).Select(s => new object[] { op.Slug, s }));

    public static IEnumerable<object[]> ErrorCases() =>
        Ops.SelectMany(op => Scenarios(op).Where(s => s.StartsWith("error-", StringComparison.Ordinal)).Select(s => new object[] { op.Slug, s }));

    private static JsonDocument Load(Op op, string scenario) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDir()!, op.Slug + "." + scenario + ".json")));

    private StampMitraClient Client(List<TimeSpan> delays, int maxRetries = 0) =>
        new(new StampMitraClientOptions
        {
            ApiKey = Tc.Key,
            BaseUrl = _mock.BaseUrl,
            MaxRetries = maxRetries,
            DelayAsync = (d, _) =>
            {
                delays.Add(d);
                return Task.CompletedTask;
            },
        });

    [Fact]
    public void TheTableCoversThirtyOneDistinctOperations()
    {
        Assert.Equal(31, Ops.Length);
        Assert.Equal(31, Ops.Select(o => o.Method + " " + o.Path).Distinct().Count());
    }

    [SkippableTheory]
    [MemberData(nameof(SuccessCases))]
    public async Task SuccessFixtureReplaysThroughTheSdk(string slug, string scenario)
    {
        Skip.If(_mock.SkipReason is not null || FixtureDir() is null, _mock.SkipReason ?? "fixtures unavailable");
        _mock.Reset();
        var op = Ops.First(o => o.Slug == slug);
        using var fx = Load(op, scenario);
        var req = fx.RootElement.GetProperty("request");
        var expected = fx.RootElement.GetProperty("response");
        using var client = Client(new List<TimeSpan>());

        var model = await op.Call(client, req, new RequestOptions { Headers = new Dictionary<string, string> { ["x-mock-scenario"] = scenario } });

        // the response: status, no field dropped, none unmodelled, value-for-value equal to the fixture
        Assert.Equal(expected.GetProperty("status").GetInt32(), model.Response!.StatusCode);
        Assert.Empty(JsonCompare.UnmodelledFields(model));
        Assert.True(JsonCompare.Equal(JsonCompare.ToJson(model), expected.GetProperty("body"), out var why), slug + ": " + why);

        // the request on the wire
        var seen = Assert.Single(_mock.Recorded());
        Assert.Equal(op.Method, seen.GetProperty("method").GetString());
        Assert.Equal(req.GetProperty("path").GetString(), seen.GetProperty("path").GetString());
        var headers = seen.GetProperty("headers");
        Assert.Equal("Bearer " + Tc.Key, headers.GetProperty("authorization").GetString());
        if (op.Method == "GET")
        {
            var query = seen.GetProperty("query").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
            var want = req.TryGetProperty("query", out var q) ? q.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()) : new Dictionary<string, string?>();
            Assert.Equal(want.OrderBy(k => k.Key), query.OrderBy(k => k.Key));
        }

        if (op.AutoKey)
        {
            Assert.Matches(Uuid4, headers.GetProperty("idempotency-key").GetString()!);
        }
        else
        {
            Assert.False(headers.TryGetProperty("idempotency-key", out _), "no Idempotency-Key on " + slug);
        }

        if (req.TryGetProperty("body", out var wantBody))
        {
            Assert.True(JsonCompare.Equal(seen.GetProperty("body"), wantBody, out var bodyWhy), slug + " body: " + bodyWhy);
        }
        else if (req.TryGetProperty("multipart", out var mp))
        {
            var parts = seen.GetProperty("body").GetProperty("multipart").EnumerateArray().ToList();
            Assert.StartsWith("multipart/form-data; boundary=", headers.GetProperty("content-type").GetString(), StringComparison.Ordinal);
            foreach (var want in mp.GetProperty("parts").EnumerateArray())
            {
                var got = parts.Single(p => p.GetProperty("name").GetString() == want.GetProperty("name").GetString());
                if (want.TryGetProperty("filename", out var fn))
                {
                    Assert.Equal(fn.GetString(), got.GetProperty("filename").GetString());
                    Assert.Equal(want.GetProperty("contentType").GetString(), got.GetProperty("contentType").GetString());
                    Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(want.GetProperty("content").GetString()!), got.GetProperty("size").GetInt32());
                }
                else
                {
                    Assert.Equal(want.GetProperty("value").GetString(), got.GetProperty("value").GetString());
                }
            }

            Assert.Equal(mp.GetProperty("parts").GetArrayLength(), parts.Count);
        }
        else if (op.Method == "POST")
        {
            Assert.True(!seen.TryGetProperty("body", out var b) || b.ValueKind == JsonValueKind.Null, "bodiless POST " + slug + " must send no body");
        }
    }

    private static Type ExceptionFor(int status) => status switch
    {
        400 or 422 => typeof(ValidationException),
        401 => typeof(AuthenticationException),
        402 => typeof(PaymentRequiredException),
        403 => typeof(PermissionDeniedException),
        404 => typeof(NotFoundException),
        409 => typeof(ConflictException),
        429 => typeof(RateLimitException),
        _ => typeof(UpstreamException),
    };

    [SkippableTheory]
    [MemberData(nameof(ErrorCases))]
    public async Task ErrorFixtureMapsToTheTypedException(string slug, string scenario)
    {
        Skip.If(_mock.SkipReason is not null || FixtureDir() is null, _mock.SkipReason ?? "fixtures unavailable");
        _mock.Reset();
        var op = Ops.First(o => o.Slug == slug);
        using var fx = Load(op, scenario);
        var expected = fx.RootElement.GetProperty("response");
        var status = expected.GetProperty("status").GetInt32();
        var err = expected.GetProperty("body").GetProperty("error");
        using var client = Client(new List<TimeSpan>());

        var ex = await Assert.ThrowsAnyAsync<StampMitraException>(() => op.Call(client, fx.RootElement.GetProperty("request"), new RequestOptions { Headers = new Dictionary<string, string> { ["x-mock-scenario"] = scenario } }));

        Assert.IsType(ExceptionFor(status), ex);
        Assert.Equal(status, ex.Status);
        Assert.Equal(err.GetProperty("code").GetString(), ex.Code);
        Assert.Equal(err.GetProperty("message").GetString(), ex.Message);
        Assert.Equal(err.GetProperty("requestId").GetString(), ex.RequestId);
        Assert.DoesNotContain(Tc.Key, ex.ToString(), StringComparison.Ordinal);

        var wantErrors = err.TryGetProperty("details", out var d) && d.TryGetProperty("errors", out var list) ? list.EnumerateArray().ToList() : new List<JsonElement>();
        Assert.Equal(wantErrors.Count, ex.Errors.Count);
        for (var i = 0; i < wantErrors.Count; i++)
        {
            Assert.Equal(wantErrors[i].GetProperty("field").GetString(), ex.Errors[i].Field);
            Assert.Equal(wantErrors[i].GetProperty("code").GetString(), ex.Errors[i].Code);
            Assert.Equal(wantErrors[i].GetProperty("message").GetString(), ex.Errors[i].Message);
            Assert.Equal(wantErrors[i].TryGetProperty("index", out var ix) ? ix.GetInt32() : null, ex.Errors[i].Index);
        }

        Assert.Equal(err.TryGetProperty("details", out var d2) && d2.TryGetProperty("wallet", out var w) ? w.GetString() : null, ex.Wallet);

        if (expected.GetProperty("headers").TryGetProperty("Retry-After", out var ra))
        {
            var want = TimeSpan.FromSeconds(double.Parse(ra.GetString()!, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(want, ex switch
            {
                RateLimitException r => r.RetryAfter,
                ConflictException c => c.RetryAfter,
                UpstreamException u => u.RetryAfter,
                _ => null,
            });
        }

        // a failed call made exactly one attempt (retries are off here)
        Assert.Single(_mock.Recorded());
    }
}
