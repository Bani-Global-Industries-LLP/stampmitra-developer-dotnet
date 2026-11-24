using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>e-Stamp orders: validate, create, list, cancel, export, stamps and the PAN card upload.</summary>
public sealed class EstampOrdersResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal EstampOrdersResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Validates an order without saving or charging anything (POST /v1/estamp/orders/validate). A dry run: safe to retry automatically. Requires the <c>estamp.orders.validate</c> scope.</summary>
    /// <param name="request">The order.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampValidateResult> ValidateAsync(EstampOrderRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate)
        {
            EstampOrdersResource.CheckOrder(request);
        }

        return await _transport.RequestAsync<EstampValidateResult>(HttpMethod.Post, "/v1/estamp/orders/validate", null, null, request, options, false, null, cancellationToken, retryMode: RetryMode.Safe).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ValidateAsync(EstampOrderRequest, RequestOptions, CancellationToken)"/>
    public Task<EstampValidateResult> ValidateAsync(EstampOrderRequest request, CancellationToken cancellationToken) => ValidateAsync(request, null, cancellationToken);

    /// <summary>Creates an order (pending internal review); the Stamp Duty and Service Fee wallets are debited (POST /v1/estamp/orders). An Idempotency-Key is generated unless you pass one and reused across automatic retries. Requires the <c>estamp.orders.create</c> scope.</summary>
    /// <param name="request">The order.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampOrder> CreateAsync(EstampOrderRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate)
        {
            CheckOrder(request);
        }

        return await _transport.RequestAsync<EstampOrder>(HttpMethod.Post, "/v1/estamp/orders", null, null, request, options, true, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CreateAsync(EstampOrderRequest, RequestOptions, CancellationToken)"/>
    public Task<EstampOrder> CreateAsync(EstampOrderRequest request, CancellationToken cancellationToken) => CreateAsync(request, null, cancellationToken);

    /// <summary>Fetches one page of your organization's orders, newest first (GET /v1/estamp/orders). Requires the <c>estamp.orders.read</c> scope.</summary>
    /// <param name="parameters">Paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampOrderList> ListAsync(EstampOrdersListParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampOrderList>(HttpMethod.Get, "/v1/estamp/orders", null, OrdersQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListAsync(EstampOrdersListParams?, RequestOptions, CancellationToken)"/>
    public Task<EstampOrderList> ListAsync(EstampOrdersListParams? parameters, CancellationToken cancellationToken) => ListAsync(parameters, null, cancellationToken);

    /// <summary>Gets an order (GET /v1/estamp/orders/{id}). Requires the <c>estamp.orders.read</c> scope.</summary>
    /// <param name="id">The order id (<c>EstampOrder.Id</c>).</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampOrder> GetAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampOrder>(HttpMethod.Get, "/v1/estamp/orders/{id}", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampOrder> GetAsync(string id, CancellationToken cancellationToken) => GetAsync(id, null, cancellationToken);

    /// <summary>Cancels an order that is still before placement (POST /v1/estamp/orders/{id}/cancel). Never retried automatically and sent without an Idempotency-Key. Requires the <c>estamp.orders.cancel</c> scope.</summary>
    /// <param name="id">The order id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampOrder> CancelAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampOrder>(HttpMethod.Post, "/v1/estamp/orders/{id}/cancel", new string?[] { id }, null, null, options, false, null, cancellationToken, retryMode: RetryMode.Never, noBody: true).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CancelAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampOrder> CancelAsync(string id, CancellationToken cancellationToken) => CancelAsync(id, null, cancellationToken);

    /// <summary>Exports an order as CSV text (GET /v1/estamp/orders/{id}/export). Requires the <c>estamp.orders.read</c> scope.</summary>
    /// <param name="id">The order id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampOrderExport> ExportAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampOrderExport>(HttpMethod.Get, "/v1/estamp/orders/{id}/export", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ExportAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampOrderExport> ExportAsync(string id, CancellationToken cancellationToken) => ExportAsync(id, null, cancellationToken);

    /// <summary>Lists the stamps of an order (GET /v1/estamp/orders/{id}/stamps). Requires the <c>estamp.orders.read</c> scope.</summary>
    /// <param name="id">The order id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampStampList> ListStampsAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampStampList>(HttpMethod.Get, "/v1/estamp/orders/{id}/stamps", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListStampsAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampStampList> ListStampsAsync(string id, CancellationToken cancellationToken) => ListStampsAsync(id, null, cancellationToken);

    /// <summary>Uploads the payer's PAN card (PDF, JPEG or PNG, at most 10 MB) as multipart/form-data; advisory, never blocking (POST /v1/estamp/orders/{orderId}/documents). An Idempotency-Key is generated unless you pass one and reused across automatic retries. Requires the <c>estamp.documents.write</c> scope.</summary>
    /// <param name="orderId">The order id.</param>
    /// <param name="file">The file.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampDocument> UploadDocumentAsync(string orderId, EstampDocumentFile file, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (_validate)
        {
            file.Check();
        }

        return await _transport.RequestAsync<EstampDocument>(HttpMethod.Post, "/v1/estamp/orders/{orderId}/documents", new string?[] { orderId }, null, null, options, true, null, cancellationToken, multipart: file.CreateContent).ConfigureAwait(false);
    }

    /// <inheritdoc cref="UploadDocumentAsync(string, EstampDocumentFile, RequestOptions, CancellationToken)"/>
    public Task<EstampDocument> UploadDocumentAsync(string orderId, EstampDocumentFile file, CancellationToken cancellationToken) => UploadDocumentAsync(orderId, file, null, cancellationToken);

    /// <summary>Streams every order, following <c>nextCursor</c> lazily until the last page.</summary>
    /// <param name="parameters">Paging; a starting cursor is honoured.</param>
    /// <param name="options">Per-call overrides applied to every page request.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>An asynchronous sequence of orders.</returns>
    public async IAsyncEnumerable<EstampOrder> ListAllAsync(EstampOrdersListParams? parameters = null, RequestOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = parameters ?? new EstampOrdersListParams();
        while (true)
        {
            var page = await ListAsync(current, options, cancellationToken).ConfigureAwait(false);
            if (page.Items is not null)
            {
                foreach (var order in page.Items)
                {
                    yield return order;
                }
            }

            var next = page.NextCursor;
            if (string.IsNullOrEmpty(next))
            {
                yield break;
            }

            if (string.Equals(next, current.Cursor, StringComparison.Ordinal))
            {
                throw new StampMitraException("The API returned the same pagination cursor twice; stopping to avoid an infinite loop.", 200, "INVALID_RESPONSE", page.Response?.RequestId, null, null);
            }

            current = current with { Cursor = next };
        }
    }

    private static List<KeyValuePair<string, string?>> OrdersQuery(EstampOrdersListParams? p) => new()
    {
        new("page", p?.Page?.ToString(CultureInfo.InvariantCulture)),
        new("limit", p?.Limit?.ToString(CultureInfo.InvariantCulture)),
        new("cursor", p?.Cursor),
    };

    internal static void CheckOrder(EstampOrderRequest request)
    {
        if (request.Stamps is null || request.Stamps.Count == 0 || request.Stamps.Count > 5000)
        {
            throw ValidationException.ForField("stamps", "stamps must contain 1 to 5000 stamp lines");
        }

        string? state = null;
        for (var i = 0; i < request.Stamps.Count; i++)
        {
            var line = request.Stamps[i] ?? throw ValidationException.ForField("stamps[" + i.ToString(CultureInfo.InvariantCulture) + "]", "stamp line must not be null");
            var at = "stamps[" + i.ToString(CultureInfo.InvariantCulture) + "]";
            if (line.Quantity is < 1 or > 1000)
            {
                throw ValidationException.ForField(at + ".quantity", "quantity must be between 1 and 1000");
            }

            if (!string.IsNullOrWhiteSpace(line.State))
            {
                if (state is not null && !string.Equals(state, line.State, StringComparison.OrdinalIgnoreCase))
                {
                    throw ValidationException.ForField(at + ".state", "all stamp lines of one order must share one state");
                }

                state ??= line.State;
            }
        }
    }
}

/// <summary>Optional filters for <see cref="EstampResource.GetStateRequirementsAsync(string, EstampRequirementsParams, RequestOptions, System.Threading.CancellationToken)"/>.</summary>
public sealed record EstampRequirementsParams
{
    /// <summary>Article code (up to 60 characters).</summary>
    public string? ArticleCode { get; init; }

    /// <summary>Denomination in rupees (greater than 0).</summary>
    public decimal? Denomination { get; init; }

    /// <summary>One of <see cref="EstampCategory"/>.</summary>
    public string? Category { get; init; }
}

/// <summary>Paging for <see cref="EstampOrdersResource"/>.</summary>
public sealed record EstampOrdersListParams
{
    /// <summary>Page number (1 or more).</summary>
    public int? Page { get; init; }

    /// <summary>Page size, 1 to 100 (the API default is 20).</summary>
    public int? Limit { get; init; }

    /// <summary>Cursor from a previous page (<see cref="EstampOrderList.NextCursor"/>).</summary>
    public string? Cursor { get; init; }
}

/// <summary>Paging for the wallet ledger and invoice lists.</summary>
public sealed record WalletListParams
{
    /// <summary>Page size (1 or more; the API default is 20).</summary>
    public int? Limit { get; init; }

    /// <summary>Cursor from a previous page (<c>NextCursor</c>).</summary>
    public string? Cursor { get; init; }
}

/// <summary>A file to upload with <see cref="EstampOrdersResource.UploadDocumentAsync(string, EstampDocumentFile, RequestOptions, System.Threading.CancellationToken)"/>. The bytes are held in memory so a retry re-sends the whole body.</summary>
public sealed class EstampDocumentFile
{
    /// <summary>The largest file the API accepts (10 MB).</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    private readonly byte[] _content;

    private EstampDocumentFile(byte[] content, string fileName, string? contentType)
    {
        _content = content;
        FileName = fileName;
        ContentType = contentType ?? Infer(fileName);
    }

    /// <summary>The file name sent with the upload.</summary>
    public string FileName { get; }

    /// <summary>The declared content type (<c>application/pdf</c>, <c>image/jpeg</c> or <c>image/png</c>).</summary>
    public string ContentType { get; }

    /// <summary>The size in bytes.</summary>
    public int Length => _content.Length;

    /// <summary>Wraps bytes.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name, e.g. <c>pan.pdf</c>.</param>
    /// <param name="contentType">The content type; inferred from the extension when omitted.</param>
    /// <returns>The file.</returns>
    public static EstampDocumentFile FromBytes(byte[] content, string fileName, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        return new EstampDocumentFile(content, fileName, contentType);
    }

    /// <summary>Reads a stream to memory (stopping early when it exceeds the 10 MB limit).</summary>
    /// <param name="content">The stream.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="contentType">The content type; inferred from the extension when omitted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file.</returns>
    public static async Task<EstampDocumentFile> FromStreamAsync(Stream content, string fileName, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int n;
        while ((n = await content.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            ms.Write(buffer, 0, n);
            if (ms.Length > MaxBytes)
            {
                throw ValidationException.ForField("file", "file must be at most 10 MB");
            }
        }

        return new EstampDocumentFile(ms.ToArray(), fileName, contentType);
    }

    /// <summary>Reads a file from disk.</summary>
    /// <param name="path">The path.</param>
    /// <param name="contentType">The content type; inferred from the extension when omitted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file.</returns>
    public static async Task<EstampDocumentFile> FromPathAsync(string path, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        var info = new FileInfo(path);
        if (info.Exists && info.Length > MaxBytes)
        {
            throw ValidationException.ForField("file", "file must be at most 10 MB");
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new EstampDocumentFile(bytes, Path.GetFileName(path), contentType);
    }

    internal void Check()
    {
        if (_content.Length == 0)
        {
            throw ValidationException.ForField("file", "file must not be empty");
        }

        if (_content.Length > MaxBytes)
        {
            throw ValidationException.ForField("file", "file must be at most 10 MB");
        }

        if (ContentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            throw ValidationException.ForField("file", "only PDF, JPEG and PNG files are accepted");
        }
    }

    internal HttpContent CreateContent()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(_content);
        file.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        var safe = new string(FileName.Select(ch => ch is '"' or '\\' or '\r' or '\n' or < ' ' ? '_' : ch).ToArray());
        var disposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileName = "\"" + safe + "\"" };
        if (safe.Any(ch => ch > '~'))
        {
            disposition.FileNameStar = safe;
        }

        file.Headers.ContentDisposition = disposition;
        form.Add(file);
        var type = new StringContent("pan_card");
        type.Headers.ContentType = null;
        type.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"type\"" };
        form.Add(type);
        return form;
    }

    private static string Infer(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
    }
}
