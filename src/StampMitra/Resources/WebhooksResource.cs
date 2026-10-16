using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Webhook management.</summary>
public sealed class WebhooksResource
{
    internal WebhooksResource(Transport transport)
    {
        Events = new WebhookEventsResource(transport);
    }

    /// <summary>Delivered webhook events: history and replay.</summary>
    public WebhookEventsResource Events { get; }
}
