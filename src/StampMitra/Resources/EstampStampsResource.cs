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

/// <summary>Single e-Stamps: read, cancel, replace, PAN card link and certificate.</summary>
public sealed class EstampStampsResource
{
    private readonly Transport _transport;
    private readonly bool _validate;

    internal EstampStampsResource(Transport transport, bool validate)
    {
        _transport = transport;
        _validate = validate;
    }

    /// <summary>Gets one stamp (GET /v1/estamp/stamps/{stampId}). Requires the <c>estamp.stamps.read</c> scope.</summary>
    /// <param name="stampId">The stamp id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampStamp> GetAsync(string stampId, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampStamp>(HttpMethod.Get, "/v1/estamp/stamps/{stampId}", new string?[] { stampId }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampStamp> GetAsync(string stampId, CancellationToken cancellationToken) => GetAsync(stampId, null, cancellationToken);

    /// <summary>Cancels one placed stamp (POST /v1/estamp/stamps/{stampId}/cancel). Never retried automatically and sent without an Idempotency-Key. Requires the <c>estamp.stamps.cancel</c> scope.</summary>
    /// <param name="stampId">The stamp id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampStamp> CancelAsync(string stampId, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampStamp>(HttpMethod.Post, "/v1/estamp/stamps/{stampId}/cancel", new string?[] { stampId }, null, null, options, false, null, cancellationToken, retryMode: RetryMode.Never, noBody: true).ConfigureAwait(false);
    }

    /// <inheritdoc cref="CancelAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampStamp> CancelAsync(string stampId, CancellationToken cancellationToken) => CancelAsync(stampId, null, cancellationToken);

    /// <summary>Requests a replacement for a stamp and returns the replacement (which has its own id) (POST /v1/estamp/stamps/{stampId}/replace). Never retried automatically and sent without an Idempotency-Key. Requires the <c>estamp.stamps.replace</c> scope.</summary>
    /// <param name="stampId">The stamp id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampStamp> ReplaceAsync(string stampId, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampStamp>(HttpMethod.Post, "/v1/estamp/stamps/{stampId}/replace", new string?[] { stampId }, null, null, options, false, null, cancellationToken, retryMode: RetryMode.Never, noBody: true).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ReplaceAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampStamp> ReplaceAsync(string stampId, CancellationToken cancellationToken) => ReplaceAsync(stampId, null, cancellationToken);

    /// <summary>Sends or replaces the payer's PAN card link; answers 202 and is processed asynchronously (poll <c>GetAsync</c> and read <c>PanCard</c>) (POST /v1/estamp/stamps/{stampId}/pan-card). Never retried automatically and sent without an Idempotency-Key. Requires the <c>estamp.orders.create</c> scope.</summary>
    /// <param name="stampId">The stamp id.</param>
    /// <param name="request">The https link.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampPanCardResult> SetPanCardAsync(string stampId, EstampPanCardRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_validate && (request.PanCardImage is null || !request.PanCardImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw ValidationException.ForField("panCardImage", "panCardImage must be an https link");
        }

        return await _transport.RequestAsync<EstampPanCardResult>(HttpMethod.Post, "/v1/estamp/stamps/{stampId}/pan-card", new string?[] { stampId }, null, request, options, false, null, cancellationToken, retryMode: RetryMode.Never).ConfigureAwait(false);
    }

    /// <inheritdoc cref="SetPanCardAsync(string, EstampPanCardRequest, RequestOptions, CancellationToken)"/>
    public Task<EstampPanCardResult> SetPanCardAsync(string stampId, EstampPanCardRequest request, CancellationToken cancellationToken) => SetPanCardAsync(stampId, request, null, cancellationToken);

    /// <summary>Gets the certificate of a verified stamp (always HTTP 200; check <c>Available</c>) (GET /v1/estamp/stamps/{stampId}/certificate). Requires the <c>estamp.stamps.read</c> scope.</summary>
    /// <param name="stampId">The stamp id.</param>
    /// <param name="options">Per-call overrides (timeout, retries, idempotency key, headers).</param>
    /// <param name="cancellationToken">Cancels the call, including any pending retry delay.</param>
    /// <returns>The response; HTTP metadata is on <see cref="ApiModel.Response"/>.</returns>
    public async Task<EstampCertificate> GetCertificateAsync(string stampId, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _transport.RequestAsync<EstampCertificate>(HttpMethod.Get, "/v1/estamp/stamps/{stampId}/certificate", new string?[] { stampId }, null, null, options, false, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="GetCertificateAsync(string, RequestOptions, CancellationToken)"/>
    public Task<EstampCertificate> GetCertificateAsync(string stampId, CancellationToken cancellationToken) => GetCertificateAsync(stampId, null, cancellationToken);
}
