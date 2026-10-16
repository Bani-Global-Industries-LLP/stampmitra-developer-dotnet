// ASP.NET Core minimal API receiver for StampMitra webhooks.
//
//   export STAMPMITRA_WEBHOOK_SECRET=whsec_...
//   dotnet run --project examples/WebhookReceiver --urls http://127.0.0.1:5080
//
// The signature covers the raw request bytes, so the body is read untouched and never re-serialised.
using StampMitra;
using StampMitra.Webhooks;

var secret = Environment.GetEnvironmentVariable("STAMPMITRA_WEBHOOK_SECRET");
if (string.IsNullOrEmpty(secret))
{
    Console.Error.WriteLine("Set STAMPMITRA_WEBHOOK_SECRET to the endpoint's signing secret.");
    return 1;
}

var app = WebApplication.CreateBuilder(args).Build();

app.MapPost("/webhooks/stampmitra", async (HttpRequest request, ILogger<Program> logger) =>
{
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var raw = ms.ToArray();

    WebhookEvent evt;
    try
    {
        evt = WebhookSignature.ConstructEventFromHeaders(raw, name => request.Headers[name].FirstOrDefault(), secret);
    }
    catch (SignatureVerificationException ex)
    {
        logger.LogWarning("Rejected webhook: {Code}", ex.Code);
        return Results.BadRequest(new { error = "invalid signature" });
    }

    // Deliveries are retried: de-duplicate on evt.EventId before doing any work.
    switch (evt)
    {
        case OrderPaymentConfirmedEvent paid:
            logger.LogInformation("Payment confirmed for order {Order}", paid.CoreOrderId);
            break;
        case OrderDocumentReadyEvent ready:
            logger.LogInformation("Document ready for order {Order}", ready.CoreOrderId);
            break;
        default:
            logger.LogInformation("{Type} for order {Order} (event {EventId})", evt.Type ?? evt.CoreEventType, evt.CoreOrderId, evt.EventId);
            break;
    }

    return Results.Ok(new { received = true });
});

await app.RunAsync();
return 0;

// Makes ILogger<Program> resolvable in a top-level-statements program.
public partial class Program
{
}
