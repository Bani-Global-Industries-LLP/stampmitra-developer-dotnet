// The e-Stamp flow: validate, create (with an idempotency key), upload the payer's PAN card, list the stamps and fetch certificates;
// then read the wallets.
//
//   export STAMPMITRA_API_KEY=sk_test_...        # sandbox key; server-side only
//   dotnet run --project examples/EstampOrder -- [path/to/pan.pdf]
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

var options = new StampMitraClientOptions { AppInfo = new AppInfo("stampmitra-dotnet-estamp-example", "1.0") };
if (Environment.GetEnvironmentVariable("STAMPMITRA_BASE_URL") is { Length: > 0 } baseUrl)
{
    options.BaseUrl = baseUrl;
}

using var client = new StampMitraClient(options);

try
{
    // 0. What does the state need? The requirements response includes a valid example body.
    var requirements = await client.Estamp.GetStateRequirementsAsync("MH", new EstampRequirementsParams { ArticleCode = "ART1", Denomination = 500, Category = EstampCategory.EStamp }, ct);
    Console.WriteLine($"fields for MH: {string.Join(", ", (requirements.Fields ?? new List<EstampRequirementsFieldsItem>()).Where(f => f.Required == true).Select(f => f.Field))}");

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
                SecondParty = "John Smith",   // a bare name converts to a party
                Purpose = "Rental Agreement",
            },
        },
    };

    // 1. Dry run: nothing is saved or charged.
    var check = await client.Estamp.Orders.ValidateAsync(body, ct);
    Console.WriteLine($"validate: {check.Status} (valid={check.Valid}); face value {check.Quote?.FaceValueTotal}");
    if (check.Valid != true)
    {
        foreach (var issue in check.Errors ?? new List<EstampValidationIssue>())
        {
            Console.Error.WriteLine($"  stamps[{issue.Index}] {issue.Field}: {issue.Message}");
        }

        return 1;
    }

    // 2. Create. The API requires an Idempotency-Key: pass your own (a stable id for this order) or let the SDK generate one.
    var order = await client.Estamp.Orders.CreateAsync(body, new RequestOptions { IdempotencyKey = "order-2026-0917" }, ct);
    Console.WriteLine($"order {order.Id} status={order.Status} stamps={order.StampCount}");

    // 3. An order can be placed without the payer's PAN card, but a PAN card is mandatory to complete the stamp. Upload it when you have it.
    if (args.Length > 0)
    {
        var file = await EstampDocumentFile.FromPathAsync(args[0], null, ct);
        var doc = await client.Estamp.Orders.UploadDocumentAsync(order.Id!, file, ct);
        Console.WriteLine($"uploaded {doc.FileName} ({doc.SizeBytes} bytes) -> {doc.Status}");
    }

    // 4. List the stamps and 5. look for each certificate.
    var stamps = await client.Estamp.Orders.ListStampsAsync(order.Id!, ct);
    foreach (var stamp in stamps.Items ?? new List<EstampStamp>())
    {
        var cert = await client.Estamp.Stamps.GetCertificateAsync(stamp.Id!, ct);
        Console.WriteLine($"stamp {stamp.Id} status={stamp.Status} pan={stamp.PanCard?.Status} certificate={(cert.Available == true ? cert.DownloadUrl : cert.Status)}");
    }

    // Wallets: both balances in one read. Balances are decimal strings.
    var wallet = await client.Estamp.GetWalletAsync(ct);
    Console.WriteLine($"Stamp duty wallet {wallet.Balance} {wallet.Currency}; Service wallet {wallet.ServiceFee?.Balance}");
}
catch (PaymentRequiredException ex)
{
    Console.Error.WriteLine($"Top up the {ex.Wallet} wallet, then send a NEW request (request {ex.RequestId})");
    return 1;
}
catch (ValidationException ex)
{
    foreach (var e in ex.Errors)
    {
        Console.Error.WriteLine($"  stamps[{e.Index}] {e.Field} {e.Code}: {e.Message}");
    }

    Console.Error.WriteLine($"{ex.Code}: {ex.Message} (request {ex.RequestId})");
    return 1;
}
catch (StampMitraException ex)
{
    Console.Error.WriteLine($"API error {ex.Status?.ToString() ?? "-"} {ex.Code}: {ex.Message} (request {ex.RequestId})");
    return 1;
}

return 0;
