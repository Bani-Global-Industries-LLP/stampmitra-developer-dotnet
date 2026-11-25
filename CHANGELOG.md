# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres
to [Semantic Versioning](https://semver.org/) (pre-1.0: minor versions may break).

## [Unreleased]

## [0.2.1] - 2026-11-25

Wording and snapshot refresh; targets API contract 1.2.1. No member, class or constant is renamed.

### Changed
- XML documentation and README describe one customer-facing **Service wallet** (the same balance pays for verification calls and for the service charges of an e-Stamp order; a top-up is a minimum of Rs 1,000 plus 18% GST, and orders debit fees before GST because GST on fees is paid at top-up) next to the **Stamp duty wallet**. `Wallets.ServiceFee`, `EstampWallet.ServiceFee` and `WalletNames.ServiceFee` (`SERVICE_FEE`) keep their names for compatibility.
- PAN card: an order can be placed without `PanCardImage`, but a PAN card is mandatory to complete the stamp. Validation is unchanged and the `PAN_CARD_RECOMMENDED` advisory keeps its name.
- Pricing wording: per paper, Rs 99 procurement + Rs 5 API charge + a convenience fee (default 5% of a paper's duty above Rs 1,000), duty multiplied by quantity; sandbox quotes use the same default rates as live (client-agreed rates are not applied in sandbox). The README rate tables match the published price book (PAN Rs 4, GSTIN Rs 3, two self-serve plans). New accounts get no signup credit.
- `spec/openapi.json` refreshed to the spec repo's 1.2.1 (documentation text only).

## [0.2.0] - 2026-11-24

Targets API contract 1.2.0 (62 operations, up from 31).

### Added
- e-Stamp reference data: `Estamp.GetStatesAvailabilityAsync`, `GetStateAsync`, `GetStateRequirementsAsync` (with `ExampleBody`), `ListDistrictsAsync`, `ListSrosAsync`, `GetWalletAsync`.
- Typed e-Stamp orders, `client.Estamp.Orders`: `ValidateAsync`, `CreateAsync` (an `Idempotency-Key` is generated when omitted), `ListAsync`/`ListAllAsync`, `GetAsync`, `CancelAsync`, `ExportAsync`, `ListStampsAsync`, `UploadDocumentAsync` (multipart PAN card upload, `EstampDocumentFile`).
- Single stamps, `client.Estamp.Stamps`: `GetAsync`, `CancelAsync`, `ReplaceAsync`, `SetPanCardAsync`, `GetCertificateAsync`.
- Wallets, `client.Wallets.StampDuty` and `client.Wallets.ServiceFee`: `GetBalanceAsync`, `TopUpAsync`, `ListTransactionsAsync`/`ListAllTransactionsAsync`, `ListInvoicesAsync`, `GetInvoiceAsync`; `client.Account`: `GetBalanceAsync`, `GetUsageAsync`.
- `EstampParty` (a `string` converts implicitly; reads a name or an object), `EstampStampLine`, `EstampPropertyArea` and constants for categories, genders and area units.
- `StampMitraException.Errors` (`error.details.errors[]`) and `Wallet` (`error.details.wallet`); `RetryAfter` on `ConflictException` and `UpstreamException`.
- Table-driven replay of the 1.2.0 golden fixtures through the SDK, and `examples/EstampOrder`.

### Changed
- `Retry-After` (seconds) is honoured on 429, 503 and 409 `IDEMPOTENCY_IN_PROGRESS`, which is now retried with the same key.
- HTTP 422 raises `ValidationException` (was the base `StampMitraException`).
- Webhook replay is never retried and never sent with an `Idempotency-Key`, even a caller-supplied one (it was retried when you passed a key).
- Order cancel, stamp cancel, stamp replace, PAN card link and both wallet top-ups are never retried automatically.
- `spec/openapi.json` and `retry-scenarios.json` refreshed to contract 1.2.0.

## [0.1.0] - 2026-10-16

### Added
- Initial release of `StampMitra` (targets `net6.0` and `net8.0`; `HttpClient` and `System.Text.Json` only):
  `Verify` (PAN, GSTIN, RC, Voter ID, driving licence, passport, CIN, Udyam, PAN-to-GSTIN, PAN-to-Udyam, bank account),
  `Services`, `Estamp`, `Orders`, `Payments.Intents`, `Usage`, `Logs` (with `ListAllAsync` returning `IAsyncEnumerable<LogEntry>`),
  `Webhooks.Events`. Async-first, `CancellationToken` everywhere, per-call `RequestOptions`.
- `WebhookSignature.Verify` / `Check` / `ConstructEvent` (HMAC-SHA256, constant-time, timestamp tolerance) with typed events for the eight `order.*` types.
- Typed immutable response models that preserve unknown fields (`AdditionalProperties`) and expose HTTP metadata (`Response`).
- Typed exception hierarchy, safe retries with full-jitter backoff, automatic idempotency keys (reused across retries) for every POST except webhook replay.
- Client-side validators and a redacting debug log sink.
- `StampMitra.Extensions.DependencyInjection`: `services.AddStampMitra(...)` on `IHttpClientFactory`.
- Bundled OpenAPI snapshot, a spec-coverage check, a conformance suite that runs against the spec repo's mock server, SourceLink and symbol packages.
- `BaseUrl` must be https (loopback http allowed), redirects are never followed, the API key and webhook secret never appear in `ToString()`, exceptions or logs.

[Unreleased]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/compare/v0.2.1...HEAD
[0.2.1]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/releases/tag/v0.1.0
