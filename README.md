# StampMitra .NET SDK

[![CI](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/StampMitra)](https://www.nuget.org/packages/StampMitra)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

> The NuGet badge shows a version only once the package is published.

Official .NET client for the [StampMitra Developer API](https://developer.stampmitra.in/docs): Indian identity verification
(PAN, GSTIN, vehicle RC, Voter ID, driving licence, passport, CIN, Udyam, bank account) and e-Stamp Paper.
OpenAPI spec: <https://developer.stampmitra.in/openapi.json>.

> **Status: pre-1.0 (0.1.0).** The API surface may change in minor releases. Pin your version.

SDK guide: <https://developer.stampmitra.in/sdks/dotnet> | API docs: <https://developer.stampmitra.in/docs>

- Async-first, `CancellationToken` on every call, `IAsyncEnumerable<T>` pagination.
- Only `System.Net.Http.HttpClient` and `System.Text.Json`: **no third-party runtime dependencies** in the core package.
- Nullable reference types, XML docs on every public member, SourceLink and symbol packages.
- Typed exceptions, safe retries with full-jitter backoff, automatic idempotency keys, client-side validators, webhook verification.

## Install

```bash
dotnet add package StampMitra
# optional: services.AddStampMitra(...) for ASP.NET Core / generic host
dotnet add package StampMitra.Extensions.DependencyInjection
```

Targets `net6.0` and `net8.0` (and therefore runs on .NET 6 and every later runtime). There is no `netstandard2.1` or .NET Framework
build: `System.Text.Json` is a separate NuGet dependency on those targets and the core package stays dependency-free. .NET 6 is
past its end of support; it is kept as a target for existing applications and is tested in CI.

## Quickstart

```csharp
using StampMitra;
using StampMitra.Models;

using var client = new StampMitraClient();   // reads STAMPMITRA_API_KEY
var pan = await client.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F", Name = "Rahul Kumar" }, ct);
Console.WriteLine($"{(pan.Valid == true ? "valid" : "invalid")} {pan.NameMatchResult} {pan.ReferenceId}");
```

(`ct` is a `CancellationToken`; use `CancellationToken.None` if you do not need cancellation.)

## Authentication

Keys look like `sk_test_...` (free sandbox) or `sk_live_...` (metered). The client reads `STAMPMITRA_API_KEY` unless you set
`StampMitraClientOptions.ApiKey`. Keys that do not match `^sk_(test|live)_[A-Za-z0-9_-]{8,}$` are rejected unless
`AllowCustomKey` is set. `client.Environment` is `Test`, `Live` (or `Custom`).

> **Security:** server-side only. Never ship `sk_` keys in mobile apps, desktop apps that you distribute, browsers (Blazor WebAssembly) or public repositories.

```csharp
using var client = new StampMitraClient(new StampMitraClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("STAMPMITRA_API_KEY"),
    Timeout = TimeSpan.FromSeconds(20),   // per attempt, default 30 s
    MaxRetries = 3,                       // default 2
    AppInfo = new AppInfo("my-app", "1.4.0", "https://example.com"),
});
```

| Option | Default | Notes |
|--------|---------|-------|
| `ApiKey` | `STAMPMITRA_API_KEY` | never appears in `ToString()`, exceptions or logs |
| `BaseUrl` | `https://api-b2b.stampmitra.in` | must be `https`; `http` only for `localhost`, `127.0.0.1`, `::1`; no credentials in the URL |
| `Timeout` | 30 s | per attempt |
| `MaxRetries` | 2 | 0 to 10 |
| `HttpClient` | one the SDK creates | yours is never disposed; see [Timeouts and redirects](#timeouts-cancellation-and-redirects) |
| `DefaultHeaders`, `AppInfo` | none | sent with every request / appended to the `User-Agent` |
| `DebugLog` | off | `Action<string>` receiving redacted lines |
| `ValidateInput` | `true` | client-side validators |
| `AllowCustomKey` | `false` | accept non-`sk_` keys |

Every method also takes a per-call `RequestOptions` (`Timeout`, `MaxRetries`, `IdempotencyKey`, `Headers`) and a `CancellationToken`:

```csharp
var order = await client.Orders.CreateAsync(
    new CreateOrderRequest { ExternalOrderId = "o1", ExternalCustomerId = "c1", ServiceCode = "estamp.instrument.rent_agreement" },
    new RequestOptions { IdempotencyKey = "order-o1", Timeout = TimeSpan.FromSeconds(10) },
    ct);
```

## Resources

| Resource | Methods |
|----------|---------|
| `client.Verify` | `PanAsync`, `GstinAsync`, `RcAsync`, `VoterIdAsync`, `DrivingLicenseAsync`\*, `PassportAsync`\*, `CinAsync`, `UdyamAsync`, `PanToGstinAsync`, `PanToUdyamAsync`, `BankAccountAsync` |
| `client.Services` | `ListAsync`, `GetAsync(serviceCode)` |
| `client.Estamp` | `GetSchemaAsync`, `ListStatesAsync`, `ListInstrumentsAsync(stateCode)`, `ListArticlesAsync(instrumentCode)`, `CalculateDutyAsync`, `AddPartiesAsync(orderId, ...)` |
| `client.Orders` | `CreateAsync`, `GetAsync`, `GetStatusAsync`, `GetPaymentAsync`, `ListDocumentsAsync`, `GetDocumentDownloadAsync(id, documentId)` |
| `client.Payments.Intents` | `CreateAsync`, `GetAsync` |
| `client.Usage` | `SummaryAsync` |
| `client.Logs` | `ListAsync`, `ListAllAsync` (`IAsyncEnumerable<LogEntry>`) |
| `client.Webhooks.Events` | `ListAsync`, `ReplayAsync` |

Add `using StampMitra.Models;` (models and request types) and `using StampMitra.Resources;` (`LogsListParams`).

\* Needs an IP-allowlisted key; otherwise a `PermissionDeniedException` carries the server message verbatim.

```csharp
// Verification: every call is billed, including valid = false results
var gst = await client.Verify.GstinAsync(new GstinVerifyRequest { Gstin = "27ABCDE1234F1Z5" }, ct);
var rc = await client.Verify.RcAsync(new VehicleRcVerifyRequest { VehicleNumber = "MH12AB1234" }, ct);
var bank = await client.Verify.BankAccountAsync(new BankAccountVerifyRequest { AccountNumber = "000123456789", Ifsc = "ICIC0001234", Name = "Rahul Kumar" }, ct);

// Services and e-Stamp reference data
ServiceList services = await client.Services.ListAsync(ct);          // enumerable of Service
GenericResponse states = await client.Estamp.ListStatesAsync(ct);    // reference data: read states.Data / states.Items
GenericResponse duty = await client.Estamp.CalculateDutyAsync(new EstampDutyCalculateRequest { StateCode = "MH", ArticleCode = "5", StampDutyAmount = 500 }, ct);

// Orders are addressed by the externalOrderId you supplied (and that the API returns)
OrderStatusResponse status = await client.Orders.GetStatusAsync(order.ExternalOrderId!, ct);
Console.WriteLine(status.Status);

// Usage and webhook history
UsageSummary usage = await client.Usage.SummaryAsync(ct);
GenericResponse events = await client.Webhooks.Events.ListAsync(20, null, null, ct);
```

Responses are immutable `record`s (`pan.RegisteredName`). Fields the SDK does not know are never dropped: they are kept in
`AdditionalProperties` (name to `JsonElement`). HTTP metadata (status, headers, request id, attempts) is on `model.Response`.
Responses whose shape the API does not fix (e-Stamp reference data, duty, parties, webhook events) are `GenericResponse`
(`Data`, `Get(name)`, `Items`, `NextCursor`, `RawJson`). `GET /v1/services` returns a bare array, exposed as `ServiceList`.

## Errors

All errors derive from `StampMitraException` (`Status`, `Code`, `Message`, `RequestId`, `Field`, `Body`):

| HTTP | Exception |
|------|-----------|
| 400 (and local validation) | `ValidationException` |
| 401 | `AuthenticationException` |
| 402 | `PaymentRequiredException` |
| 403 | `PermissionDeniedException` |
| 404 | `NotFoundException` |
| 409 | `ConflictException` |
| 429 | `RateLimitException` (`RetryAfter` when the header is present) |
| 5xx | `UpstreamException` |
| network failure / timeout | `ApiConnectionException` / `ApiTimeoutException` |
| bad webhook | `SignatureVerificationException` |

Any other status raises the base `StampMitraException`; non-JSON error bodies still map by status. Caller cancellation surfaces as
`OperationCanceledException`, never wrapped.

```csharp
try
{
    await client.Verify.GstinAsync(new GstinVerifyRequest { Gstin = "27ABCDE1234F1Z5" }, ct);
}
catch (RateLimitException ex)
{
    Console.WriteLine($"back off {ex.RetryAfter}");
}
catch (StampMitraException ex)
{
    Console.Error.WriteLine($"{ex.RequestId}: {ex.Message}");
}
```

## Retries and idempotency

`MaxRetries` defaults to 2, with exponential backoff and full jitter (0.5 s base, 8 s cap). `Retry-After` is honoured when sent
(capped at 60 s); the API itself currently sends none. Retried: 429, 502, 503, 504, connection errors and timeouts. GET is always
retry-safe. A POST is retried only when it carries an `Idempotency-Key`: the SDK generates a random UUIDv4 (from the operating
system CSPRNG) for **every POST except webhook replay** (verification, duty, parties, orders, payment intents), and reuses the **same key
across retries**, so a retried metered call replays the original result instead of billing twice. Pass your own with
`RequestOptions.IdempotencyKey`. Webhook replay is never retried unless you supply a key. Other statuses (400, 401, 402, 403, 404, 409, 500)
are never retried.

## Timeouts, cancellation and redirects

`Timeout` is applied per attempt with a linked `CancellationTokenSource`, so it works with any `HttpClient`; a timed-out attempt
raises `ApiTimeoutException` and is retried like any connection error when retry-safe. Cancelling your token stops the call and any
pending retry delay. The SDK never follows redirects (so credentials cannot leak to another host): a 3xx is reported as an error.
If you pass your own `HttpClient`, create it with `AllowAutoRedirect = false` and `Timeout = Timeout.InfiniteTimeSpan`
(`StampMitra.Extensions.DependencyInjection` does both).

## Pagination

```csharp
await foreach (var entry in client.Logs.ListAllAsync(new LogsListParams { Service = "pan.verify", Limit = 100 }, null, ct))
{
    Console.WriteLine($"{entry.Id} {entry.CreatedAt}");   // follows nextCursor lazily
}

LogsPage page = await client.Logs.ListAsync(new LogsListParams { Status = "failure", Limit = 50 }, ct);   // page.Items, page.NextCursor
```

## Webhooks

Deliveries carry `X-Webhook-Signature`, `X-Webhook-Timestamp` (unix seconds) and `X-Webhook-Event-Id`. The signature is
`hex(HMAC-SHA256(secret, "{timestamp}.{rawBody}"))` over the body **exactly as received**.

```csharp
using StampMitra.Webhooks;

// rawBody: the request body bytes, untouched (never parse and re-serialise); timestamp/signature/eventId: the headers above
WebhookEvent evt = WebhookSignature.ConstructEvent(rawBody, timestamp, signature, secret, eventId: eventId);

switch (evt)
{
    case OrderPaymentConfirmedEvent paid:
        Console.WriteLine($"paid: {paid.CoreOrderId}");
        break;
    case OrderDocumentReadyEvent ready:
        Console.WriteLine($"document ready: {ready.CoreOrderId}");
        break;
}

bool authentic = WebhookSignature.Verify(rawBody, timestamp, signature, secret, toleranceSeconds: 300);
```

`ConstructEvent` returns a typed record for each of the eight events (`OrderCreatedEvent`, `OrderPaymentConfirmedEvent`,
`OrderFulfilmentStartedEvent`, `OrderFulfilmentCompletedEvent`, `OrderFailedEvent`, `OrderCancelledEvent`,
`OrderRefundCompletedEvent`, `OrderDocumentReadyEvent`; `Type` is the documented name such as `order.created`) or an
`UnknownWebhookEvent`; extra fields are kept in `AdditionalProperties`. The comparison is constant-time, timestamps further than
300 s (configurable) from now are rejected, an empty secret is rejected, and failures raise `SignatureVerificationException`
(`Code` is `SIGNATURE_MISMATCH`, `INVALID_TIMESTAMP`, `TIMESTAMP_OUTSIDE_TOLERANCE`, `INVALID_PAYLOAD`, `MISSING_HEADERS` or `MISSING_SECRET`).
`ConstructEventFromHeaders` reads the three headers through a lookup. Deliveries are retried by the platform, so de-duplicate on `EventId`.
A complete ASP.NET Core receiver lives in [`examples/WebhookReceiver`](examples/WebhookReceiver/Program.cs).

## Validation

Inputs are checked before sending (PAN, GSTIN, IFSC, CIN, Udyam, vehicle number, 9-18 digit account number, passport file number,
driving licence, voter ID, phone, ISO dates), trimmed and upper-cased where the API does so, with a local `ValidationException`
(`Field` set) and no network call or billing. Separators such as `MH-12 AB 1234` are **not** stripped, exactly like the API. The
validators are public (`Validators.Pan(...)`) and can be disabled with `ValidateInput = false`.

```csharp
string pan = Validators.Pan(" abcde1234f ");   // "ABCDE1234F"; throws ValidationException when malformed
```

## Logging

Nothing is logged by default. Set `DebugLog` to receive one line per request and response. `Authorization`/`x-api-key` are
redacted, identifiers (PAN, GSTIN, account number, ...) are masked to their last four characters and names and dates of birth are
removed; `Redactor` is public if you need the same masking elsewhere. Treat logs as sensitive anyway.

```csharp
using var debugClient = new StampMitraClient(new StampMitraClientOptions { DebugLog = Console.Error.WriteLine });
```

## Dependency injection

```csharp
using StampMitra.Extensions.DependencyInjection;

services.AddStampMitra(o => o.ApiKey = configuration["StampMitra:ApiKey"]);
```

`AddStampMitra` lives in the `StampMitra.Extensions.DependencyInjection` namespace. It registers a singleton `StampMitraClient` on a named `IHttpClientFactory` client that does not follow redirects, and bridges the debug
lines to `ILogger` (category `StampMitra`, Debug level). The core package has no dependency on `Microsoft.Extensions.*`.

## Security

- Use keys server-side only; load them from the environment or a secret manager and never commit them.
- TLS verification is never disabled, redirects are never followed, and `BaseUrl` must be `https` (loopback `http` only, for mock servers).
- The API key and webhook secret never appear in `ToString()` output, exception messages, stack traces or debug logs (covered by tests).
- Path ids are validated and percent-encoded (empty values, `.`, `..` and slashes are rejected locally); header values are checked for CR/LF.
- Always verify webhook signatures on the raw body and keep the default 300 s tolerance.
- Report vulnerabilities as described in [SECURITY.md](SECURITY.md), not in public issues.

## Supported versions

| | |
|---|---|
| Runtime targets | `net6.0`, `net8.0` (CI runs the test suite on both) |
| Language | C# 9 or newer recommended (records and init-only properties); built with the .NET 8 SDK |
| Core dependencies | none (BCL only) |
| `StampMitra.Extensions.DependencyInjection` | `Microsoft.Extensions.Http` 8.x |

Only the latest minor release receives fixes while the SDK is pre-1.0. See [CHANGELOG.md](CHANGELOG.md).

## Examples

[`examples/ConsoleApp`](examples/ConsoleApp/Program.cs) (verify a PAN, create an order, read its status, page through logs) and
[`examples/WebhookReceiver`](examples/WebhookReceiver/Program.cs) (ASP.NET Core minimal API). Both are compiled by `dotnet build` and CI.
Point them at a mock server with `STAMPMITRA_BASE_URL=http://127.0.0.1:4010`.

## Development

```bash
dotnet build && dotnet test                         # unit + conformance suites (net6.0 and net8.0)
scripts/check-spec-coverage.sh                      # every operation in spec/openapi.json must have an SDK method
dotnet format StampMitra.sln --verify-no-changes    # formatting and analyzers
dotnet list package --vulnerable --include-transitive
```

`spec/openapi.json` is a snapshot of the public spec; the coverage test fails if any `METHOD + path` in it has no SDK method.
The conformance suite starts the spec repo's mock server (see [CONTRIBUTING.md](CONTRIBUTING.md)). See also
[RELEASING.md](RELEASING.md) and [SECURITY.md](SECURITY.md).

Licensed under the [MIT License](LICENSE). Copyright Bani Global Industries LLP (StampMitra).
