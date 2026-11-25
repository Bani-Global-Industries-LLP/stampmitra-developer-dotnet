# StampMitra .NET SDK

[![CI](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/StampMitra)](https://www.nuget.org/packages/StampMitra)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/LICENSE)

> The NuGet badge shows a version only once the package is published.

Official .NET client for the [StampMitra Developer API](https://developer.stampmitra.in/docs): Indian identity verification
(PAN, GSTIN, vehicle RC, Voter ID, driving licence, passport, CIN, Udyam, bank account) and e-Stamp Paper.
OpenAPI spec: <https://developer.stampmitra.in/openapi.json>.

> **Status: pre-1.0 (0.2.1, API contract 1.2.1).** The API surface may change in minor releases. Pin your version.

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
| `client.Estamp` | `GetSchemaAsync`, `ListStatesAsync`, `ListInstrumentsAsync(stateCode)`, `ListArticlesAsync(instrumentCode)`, `CalculateDutyAsync`, `AddPartiesAsync(orderId, ...)`, `GetStatesAvailabilityAsync`, `GetStateAsync(stateCode)`, `GetStateRequirementsAsync(stateCode, ...)`, `ListDistrictsAsync(stateCode)`, `ListSrosAsync(stateCode, districtCode)`, `GetWalletAsync` |
| `client.Estamp.Orders` | `ValidateAsync`, `CreateAsync`, `ListAsync`, `ListAllAsync`, `GetAsync`, `CancelAsync`, `ExportAsync`, `ListStampsAsync`, `UploadDocumentAsync` |
| `client.Estamp.Stamps` | `GetAsync`, `CancelAsync`, `ReplaceAsync`, `SetPanCardAsync`, `GetCertificateAsync` |
| `client.Wallets.StampDuty`, `client.Wallets.ServiceFee` | `GetBalanceAsync`, `TopUpAsync`, `ListTransactionsAsync`, `ListAllTransactionsAsync`, `ListInvoicesAsync`, `GetInvoiceAsync(id)` |
| `client.Account` | `GetBalanceAsync`, `GetUsageAsync` |
| `client.Orders` | `CreateAsync`, `GetAsync`, `GetStatusAsync`, `GetPaymentAsync`, `ListDocumentsAsync`, `GetDocumentDownloadAsync(id, documentId)` |
| `client.Payments.Intents` | `CreateAsync`, `GetAsync` |
| `client.Usage` | `SummaryAsync` |
| `client.Logs` | `ListAsync`, `ListAllAsync` (`IAsyncEnumerable<LogEntry>`) |
| `client.Webhooks.Events` | `ListAsync`, `ReplayAsync` |

Add `using StampMitra.Models;` (models and request types) and `using StampMitra.Resources;` (`LogsListParams`, `EstampDocumentFile`, `EstampRequirementsParams`, `EstampOrdersListParams`, `WalletListParams`).

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

## e-Stamp orders

`client.Estamp.Orders` takes typed stamp lines. One order covers **one state** (up to 5000 lines, `Quantity` up to 1000 per line). A party is
a bare name (a `string` converts implicitly to `EstampParty`) or an `EstampParty` object with `Address`, `Phone`, `FatherHusbandName` and
`Gender`; `GetStateRequirementsAsync` tells you which of these a state needs and returns a valid `ExampleBody`. An order can be placed
without the payer's PAN card, but **a PAN card is mandatory to complete the stamp** (until one is on file the stamp waits). Send it as `PanCardImage` (an https link), upload it to the order, or
link it later with `Stamps.SetPanCardAsync` (processed asynchronously: it answers 202 and the result appears in `stamp.PanCard`).

```csharp
var body = new EstampOrderRequest
{
    Reference = "order-2026-0917",
    Stamps = new[]
    {
        new EstampStampLine
        {
            ClientReference = "line-001",
            State = "MH",
            Category = EstampCategory.EStamp,
            ArticleCode = "ART1",
            Denomination = 500,
            FirstParty = new EstampParty { Name = "Jane Doe", Address = "12 MG Road, Pune" },
            SecondParty = "John Smith",
            Purpose = "Rental Agreement",
        },
    },
};

// 1. Dry run: nothing is saved or charged.
EstampValidateResult check = await client.Estamp.Orders.ValidateAsync(body, ct);
if (check.Valid != true)
{
    foreach (var issue in check.Errors ?? new List<EstampValidationIssue>())
    {
        Console.WriteLine($"stamps[{issue.Index}] {issue.Field}: {issue.Message}");
    }

    return;
}

// 2. Create. The Idempotency-Key is required by the API: the SDK generates one when you omit it and reuses it on retries.
EstampOrder order = await client.Estamp.Orders.CreateAsync(body, new RequestOptions { IdempotencyKey = "order-2026-0917" }, ct);

// 3. Upload the payer's PAN card (PDF, JPEG or PNG, at most 10 MB). Optional.
await client.Estamp.Orders.UploadDocumentAsync(order.Id!, await EstampDocumentFile.FromPathAsync("pan.pdf", null, ct), ct);

// 4. List the stamps and 5. fetch the certificate of each one once it has been delivered.
EstampStampList stamps = await client.Estamp.Orders.ListStampsAsync(order.Id!, ct);
foreach (var stamp in stamps.Items ?? new List<EstampStamp>())
{
    EstampCertificate cert = await client.Estamp.Stamps.GetCertificateAsync(stamp.Id!, ct);
    Console.WriteLine(cert.Available == true ? cert.DownloadUrl : $"{stamp.Id}: {cert.Status}");
}
```

Order creation debits the duty from your Stamp duty wallet and the service charges from your Service wallet (₹99 procurement + ₹5 API charge per paper plus a convenience fee, default 5% of a paper's duty above ₹1,000; the duty is multiplied by `Quantity`; sandbox quotes use the same default rates as live, client-agreed rates are not applied in sandbox); the returned `Status` is a
pre-placement review status. `CancelAsync` (order) and `Stamps.CancelAsync`, `ReplaceAsync` and `SetPanCardAsync` are **never retried
automatically**. A rejected order body raises a `ValidationException` whose `Errors` lists every finding (`Field`, `Code`, `Message`, `Index`);
a 402 raises a `PaymentRequiredException` whose `Wallet` is `WalletNames.StampDuty` or `WalletNames.ServiceFee`:

```csharp
try
{
    await client.Estamp.Orders.CreateAsync(body, ct);
}
catch (ValidationException ex) when (ex.Errors.Count > 0)
{
    foreach (var e in ex.Errors)
    {
        Console.Error.WriteLine($"stamps[{e.Index}] {e.Field} {e.Code}: {e.Message}");
    }
}
catch (PaymentRequiredException ex)
{
    Console.Error.WriteLine($"top up the {ex.Wallet} wallet first");
}
```

### Wallets

There are two prepaid wallets: the **Stamp duty wallet** (statutory duty) and the **Service wallet** (one balance that pays for verification calls and for the procurement and API charges of an
e-Stamp order, net of GST; a top-up is a minimum of ₹1,000 plus 18% GST, and orders debit fees before GST because GST on fees is paid at top-up). The service-fee names (`Wallets.ServiceFee`, the `ServiceFee` member of `EstampWallet`, `WalletNames.ServiceFee` = `SERVICE_FEE`) are kept for compatibility. Balances are decimal strings; parse them, never concatenate. A Stamp duty top-up adds a **6% processing fee plus 18% GST on
that fee** to what the payer pays; the wallet is credited the amount you add (`CreditAmount`), and a GST tax invoice for the fee is available from
`Wallets.StampDuty.ListInvoicesAsync` / `GetInvoiceAsync` (the invoice id is the top-up's `TopupId`). A Service wallet top-up adds 18% GST on top of the amount.
The SDK never computes fees: read `ProcessingFeeAmount`, `GstAmount`, `TotalCharged` and `CreditAmount` from the response. Top-ups are never retried automatically.

```csharp
StampDutyWalletTopup topup = await client.Wallets.StampDuty.TopUpAsync(new WalletTopupRequest { BaseAmount = 1000 }, ct);
Console.WriteLine($"pay {topup.TotalCharged} (fee {topup.ProcessingFeeAmount} + GST {topup.GstAmount}); wallet credited {topup.CreditAmount}");

WalletBalance balance = await client.Wallets.ServiceFee.GetBalanceAsync(ct);
StampDutyWalletInvoicesPage invoices = await client.Wallets.StampDuty.ListInvoicesAsync(null, ct);
```

The top-up response carries the payment order to complete in checkout (live keys); a sandbox top-up credits fake money at once.

## Errors

All errors derive from `StampMitraException` (`Status`, `Code`, `Message`, `RequestId`, `Field`, `Body`, plus `Errors` (`error.details.errors[]`) and `Wallet` (`error.details.wallet`)):

| HTTP | Exception |
|------|-----------|
| 400, 422 (and local validation) | `ValidationException` |
| 401 | `AuthenticationException` |
| 402 | `PaymentRequiredException` (`Wallet` names the wallet that is short) |
| 403 | `PermissionDeniedException` |
| 404 | `NotFoundException` |
| 409 | `ConflictException` (`RetryAfter`) |
| 429 | `RateLimitException` (`RetryAfter` when the header is present) |
| 5xx | `UpstreamException` (`RetryAfter`) |
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

`MaxRetries` defaults to 2, with exponential backoff and full jitter (0.5 s base, 8 s cap). `Retry-After` (seconds) is honoured on 429, 503 and
409 `IDEMPOTENCY_IN_PROGRESS` (capped at 60 s); without the header the computed backoff applies. Retried: 429, 502, 503, 504, connection errors,
timeouts and a 409 `IDEMPOTENCY_IN_PROGRESS`. GET is always retry-safe. A POST is retried only when it carries an `Idempotency-Key`: the SDK
generates a random UUIDv4 (from the operating system CSPRNG) for the POSTs that accept one (verification, duty, parties, orders, e-Stamp order
creation and document upload, payment intents) and reuses the **same key across retries**, so a retried metered call replays the original result
instead of billing twice. Pass your own with `RequestOptions.IdempotencyKey`. `Estamp.Orders.ValidateAsync` is a dry run and is retried without a key.
**Never retried automatically** (and never sent with a key, even a caller-supplied one): order cancel, stamp cancel, stamp replace, PAN card link,
both wallet top-ups and webhook replay. Other statuses (400, 401, 402, 403, 404, 409 with any other code, 422, 500) are never retried.

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
A complete ASP.NET Core receiver lives in [`examples/WebhookReceiver`](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/examples/WebhookReceiver/Program.cs).

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

## Pricing

The SDK is free and MIT-licensed. You pay only for successful API calls, from one prepaid **Service wallet**: the same balance pays for verification calls and for the service charges of an e-Stamp order. A Service wallet top-up is a minimum of ₹1,000 plus 18% GST; GST on fees is paid at top-up, so an order debits the fees before GST. New accounts get no signup credit. Sandbox is free: mock data on every endpoint, and it never debits your wallet.

Per-call rates in rupees, excluding GST (the same on both self-serve plans):

| Product | Rate per successful call |
|---|---|
| PAN | ₹4 |
| GSTIN | ₹3 |
| Voter ID, Driving Licence, CIN, PAN to GSTIN | ₹5 |
| Udyam | ₹8.50 |
| PAN to Udyam | ₹20 |
| Passport and Vehicle RC | ₹9 |
| Bank account (penny-drop) | ₹4 |
| e-Stamp API charge (per paper, see below) | ₹5 |

| | Pay as you go | Business |
|---|---|---|
| Plan fee | ₹0 / month | ₹999 / month (₹9,990 / year) |
| Seats / API keys / webhooks | 3 / 5 / 3 | 10 / 20 / 10 |
| Requests per minute | 60 | 300 |
| Daily limit per API | 100 | 500 |
| Log retention | 30 days | 90 days |
| IP allowlisting | No | Yes |

An e-Stamp order is priced per paper: ₹99 procurement + ₹5 API charge + a convenience fee (default 5% of a paper's duty above ₹1,000), and the duty is multiplied by quantity. The duty is paid from the **Stamp duty wallet**; the service charges are paid from the Service wallet. Sandbox quotes use the same default rates as live; client-agreed rates are not applied in sandbox. There are separate Startup and NGO programmes. Plans, rates and limits can change, and [developer.stampmitra.in/pricing](https://developer.stampmitra.in/pricing) is the source of truth.

## Security

- Use keys server-side only; load them from the environment or a secret manager and never commit them.
- TLS verification is never disabled, redirects are never followed, and `BaseUrl` must be `https` (loopback `http` only, for mock servers).
- The API key and webhook secret never appear in `ToString()` output, exception messages, stack traces or debug logs (covered by tests).
- Path ids are validated and percent-encoded (empty values, `.`, `..` and slashes are rejected locally); header values are checked for CR/LF.
- Always verify webhook signatures on the raw body and keep the default 300 s tolerance.
- Report vulnerabilities as described in [SECURITY.md](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/SECURITY.md), not in public issues.

## Supported versions

| | |
|---|---|
| Runtime targets | `net6.0`, `net8.0` (CI runs the test suite on both) |
| Language | C# 9 or newer recommended (records and init-only properties); built with the .NET 8 SDK |
| Core dependencies | none (BCL only) |
| `StampMitra.Extensions.DependencyInjection` | `Microsoft.Extensions.Http` 8.x |

Only the latest minor release receives fixes while the SDK is pre-1.0. See [CHANGELOG.md](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/CHANGELOG.md).

## Examples

[`examples/ConsoleApp`](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/examples/ConsoleApp/Program.cs) (verify a PAN, create an order, read its status, page through logs),
[`examples/EstampOrder`](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/examples/EstampOrder/Program.cs) (validate, create, upload the PAN card, list stamps, certificate, wallets) and
[`examples/WebhookReceiver`](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/examples/WebhookReceiver/Program.cs) (ASP.NET Core minimal API). All are compiled by `dotnet build` and CI.
Point them at a mock server with `STAMPMITRA_BASE_URL=http://127.0.0.1:4010`.

## Development

```bash
dotnet build && dotnet test                         # unit + conformance suites (net6.0 and net8.0)
scripts/check-spec-coverage.sh                      # every operation in spec/openapi.json must have an SDK method
dotnet format StampMitra.sln --verify-no-changes    # formatting and analyzers
dotnet list package --vulnerable --include-transitive
```

`spec/openapi.json` is a snapshot of the public spec; the coverage test fails if any `METHOD + path` in it has no SDK method.
The conformance suite starts the spec repo's mock server (see [CONTRIBUTING.md](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/CONTRIBUTING.md)). See also
[RELEASING.md](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/RELEASING.md) and [SECURITY.md](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/SECURITY.md).

Licensed under the [MIT License](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/blob/main/LICENSE). Copyright Bani Global Industries LLP (StampMitra).
