# Contributing

Thanks for helping improve the StampMitra .NET SDK.

## Setup

You need the .NET 8 SDK (`global.json` pins the feature band) and, to run the tests on `net6.0`, the .NET 6 runtime.

```bash
dotnet build                                        # warnings are errors
dotnet test                                         # unit + conformance suites on net6.0 and net8.0
scripts/check-readme-snippets.sh                    # every README ```csharp block must be mirrored in tests/StampMitra.Tests/ReadmeSnippets.cs
scripts/check-spec-coverage.sh                      # every operation in spec/openapi.json must have an SDK method
dotnet format StampMitra.sln --verify-no-changes    # formatting and analyzers (run `dotnet format StampMitra.sln` to fix)
dotnet list package --vulnerable --include-transitive
dotnet test --collect:"XPlat Code Coverage"         # coverlet
```

### Conformance suite

`tests/StampMitra.Tests/ConformanceTests.cs` drives all 31 operations, the error statuses, retries, idempotency, timeouts, pagination
and the shared fixtures against the spec repo's mock server (needs Node.js):

```bash
git clone https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-openapi ../stampmitra-developer-openapi
export STAMPMITRA_SPEC_DIR=$PWD/../stampmitra-developer-openapi    # default: /home/user/stampmitra-developer-openapi
dotnet test --filter Category=Conformance
```

The test fixture starts `node $STAMPMITRA_SPEC_DIR/scripts/mock-server.mjs` on a free port (`STAMPMITRA_NODE` overrides the `node` binary).
When the spec directory or Node is missing the conformance tests are skipped; CI sets `CONFORMANCE_REQUIRED=1`, which makes that a failure.
CI checks out `Bani-Global-Industries-LLP/stampmitra-developer-openapi` (default branch), so that repository needs its `main` branch to exist.

`tests/StampMitra.Tests/fixtures/*.json` are copies of the shared fixtures in the spec repo (validators, webhook vectors, retry scenarios,
webhook examples): refresh them when it changes. `spec/openapi.json` is a snapshot of the public spec; when the API changes, refresh it,
regenerate or add the models and methods, and the coverage test will fail until you do.

## Guidelines

- Public members need XML documentation (`GenerateDocumentationFile` with warnings as errors enforces it).
- `Models/Models.g.cs`, `Models/Requests.g.cs` and `Resources/VerifyResource.g.cs` were generated from the spec; edit them carefully and keep them consistent with it.
- The core package must stay dependency-free: `HttpClient` and `System.Text.Json` only. Integrations belong in separate packages.
- Every `csharp` block in the README is compiled through `tests/StampMitra.Tests/ReadmeSnippets.cs`; update both together.
- Never log or commit real keys or personal data; tests use fake handlers or the local mock server, never the real API.
- Update `CHANGELOG.md` under *Unreleased*.
- By contributing you agree your work is licensed under the MIT license.

Security issues: see [SECURITY.md](SECURITY.md), do not open a public issue.
