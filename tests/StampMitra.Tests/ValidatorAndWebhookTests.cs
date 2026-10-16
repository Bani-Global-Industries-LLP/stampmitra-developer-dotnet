using System.Text.Json;
using StampMitra.Webhooks;

namespace StampMitra.Tests;

public sealed class ValidatorTests
{
    private static readonly Dictionary<string, Func<string?, string>> Map = new()
    {
        ["pan"] = v => Validators.Pan(v),
        ["gstin"] = v => Validators.Gstin(v),
        ["ifsc"] = v => Validators.Ifsc(v),
        ["cin"] = v => Validators.Cin(v),
        ["udyam"] = v => Validators.Udyam(v),
        ["vehicleNumber"] = v => Validators.VehicleNumber(v),
        ["bankAccount"] = v => Validators.BankAccount(v),
        ["passport"] = v => Validators.PassportFileNumber(v),
        ["drivingLicense"] = v => Validators.DrivingLicense(v),
        ["voterId"] = v => Validators.VoterId(v),
    };

    public static IEnumerable<object[]> Vectors()
    {
        using var doc = JsonDocument.Parse(Tc.Fixture("validators.json"));
        foreach (var v in doc.RootElement.GetProperty("validators").EnumerateObject())
        {
            foreach (var s in v.Value.GetProperty("valid").EnumerateArray())
            {
                yield return new object[] { v.Name, s.GetString()!, true };
            }

            foreach (var s in v.Value.GetProperty("invalid").EnumerateArray())
            {
                yield return new object[] { v.Name, s.GetString()!, false };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void SharedVectors(string validator, string input, bool valid)
    {
        var fn = Map[validator];
        if (valid)
        {
            var normalized = fn(input);
            Assert.Equal(validator == "bankAccount" ? input.Trim() : input.Trim().ToUpperInvariant(), normalized);
        }
        else
        {
            var ex = Assert.Throws<ValidationException>(() => fn(input));
            Assert.NotNull(ex.Field);
            Assert.Null(ex.Status);
        }
    }

    [Fact]
    public void VectorsCoverEveryValidator() => Assert.Equal(Map.Keys.OrderBy(x => x), Vectors().Select(v => (string)v[0]).Distinct().OrderBy(x => x));

    [Theory]
    [InlineData("MH-12-AB-1234")]
    [InlineData("MH 12 AB 1234")]
    public void DoesNotStripSeparators(string vehicle) => Assert.Throws<ValidationException>(() => Validators.VehicleNumber(vehicle));

    [Fact]
    public void NullInputIsAValidationError() => Assert.Throws<ValidationException>(() => Validators.Pan(null));

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("+919876543210", true)]
    [InlineData("09876543210", true)]
    [InlineData("1234567890", false)]
    [InlineData("98765", false)]
    public void Phone(string v, bool ok)
    {
        if (ok)
        {
            Assert.Equal(v, Validators.Phone(v));
        }
        else
        {
            Assert.Throws<ValidationException>(() => Validators.Phone(v));
        }
    }

    [Theory]
    [InlineData("1990-01-31", true)]
    [InlineData("1990-01-31T10:00:00Z", true)]
    [InlineData("31-01-1990", false)]
    [InlineData("", false)]
    public void Date(string v, bool ok)
    {
        if (ok)
        {
            Assert.Equal(v, Validators.Date(v));
        }
        else
        {
            Assert.Throws<ValidationException>(() => Validators.Date(v));
        }
    }

    [Fact]
    public void Name()
    {
        Assert.Equal("Rahul", Validators.Name("  Rahul "));
        Assert.Throws<ValidationException>(() => Validators.Name("   "));
        Assert.Throws<ValidationException>(() => Validators.Name(new string('a', 257)));
    }
}

public sealed class WebhookTests
{
    public static IEnumerable<object[]> SignatureVectors()
    {
        using var doc = JsonDocument.Parse(Tc.Fixture("webhook-signatures.json"));
        foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            yield return new object[] { v.GetProperty("name").GetString()! };
        }
    }

    private static JsonElement Vector(string name, out long now)
    {
        var doc = JsonDocument.Parse(Tc.Fixture("webhook-signatures.json"));
        now = doc.RootElement.GetProperty("now").GetInt64();
        return doc.RootElement.GetProperty("vectors").EnumerateArray().First(v => v.GetProperty("name").GetString() == name);
    }

    [Theory]
    [MemberData(nameof(SignatureVectors))]
    public void SharedSignatureVectors(string name)
    {
        var v = Vector(name, out var now);
        var at = DateTimeOffset.FromUnixTimeSeconds(now);
        var result = WebhookSignature.Check(v.GetProperty("payload").GetString()!, v.GetProperty("timestamp").GetString()!, v.GetProperty("signature").GetString()!, v.GetProperty("secret").GetString()!, 300, at);
        var expected = v.GetProperty("expected");
        if (expected.GetProperty("valid").GetBoolean())
        {
            Assert.Equal(WebhookVerificationResult.Valid, result);
        }
        else
        {
            var reason = expected.GetProperty("reason").GetString();
            Assert.Equal(reason switch
            {
                "signature_mismatch" => WebhookVerificationResult.SignatureMismatch,
                "timestamp_outside_tolerance" => WebhookVerificationResult.TimestampOutsideTolerance,
                "invalid_timestamp" => WebhookVerificationResult.InvalidTimestamp,
                _ => throw new InvalidOperationException(reason),
            }, result);
        }

        Assert.Equal(result == WebhookVerificationResult.Valid, WebhookSignature.Verify(v.GetProperty("payload").GetString()!, v.GetProperty("timestamp").GetString()!, v.GetProperty("signature").GetString()!, v.GetProperty("secret").GetString()!, 300, at));
    }

    [Fact]
    public void BytePayloadsAndStringPayloadsAgree()
    {
        var v = Vector("valid-unicode-payload", out var now);
        var at = DateTimeOffset.FromUnixTimeSeconds(now);
        var bytes = System.Text.Encoding.UTF8.GetBytes(v.GetProperty("payload").GetString()!);
        Assert.True(WebhookSignature.Verify(bytes, v.GetProperty("timestamp").GetString()!, v.GetProperty("signature").GetString()!, v.GetProperty("secret").GetString()!, 300, at));
    }

    [Fact]
    public void SignProducesLowerCaseHex()
    {
        var sig = WebhookSignature.Sign("{}", "1760605190", "whsec_test_0123456789abcdef");
        Assert.Matches("^[0-9a-f]{64}$", sig);
        Assert.True(WebhookSignature.Verify("{}", "1760605190", sig, "whsec_test_0123456789abcdef", 300, DateTimeOffset.FromUnixTimeSeconds(1760605200)));
    }

    [Fact]
    public void EmptySecretIsRejected()
    {
        var ex = Assert.Throws<SignatureVerificationException>(() => WebhookSignature.Verify("{}", "1760605190", "x", string.Empty));
        Assert.Equal("MISSING_SECRET", ex.Code);
        Assert.Throws<SignatureVerificationException>(() => WebhookSignature.Sign("{}", "1", string.Empty));
        Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent("{}", "1", "x", string.Empty));
    }

    [Fact]
    public void DefaultToleranceIs300Seconds()
    {
        Assert.Equal(300, WebhookSignature.DefaultToleranceSeconds);
        var secret = "whsec_x";
        var old = DateTimeOffset.UtcNow.AddSeconds(-400).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(WebhookSignature.Verify("{}", old, WebhookSignature.Sign("{}", old, secret), secret));
        var fresh = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(WebhookSignature.Verify("{}", fresh, WebhookSignature.Sign("{}", fresh, secret), secret));
    }

    public static IEnumerable<object[]> EventFixtures() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures", "webhooks"), "*.json").OrderBy(x => x).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void AllEightEventFixturesArePresent() => Assert.Equal(8, EventFixtures().Count());

    [Theory]
    [MemberData(nameof(EventFixtures))]
    public void ConstructEventReturnsTypedEventForEveryOrderType(string file)
    {
        using var doc = JsonDocument.Parse(Tc.Fixture("webhooks", file));
        var f = doc.RootElement;
        var headers = f.GetProperty("headers");
        var at = DateTimeOffset.FromUnixTimeSeconds(f.GetProperty("now").GetInt64());
        var raw = f.GetProperty("rawBody").GetString()!;
        var evt = WebhookSignature.ConstructEventFromHeaders(
            System.Text.Encoding.UTF8.GetBytes(raw),
            name => headers.TryGetProperty(name, out var v) ? v.GetString() : null,
            f.GetProperty("secret").GetString()!,
            300,
            at);

        var expectedType = f.GetProperty("event").GetString()!;
        Assert.Equal(expectedType, evt.Type);
        Assert.Equal(headers.GetProperty("X-Webhook-Event-Id").GetString(), evt.EventId);
        Assert.False(string.IsNullOrEmpty(evt.CoreOrderId));
        Assert.False(string.IsNullOrEmpty(evt.OccurredAt));
        Assert.Contains(expectedType, WebhookSignature.EventTypes);
        var expectedClass = "Order" + string.Concat(expectedType["order.".Length..].Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..])) + "Event";
        Assert.Equal(expectedClass, evt.GetType().Name);
    }

    [Fact]
    public void ExtraFieldsArePreservedAndUnknownEventsAreAccepted()
    {
        var secret = "whsec_x";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var body = """{"coreOrderId":"co1","coreEventType":"ORDER_FAILED","occurredAt":"2026-10-16T00:00:00Z","reason":"x","n":3}""";
        var evt = WebhookSignature.ConstructEvent(body, ts, WebhookSignature.Sign(body, ts, secret), secret, eventId: "evt_9");
        var failed = Assert.IsType<OrderFailedEvent>(evt);
        Assert.Equal("x", failed.AdditionalProperties!["reason"].GetString());
        Assert.Equal("evt_9", failed.EventId);

        var other = """{"coreOrderId":"co1","coreEventType":"SOMETHING_NEW","occurredAt":"t"}""";
        var u = Assert.IsType<UnknownWebhookEvent>(WebhookSignature.ConstructEvent(other, ts, WebhookSignature.Sign(other, ts, secret), secret));
        Assert.Null(u.Type);
        Assert.Equal("SOMETHING_NEW", u.CoreEventType);
    }

    [Fact]
    public void BadSignatureOrPayloadRaisesTypedErrors()
    {
        var secret = "whsec_x";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var ex = Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent("{}", ts, "deadbeef", secret));
        Assert.Equal("SIGNATURE_MISMATCH", ex.Code);
        Assert.DoesNotContain(secret, ex.ToString(), StringComparison.Ordinal);
        Assert.Equal("INVALID_TIMESTAMP", Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent("{}", "yesterday", "x", secret)).Code);
        Assert.Equal("TIMESTAMP_OUTSIDE_TOLERANCE", Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent("{}", "1", "x", secret)).Code);
        var list = "[1]";
        Assert.Equal("INVALID_PAYLOAD", Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent(list, ts, WebhookSignature.Sign(list, ts, secret), secret)).Code);
        var bad = "{nope";
        Assert.Equal("INVALID_PAYLOAD", Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEvent(bad, ts, WebhookSignature.Sign(bad, ts, secret), secret)).Code);
        Assert.Equal("MISSING_HEADERS", Assert.Throws<SignatureVerificationException>(() => WebhookSignature.ConstructEventFromHeaders(System.Text.Encoding.UTF8.GetBytes("{}"), _ => null, secret)).Code);
    }
}
