# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres
to [Semantic Versioning](https://semver.org/) (pre-1.0: minor versions may break).

## [Unreleased]

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

[Unreleased]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet/releases/tag/v0.1.0
