# StampMitra.Extensions.DependencyInjection

`IHttpClientFactory` / `Microsoft.Extensions.DependencyInjection` integration for the
[StampMitra .NET SDK](https://github.com/Bani-Global-Industries-LLP/stampmitra-developer-dotnet).

```csharp
using StampMitra;
using StampMitra.Extensions.DependencyInjection;
using StampMitra.Models;

builder.Services.AddStampMitra(o => o.ApiKey = builder.Configuration["StampMitra:ApiKey"]);

// anywhere:
public sealed class Kyc(StampMitraClient client)
{
    public Task<PanVerificationResult> CheckAsync(string pan, CancellationToken ct) =>
        client.Verify.PanAsync(new PanVerifyRequest { Pan = pan }, ct);
}
```

The client is registered as a singleton on a named `HttpClient` that never follows redirects. The SDK applies its own
per-attempt timeout and retries. When the `ILogger` category `StampMitra` has Debug enabled, the SDK's redacted request
log lines are written to it. Server-side only: never ship `sk_` keys in apps.
