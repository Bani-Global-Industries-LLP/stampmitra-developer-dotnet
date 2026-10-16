using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using StampMitra.Models;

namespace StampMitra.Tests;

public sealed class ClientTests
{
    private const string PanOk = """{"referenceId":"SMV-1","pan":"ABCDE1234F","valid":true,"registeredName":"ACME","brandNewField":{"x":1}}""";

    [Fact]
    public void VersionConstantMatchesAssembly()
    {
        var v = typeof(StampMitraClient).Assembly.GetName().Version!;
        Assert.Equal(StampMitraClient.Version, $"{v.Major}.{v.Minor}.{v.Build}");
    }

    [Theory]
    [InlineData("sk_test_abcdefgh", StampMitraEnvironment.Test)]
    [InlineData("sk_live_ABCDEFGH12_-", StampMitraEnvironment.Live)]
    public void AcceptsWellFormedKeysAndExposesEnvironment(string key, StampMitraEnvironment env)
    {
        using var c = new StampMitraClient(key);
        Assert.Equal(env, c.Environment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sk_test_short")]
    [InlineData("pk_test_abcdefgh1234")]
    [InlineData("sk_prod_abcdefgh1234")]
    [InlineData("sk_test_abcdefgh\n1234")]
    public void RejectsMalformedKeys(string key)
    {
        var ex = Assert.Throws<AuthenticationException>(() => new StampMitraClient(key));
        Assert.DoesNotContain("abcdefgh1234", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowCustomKeyAcceptsOtherKeys()
    {
        using var c = new StampMitraClient(new StampMitraClientOptions { ApiKey = "custom-key-123", AllowCustomKey = true });
        Assert.Equal(StampMitraEnvironment.Custom, c.Environment);
    }

    [Fact]
    public void ReadsKeyFromEnvironment()
    {
        var old = Environment.GetEnvironmentVariable("STAMPMITRA_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("STAMPMITRA_API_KEY", "sk_live_fromenv12345");
            using var c = new StampMitraClient();
            Assert.Equal(StampMitraEnvironment.Live, c.Environment);
            Environment.SetEnvironmentVariable("STAMPMITRA_API_KEY", null);
            Assert.Throws<AuthenticationException>(() => new StampMitraClient());
        }
        finally
        {
            Environment.SetEnvironmentVariable("STAMPMITRA_API_KEY", old);
        }
    }

    [Theory]
    [InlineData("http://api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("https://user:pass@api.example.com")]
    [InlineData("https://api.example.com/?x=1")]
    [InlineData("http://localhost.evil.com")]
    public void RejectsInsecureOrMalformedBaseUrls(string url)
    {
        var ex = Assert.Throws<ValidationException>(() => new StampMitraClient(new StampMitraClientOptions { ApiKey = Tc.Key, BaseUrl = url }));
        Assert.Equal("baseUrl", ex.Field);
        Assert.DoesNotContain("pass", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:4010")]
    [InlineData("http://127.0.0.1:4010")]
    [InlineData("http://[::1]:4010")]
    [InlineData("https://api.example.com/base")]
    public void AcceptsSecureOrLoopbackBaseUrls(string url)
    {
        using var c = new StampMitraClient(new StampMitraClientOptions { ApiKey = Tc.Key, BaseUrl = url });
        Assert.NotNull(c.BaseUrl);
    }

    [Fact]
    public void ToStringNeverIncludesTheKey()
    {
        var options = new StampMitraClientOptions { ApiKey = Tc.Key };
        using var c = new StampMitraClient(options);
        Assert.DoesNotContain(Tc.Key, options.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Tc.Key, c.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Tc.Key, c.Verify.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Tc.Key, c.Transport.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefgh12345678", System.Text.Json.JsonSerializer.Serialize(new { c.Environment, c.BaseUrl }), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonSerializationOfOptionsNeverIncludesTheKey()
    {
        var options = new StampMitraClientOptions { ApiKey = Tc.Key };
        options.DefaultHeaders["X-Custom"] = Tc.Key;
        Assert.DoesNotContain("abcdefgh12345678", System.Text.Json.JsonSerializer.Serialize(options), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendsExpectedHeadersAndBodyAndReturnsTypedModel()
    {
        var (c, h, _) = Tc.Make(FakeHandler.Json(201, PanOk, ("x-request-id", "req_abc")));
        var r = await c.Verify.PanAsync(new PanVerifyRequest { Pan = " abcde1234f ", Name = "Rahul Kumar" });

        Assert.True(r.Valid);
        Assert.Equal("SMV-1", r.ReferenceId);
        Assert.Equal(201, r.Response!.StatusCode);
        Assert.Equal("req_abc", r.Response.RequestId);
        Assert.Equal(1, r.Response.Attempts);
        Assert.True(r.AdditionalProperties!.ContainsKey("brandNewField"));

        var req = Assert.Single(h.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/v1/verify/pan", req.Uri.AbsolutePath);
        Assert.Equal("Bearer " + Tc.Key, req.Headers["Authorization"]);
        Assert.Equal("application/json", req.Headers["Accept"]);
        Assert.StartsWith("application/json", req.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.StartsWith("stampmitra-dotnet/0.1.0 (", req.Headers["User-Agent"], StringComparison.Ordinal);
        Assert.Matches(new Regex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"), req.Headers["Idempotency-Key"]);
        using var body = JsonDocument.Parse(req.Body!);
        Assert.Equal("ABCDE1234F", body.RootElement.GetProperty("pan").GetString());
        Assert.Equal("Rahul Kumar", body.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AppInfoAndDefaultHeadersAndPerCallHeaders()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, "{}")), o =>
        {
            o.AppInfo = new AppInfo("my-app", "2.0", "https://example.com");
            o.DefaultHeaders["X-Tenant"] = "t1";
            o.DefaultHeaders["Authorization"] = "Bearer evil";
        });
        await c.Usage.SummaryAsync(new RequestOptions { Headers = new Dictionary<string, string> { ["X-Trace"] = "abc", ["x-api-key"] = "evil" } });
        var req = Assert.Single(h.Requests);
        Assert.Contains("my-app/2.0 (https://example.com)", req.Headers["User-Agent"], StringComparison.Ordinal);
        Assert.Equal("t1", req.Headers["X-Tenant"]);
        Assert.Equal("abc", req.Headers["X-Trace"]);
        Assert.Equal("Bearer " + Tc.Key, req.Headers["Authorization"]);
        Assert.False(req.Headers.ContainsKey("x-api-key"));
    }

    [Fact]
    public async Task RejectsHeaderInjection()
    {
        var (c, h, _) = Tc.Make(FakeHandler.Json(200, "{}"));
        await Assert.ThrowsAsync<ValidationException>(() => c.Usage.SummaryAsync(new RequestOptions { Headers = new Dictionary<string, string> { ["X-A"] = "a\r\nX-B: b" } }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Usage.SummaryAsync(new RequestOptions { IdempotencyKey = "bad\nkey" }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Usage.SummaryAsync(new RequestOptions { IdempotencyKey = new string('x', 256) }));
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task ValidationFailsLocallyWithoutNetwork()
    {
        var (c, h, _) = Tc.Make(FakeHandler.Json(200, "{}"));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => c.Verify.PanAsync(new PanVerifyRequest { Pan = "nope" }));
        Assert.Equal("pan", ex.Field);
        Assert.Null(ex.Status);
        await Assert.ThrowsAsync<ValidationException>(() => c.Verify.PanAsync(new PanVerifyRequest()));
        await Assert.ThrowsAsync<ValidationException>(() => c.Verify.BankAccountAsync(new BankAccountVerifyRequest { AccountNumber = "123456789", Ifsc = "ICIC0001234", Phone = "123" }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Orders.CreateAsync(new CreateOrderRequest { ExternalOrderId = "o" }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Payments.Intents.CreateAsync(new CreatePaymentIntentRequest { ExternalOrderId = "o", Currency = "INR", PaymentConnectionId = "p" }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Estamp.CalculateDutyAsync(new EstampDutyCalculateRequest { StateCode = "MH", ArticleCode = "5" }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Logs.ListAsync(new Resources.LogsListParams { Limit = 500 }));
        await Assert.ThrowsAsync<ValidationException>(() => c.Logs.ListAsync(new Resources.LogsListParams { Status = "weird" }));
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task ValidateInputFalseSendsAsIs()
    {
        var (c, h, _) = Tc.Make((_, _) => Task.FromResult(FakeHandler.Json(200, PanOk)), o => o.ValidateInput = false);
        await c.Verify.PanAsync(new PanVerifyRequest { Pan = "nope" });
        Assert.Contains("\"pan\":\"nope\"", h.Requests[0].Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public async Task PathIdsAreValidated(string id)
    {
        var (c, h, _) = Tc.Make(FakeHandler.Json(200, "{}"));
        await Assert.ThrowsAsync<ValidationException>(() => c.Orders.GetAsync(id));
        Assert.Empty(h.Requests);
    }

    [Fact]
    public async Task PathIdsAreEncoded()
    {
        var (c, h, _) = Tc.Make(FakeHandler.Json(200, """{"externalOrderId":"a b?c#d","order":{"x":1}}"""));
        var r = await c.Orders.GetAsync("a b?c#d");
        Assert.Equal("/v1/orders/a%20b%3Fc%23d", h.Requests[0].Uri.AbsolutePath);
        Assert.Equal(1, r.Order!.Value.GetProperty("x").GetInt32());
        await c.Estamp.GetSchemaAsync("estamp.instrument.rent_agreement");
        Assert.Equal("/v1/estamp/schema/estamp.instrument.rent_agreement", h.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task UnknownAndMistypedFieldsAreTolerated()
    {
        var (c, _, _) = Tc.Make(FakeHandler.Json(200, """{"referenceId":"SMV-1","valid":true,"nameMatchScore":87,"extra":[1,2],"panStatus":false}"""));
        var r = await c.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F" });
        Assert.Equal("87", r.NameMatchScore);
        Assert.Equal("false", r.PanStatus);
        Assert.Equal(JsonValueKind.Array, r.AdditionalProperties!["extra"].ValueKind);
    }

    [Fact]
    public async Task ServicesListIsABareArray()
    {
        var (c, _, _) = Tc.Make(FakeHandler.Json(200, """[{"serviceCode":"a","name":"A","isActive":true},{"serviceCode":"b","name":"B","entitled":false}]"""));
        var list = await c.Services.ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal("b", list[1].ServiceCode);
        Assert.Equal(200, list.Response!.StatusCode);
        Assert.Equal(new[] { "a", "b" }, list.Select(s => s.ServiceCode).ToArray());
    }

    [Fact]
    public async Task GenericResponsesKeepEverything()
    {
        var (c, _, _) = Tc.Make(FakeHandler.Json(200, """{"items":[{"code":"MH"},{"code":"DL"}],"nextCursor":"c2","x":1}"""));
        var r = await c.Estamp.ListStatesAsync();
        Assert.Equal(2, r.Items.Count);
        Assert.Equal("c2", r.NextCursor);
        Assert.Equal(1, r.Get("x")!.Value.GetInt32());
        Assert.Contains("MH", r.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonObjectSuccessBodyForTypedModelIsReportedAsInvalidResponse()
    {
        var (c, _, _) = Tc.Make(FakeHandler.Json(200, "[1,2,3]"));
        var ex = await Assert.ThrowsAsync<StampMitraException>(() => c.Usage.SummaryAsync());
        Assert.Equal("INVALID_RESPONSE", ex.Code);
        var (c2, _, _) = Tc.Make(FakeHandler.Text(200, "<html>proxy</html>"));
        var ex2 = await Assert.ThrowsAsync<StampMitraException>(() => c2.Usage.SummaryAsync());
        Assert.Equal("INVALID_RESPONSE", ex2.Code);
    }

    [Fact]
    public async Task RedirectsAreNotFollowedAndSurfaceAsErrors()
    {
        var (c, h, _) = Tc.Make(FakeHandler.Text(302, "moved"));
        var ex = await Assert.ThrowsAsync<StampMitraException>(() => c.Usage.SummaryAsync());
        Assert.Equal(302, ex.Status);
        Assert.Single(h.Requests);
    }

    [Fact]
    public async Task DisposeLeavesSuppliedHttpClientUsable()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(FakeHandler.Json(200, "{}")));
        using var http = new HttpClient(handler);
        var c = new StampMitraClient(new StampMitraClientOptions { ApiKey = Tc.Key, HttpClient = http });
        c.Dispose();
        using var resp = await http.GetAsync(new Uri("https://example.com/"));
        Assert.True(resp.IsSuccessStatusCode);
    }
}
