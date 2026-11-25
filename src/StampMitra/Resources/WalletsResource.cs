using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>The two e-Stamp wallets.</summary>
public sealed class WalletsResource
{
    internal WalletsResource(Transport transport, bool validate)
    {
        StampDuty = new StampDutyWalletResource(transport, validate);
        ServiceFee = new ServiceFeeWalletResource(transport, validate);
    }

    /// <summary>The Stamp duty wallet (pays the stamp duty).</summary>
    public StampDutyWalletResource StampDuty { get; }

    /// <summary>The Service wallet: one balance that pays for verification calls and for the service charges of an e-Stamp order. The property keeps its service-fee name for compatibility.</summary>
    public ServiceFeeWalletResource ServiceFee { get; }

    internal static List<KeyValuePair<string, string?>> PageQuery(WalletListParams? p) => new()
    {
        new("limit", p?.Limit?.ToString(CultureInfo.InvariantCulture)),
        new("cursor", p?.Cursor),
    };
}

/// <summary>The Stamp Duty Wallet (statutory duty). A top-up adds a 6% processing fee plus 18% GST on that fee to what the payer pays; the wallet is credited the amount you add. Read the fee, GST and total from the response.</summary>
public sealed class StampDutyWalletResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal StampDutyWalletResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Gets the wallet balance (a decimal string) (GET /v1/stamp-duty-wallet). Requires the <c>stamp-duty-wallet.read</c> scope.</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletBalance> GetBalanceAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletBalance>(HttpMethod.Get, "/v1/stamp-duty-wallet", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetBalanceAsync(RequestOptions, CancellationToken)"/>
    public Task<WalletBalance> GetBalanceAsync(CancellationToken cancellationToken) => GetBalanceAsync(null, cancellationToken);

    /// <summary>Creates a payment order to top up the wallet. Amounts come from the response; the SDK never computes fees (POST /v1/stamp-duty-wallet/topup). Never retried automatically and sent without an Idempotency-Key. Requires the <c>stamp-duty-wallet.topup</c> scope.</summary>
    /// <param name="request">The amount to credit, without fee or tax.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<StampDutyWalletTopup> TopUpAsync(WalletTopupRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate && request.BaseAmount is null or <= 0)
        {
            throw ValidationException.ForField("baseAmount", "baseAmount must be a positive number");
        }

        return await _transport.RequestAsync<StampDutyWalletTopup>(HttpMethod.Post, "/v1/stamp-duty-wallet/topup", null, null, request, options, false, null, cancellationToken, retryMode: RetryMode.Never).ConfigureAwait(false);
    }

    /// <inheritdoc cref="TopUpAsync(WalletTopupRequest, RequestOptions, CancellationToken)"/>
    public Task<StampDutyWalletTopup> TopUpAsync(WalletTopupRequest request, CancellationToken cancellationToken) => TopUpAsync(request, null, cancellationToken);

    /// <summary>Fetches one page of the wallet ledger, newest first (GET /v1/stamp-duty-wallet/transactions). Requires the <c>stamp-duty-wallet.read</c> scope.</summary>
    /// <param name="parameters">Paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletTransactionsPage> ListTransactionsAsync(WalletListParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletTransactionsPage>(HttpMethod.Get, "/v1/stamp-duty-wallet/transactions", null, WalletsResource.PageQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListTransactionsAsync(WalletListParams?, RequestOptions, CancellationToken)"/>
    public Task<WalletTransactionsPage> ListTransactionsAsync(WalletListParams? parameters, CancellationToken cancellationToken) => ListTransactionsAsync(parameters, null, cancellationToken);

    /// <summary>Fetches one page of top-up GST tax invoices, newest first (GET /v1/stamp-duty-wallet/invoices). Requires the <c>stamp-duty-wallet.invoices.read</c> scope.</summary>
    /// <param name="parameters">Paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<StampDutyWalletInvoicesPage> ListInvoicesAsync(WalletListParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<StampDutyWalletInvoicesPage>(HttpMethod.Get, "/v1/stamp-duty-wallet/invoices", null, WalletsResource.PageQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListInvoicesAsync(WalletListParams?, RequestOptions, CancellationToken)"/>
    public Task<StampDutyWalletInvoicesPage> ListInvoicesAsync(WalletListParams? parameters, CancellationToken cancellationToken) => ListInvoicesAsync(parameters, null, cancellationToken);

    /// <summary>Gets one top-up GST tax invoice (GET /v1/stamp-duty-wallet/invoices/{id}). Requires the <c>stamp-duty-wallet.invoices.read</c> scope.</summary>
    /// <param name="id">The invoice id (for the Stamp Duty Wallet, the <c>TopupId</c> of the top-up).</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<StampDutyWalletInvoice> GetInvoiceAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<StampDutyWalletInvoice>(HttpMethod.Get, "/v1/stamp-duty-wallet/invoices/{id}", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetInvoiceAsync(string, RequestOptions, CancellationToken)"/>
    public Task<StampDutyWalletInvoice> GetInvoiceAsync(string id, CancellationToken cancellationToken) => GetInvoiceAsync(id, null, cancellationToken);

    /// <summary>Streams every ledger row, following <c>nextCursor</c> lazily.</summary>
    /// <param name="parameters">Paging; a starting cursor is honoured.</param>
    /// <param name="options">Per-call overrides applied to every page request.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>An asynchronous sequence of ledger rows.</returns>
    public async IAsyncEnumerable<WalletTransaction> ListAllTransactionsAsync(WalletListParams? parameters = null, RequestOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = parameters ?? new WalletListParams();
        while (true)
        {
            var page = await ListTransactionsAsync(current, options, cancellationToken).ConfigureAwait(false);
            if (page.Items is not null)
            {
                foreach (var row in page.Items)
                {
                    yield return row;
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
}

/// <summary>The Service wallet (class name kept for compatibility): one balance for verification calls and the procurement and API charges of an e-Stamp order, net of GST. A top-up is a minimum of Rs 1,000 plus 18% GST on the amount you add; GST on fees is paid at top-up, so orders debit fees before GST.</summary>
public sealed class ServiceFeeWalletResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal ServiceFeeWalletResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Gets the wallet balance (a decimal string) (GET /v1/service-fee-wallet). Requires the <c>service-fee-wallet.read</c> scope.</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletBalance> GetBalanceAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletBalance>(HttpMethod.Get, "/v1/service-fee-wallet", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetBalanceAsync(RequestOptions, CancellationToken)"/>
    public Task<WalletBalance> GetBalanceAsync(CancellationToken cancellationToken) => GetBalanceAsync(null, cancellationToken);

    /// <summary>Creates a payment order to top up the wallet. Amounts come from the response; the SDK never computes fees (POST /v1/service-fee-wallet/topup). Never retried automatically and sent without an Idempotency-Key. Requires the <c>service-fee-wallet.topup</c> scope.</summary>
    /// <param name="request">The amount to credit, without fee or tax.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<ServiceFeeWalletTopup> TopUpAsync(WalletTopupRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate && request.BaseAmount is null or <= 0)
        {
            throw ValidationException.ForField("baseAmount", "baseAmount must be a positive number");
        }

        return await _transport.RequestAsync<ServiceFeeWalletTopup>(HttpMethod.Post, "/v1/service-fee-wallet/topup", null, null, request, options, false, null, cancellationToken, retryMode: RetryMode.Never).ConfigureAwait(false);
    }

    /// <inheritdoc cref="TopUpAsync(WalletTopupRequest, RequestOptions, CancellationToken)"/>
    public Task<ServiceFeeWalletTopup> TopUpAsync(WalletTopupRequest request, CancellationToken cancellationToken) => TopUpAsync(request, null, cancellationToken);

    /// <summary>Fetches one page of the wallet ledger, newest first (GET /v1/service-fee-wallet/transactions). Requires the <c>service-fee-wallet.read</c> scope.</summary>
    /// <param name="parameters">Paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletTransactionsPage> ListTransactionsAsync(WalletListParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletTransactionsPage>(HttpMethod.Get, "/v1/service-fee-wallet/transactions", null, WalletsResource.PageQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListTransactionsAsync(WalletListParams?, RequestOptions, CancellationToken)"/>
    public Task<WalletTransactionsPage> ListTransactionsAsync(WalletListParams? parameters, CancellationToken cancellationToken) => ListTransactionsAsync(parameters, null, cancellationToken);

    /// <summary>Fetches one page of top-up GST tax invoices, newest first (GET /v1/service-fee-wallet/invoices). Requires the <c>service-fee-wallet.invoices.read</c> scope.</summary>
    /// <param name="parameters">Paging; <see langword="null"/> for the defaults.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletInvoicesPage> ListInvoicesAsync(WalletListParams? parameters, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletInvoicesPage>(HttpMethod.Get, "/v1/service-fee-wallet/invoices", null, WalletsResource.PageQuery(parameters), null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ListInvoicesAsync(WalletListParams?, RequestOptions, CancellationToken)"/>
    public Task<WalletInvoicesPage> ListInvoicesAsync(WalletListParams? parameters, CancellationToken cancellationToken) => ListInvoicesAsync(parameters, null, cancellationToken);

    /// <summary>Gets one top-up GST tax invoice (GET /v1/service-fee-wallet/invoices/{id}). Requires the <c>service-fee-wallet.invoices.read</c> scope.</summary>
    /// <param name="id">The invoice id (for the Stamp Duty Wallet, the <c>TopupId</c> of the top-up).</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<WalletInvoice> GetInvoiceAsync(string id, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<WalletInvoice>(HttpMethod.Get, "/v1/service-fee-wallet/invoices/{id}", new string?[] { id }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetInvoiceAsync(string, RequestOptions, CancellationToken)"/>
    public Task<WalletInvoice> GetInvoiceAsync(string id, CancellationToken cancellationToken) => GetInvoiceAsync(id, null, cancellationToken);

    /// <summary>Streams every ledger row, following <c>nextCursor</c> lazily.</summary>
    /// <param name="parameters">Paging; a starting cursor is honoured.</param>
    /// <param name="options">Per-call overrides applied to every page request.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>An asynchronous sequence of ledger rows.</returns>
    public async IAsyncEnumerable<WalletTransaction> ListAllTransactionsAsync(WalletListParams? parameters = null, RequestOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = parameters ?? new WalletListParams();
        while (true)
        {
            var page = await ListTransactionsAsync(current, options, cancellationToken).ConfigureAwait(false);
            if (page.Items is not null)
            {
                foreach (var row in page.Items)
                {
                    yield return row;
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
}
