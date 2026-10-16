using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StampMitra.Models;

namespace StampMitra.Resources;

/// <summary>Payments.</summary>
public sealed class PaymentsResource
{
    internal PaymentsResource(Transport transport, bool validate)
    {
        Intents = new PaymentIntentsResource(transport, validate);
    }

    /// <summary>Payment intents.</summary>
    public PaymentIntentsResource Intents { get; }
}
