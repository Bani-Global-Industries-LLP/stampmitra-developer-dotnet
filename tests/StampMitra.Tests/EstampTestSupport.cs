using System.Reflection;
using System.Text.Json;
using StampMitra.Resources;

namespace StampMitra.Tests;

/// <summary>Sample inputs and a path-to-SDK-method map for the shared retry scenarios of the e-Stamp operations.</summary>
internal static class EstampScenarios
{
    public static readonly EstampOrderRequest SampleOrder = new()
    {
        Reference = "order-1",
        Stamps = new[]
        {
            new EstampStampLine { State = "MH", ArticleCode = "ART1", Denomination = 100, FirstParty = "Jane Doe", SecondParty = new EstampParty("John Smith") },
        },
    };

    public static EstampDocumentFile SampleFile() =>
        EstampDocumentFile.FromBytes(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n"), "pan.pdf");

    /// <summary>Calls the SDK method for a known e-Stamp scenario path; <see langword="null"/> when the path is not one of them.</summary>
    public static async Task<int?> TryCall(StampMitraClient c, string method, string path, RequestOptions o)
    {
        ApiModel? r = (method, path) switch
        {
            ("GET", "/v1/estamp/states/MH") => await c.Estamp.GetStateAsync("MH", o),
            ("GET", "/v1/stamp-duty-wallet") => await c.Wallets.StampDuty.GetBalanceAsync(o),
            ("GET", "/v1/estamp/orders/estord_missing") => await c.Estamp.Orders.GetAsync("estord_missing", o),
            ("POST", "/v1/estamp/orders") => await c.Estamp.Orders.CreateAsync(SampleOrder, o),
            ("POST", "/v1/estamp/orders/validate") => await c.Estamp.Orders.ValidateAsync(SampleOrder, o),
            ("POST", "/v1/estamp/orders/estord_1/documents") => await c.Estamp.Orders.UploadDocumentAsync("estord_1", SampleFile(), o),
            ("POST", "/v1/estamp/orders/estord_1/cancel") => await c.Estamp.Orders.CancelAsync("estord_1", o),
            ("POST", "/v1/estamp/stamps/eststamp_1/cancel") => await c.Estamp.Stamps.CancelAsync("eststamp_1", o),
            ("POST", "/v1/estamp/stamps/eststamp_1/replace") => await c.Estamp.Stamps.ReplaceAsync("eststamp_1", o),
            ("POST", "/v1/estamp/stamps/eststamp_1/pan-card") => await c.Estamp.Stamps.SetPanCardAsync("eststamp_1", new EstampPanCardRequest { PanCardImage = "https://cdn.example.com/pan.jpg" }, o),
            ("POST", "/v1/stamp-duty-wallet/topup") => await c.Wallets.StampDuty.TopUpAsync(new WalletTopupRequest { BaseAmount = 1000 }, o),
            ("POST", "/v1/service-fee-wallet/topup") => await c.Wallets.ServiceFee.TopUpAsync(new WalletTopupRequest { BaseAmount = 500 }, o),
            _ => null,
        };
        return r?.Response!.StatusCode;
    }
}

/// <summary>JSON helpers for comparing fixtures with what the SDK sent or modelled.</summary>
internal static class JsonCompare
{
    /// <summary>Removes nulls and compares numbers by value; property order is ignored.</summary>
    public static bool Equal(JsonElement a, JsonElement b, out string why)
    {
        why = string.Empty;
        return Eq(a, b, "$", ref why);
    }

    private static bool Eq(JsonElement a, JsonElement b, string at, ref string why)
    {
        if (a.ValueKind != b.ValueKind && !(a.ValueKind is JsonValueKind.True or JsonValueKind.False && b.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            why = at + ": " + a.ValueKind + " vs " + b.ValueKind;
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                var pa = a.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => p.Value);
                var pb = b.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => p.Value);
                foreach (var k in pa.Keys.Union(pb.Keys))
                {
                    if (!pa.TryGetValue(k, out var va) || !pb.TryGetValue(k, out var vb))
                    {
                        why = at + "." + k + ": missing on one side";
                        return false;
                    }

                    if (!Eq(va, vb, at + "." + k, ref why))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var la = a.EnumerateArray().ToList();
                var lb = b.EnumerateArray().ToList();
                if (la.Count != lb.Count)
                {
                    why = at + ": array length " + la.Count + " vs " + lb.Count;
                    return false;
                }

                for (var i = 0; i < la.Count; i++)
                {
                    if (!Eq(la[i], lb[i], at + "[" + i + "]", ref why))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Number:
                if (a.GetDecimal() != b.GetDecimal())
                {
                    why = at + ": " + a.GetRawText() + " vs " + b.GetRawText();
                    return false;
                }

                return true;
            case JsonValueKind.String:
                if (a.GetString() != b.GetString())
                {
                    why = at + ": '" + a.GetString() + "' vs '" + b.GetString() + "'";
                    return false;
                }

                return true;
            default:
                if (a.ValueKind != b.ValueKind || a.GetRawText() != b.GetRawText())
                {
                    why = at + ": " + a.GetRawText() + " vs " + b.GetRawText();
                    return false;
                }

                return true;
        }
    }

    /// <summary>The model as JSON (a bare-array list model as the array).</summary>
    public static JsonElement ToJson(ApiModel m) => m switch
    {
        EstampDistrictList d => JsonSerializer.SerializeToElement(d.Items, Json.Options),
        EstampSroList s => JsonSerializer.SerializeToElement(s.Items, Json.Options),
        _ => JsonSerializer.SerializeToElement(m, m.GetType(), Json.Options),
    };

    /// <summary>Paths of every modelled object that kept fields it does not model (the model is missing a spec field).</summary>
    public static List<string> UnmodelledFields(object? o, string at = "$")
    {
        var found = new List<string>();
        Walk(o, at, found);
        return found;
    }

    private static void Walk(object? o, string at, List<string> found)
    {
        if (o is null || o is string || o.GetType().IsPrimitive || o is decimal || o is JsonElement)
        {
            return;
        }

        if (o is ApiModel m && m.AdditionalProperties is { Count: > 0 } extra)
        {
            found.Add(at + " has unmodelled " + string.Join(",", extra.Keys));
        }

        if (o is System.Collections.IEnumerable e && o is not IDictionary<string, JsonElement>)
        {
            var i = 0;
            foreach (var x in e)
            {
                Walk(x, at + "[" + i++ + "]", found);
            }

            if (o is not ApiModel)
            {
                return;
            }
        }

        foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length == 0 && p.Name is not ("Response" or "AdditionalProperties" or "Items" or "Data" or "Count"))
            {
                Walk(p.GetValue(o), at + "." + p.Name, found);
            }
        }
    }
}
