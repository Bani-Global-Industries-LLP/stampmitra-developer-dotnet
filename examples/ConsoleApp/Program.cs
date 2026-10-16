// Verify a PAN, create an order, read it back and page through the request logs.
//
//   export STAMPMITRA_API_KEY=sk_test_...        # sandbox key; server-side only
//   dotnet run --project examples/ConsoleApp
//
// STAMPMITRA_BASE_URL (optional) points the example at a mock server, e.g. http://127.0.0.1:4010.
using StampMitra;
using StampMitra.Models;
using StampMitra.Resources;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
var ct = cts.Token;

var options = new StampMitraClientOptions { AppInfo = new AppInfo("stampmitra-dotnet-example", "1.0") };
if (Environment.GetEnvironmentVariable("STAMPMITRA_BASE_URL") is { Length: > 0 } baseUrl)
{
    options.BaseUrl = baseUrl;
}

// ApiKey is read from STAMPMITRA_API_KEY when not set on the options.
using var client = new StampMitraClient(options);

try
{
    // 1. Verification. Every call is billed, including valid = false results.
    var pan = await client.Verify.PanAsync(new PanVerifyRequest { Pan = "ABCDE1234F", Name = "Rahul Kumar" }, ct);
    Console.WriteLine($"PAN valid={pan.Valid} name={pan.RegisteredName ?? "-"} match={pan.NameMatchResult ?? "-"} reference={pan.ReferenceId}");

    // 2. Create an order. An Idempotency-Key is generated for you and reused across automatic retries.
    var order = await client.Orders.CreateAsync(
        new CreateOrderRequest
        {
            ExternalOrderId = "ord_ext_123",
            ExternalCustomerId = "cust_ext_456",
            ServiceCode = "estamp.instrument.rent_agreement",
            Details = new Dictionary<string, object?> { ["state"] = "MH", ["stampValue"] = 500 },
        },
        ct);
    Console.WriteLine($"order externalOrderId={order.ExternalOrderId} (HTTP {order.Response?.StatusCode})");

    // 3. Orders are addressed by the externalOrderId, not by an internal id.
    var status = await client.Orders.GetStatusAsync(order.ExternalOrderId!, ct);
    Console.WriteLine($"status={status.Status}");

    // 4. Auto-pagination: ListAllAsync follows nextCursor lazily.
    var shown = 0;
    await foreach (var entry in client.Logs.ListAllAsync(new LogsListParams { Limit = 50 }, null, ct))
    {
        Console.WriteLine($"log {entry.Id} {entry.CreatedAt} {entry.Service}");
        if (++shown == 5)
        {
            break;
        }
    }
}
catch (RateLimitException ex)
{
    Console.Error.WriteLine($"Rate limited; retry after {ex.RetryAfter?.TotalSeconds.ToString("0") ?? "?"}s (request {ex.RequestId})");
    return 1;
}
catch (StampMitraException ex)
{
    Console.Error.WriteLine($"API error {ex.Status?.ToString() ?? "-"} {ex.Code}: {ex.Message} (request {ex.RequestId})");
    return 1;
}

return 0;
