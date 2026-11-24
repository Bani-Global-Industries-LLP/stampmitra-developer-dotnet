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

/// <summary>Your organization's wallet balance and usage. Any key with a verification scope can read them.</summary>
public sealed class AccountResource
{
    private readonly Transport _transport;

    internal AccountResource(Transport transport)
    {
        _transport = transport;
    }

    /// <summary>Gets the wallet balance in rupees and paise, the low-balance state and the estimated calls remaining (see also the <c>X-Wallet-State</c> response header on <see cref="ApiModel.Response"/>) (GET /v1/account/balance).</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<AccountBalance> GetBalanceAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<AccountBalance>(HttpMethod.Get, "/v1/account/balance", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetBalanceAsync(RequestOptions, CancellationToken)"/>
    public Task<AccountBalance> GetBalanceAsync(CancellationToken cancellationToken) => GetBalanceAsync(null, cancellationToken);

    /// <summary>Gets this month's and last month's usage (IST) with limits (GET /v1/account/usage).</summary>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<AccountUsage> GetUsageAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<AccountUsage>(HttpMethod.Get, "/v1/account/usage", null, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetUsageAsync(RequestOptions, CancellationToken)"/>
    public Task<AccountUsage> GetUsageAsync(CancellationToken cancellationToken) => GetUsageAsync(null, cancellationToken);
}
