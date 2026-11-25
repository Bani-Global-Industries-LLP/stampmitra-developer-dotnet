using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampMitra.Models;

/// <summary>Values of <see cref="EstampParty.Gender"/> and <see cref="EstampStampLine.PayerGender"/>.</summary>
public static class EstampGender
{
    /// <summary>MALE.</summary>
    public const string Male = "MALE";

    /// <summary>FEMALE.</summary>
    public const string Female = "FEMALE";

    /// <summary>OTHER.</summary>
    public const string Other = "OTHER";
}

/// <summary>Values of <see cref="EstampStampLine.Category"/>.</summary>
public static class EstampCategory
{
    /// <summary>An e-Stamp.</summary>
    public const string EStamp = "e_stamp";

    /// <summary>A traditional (physical) stamp paper.</summary>
    public const string TraditionalStamp = "traditional_stamp";
}

/// <summary>Values of <see cref="EstampPropertyArea.Unit"/>: exactly these four spellings are accepted.</summary>
public static class EstampAreaUnit
{
    /// <summary>Square Feet.</summary>
    public const string SquareFeet = "Square Feet";

    /// <summary>Square Yard.</summary>
    public const string SquareYard = "Square Yard";

    /// <summary>Square Meters.</summary>
    public const string SquareMeters = "Square Meters";

    /// <summary>Not Applicable (no value needed).</summary>
    public const string NotApplicable = "Not Applicable";
}

/// <summary>
/// A party to an instrument. On a stamp line a party is either a bare name string or this object; a <see cref="string"/>
/// converts implicitly (<c>FirstParty = "Jane Doe"</c>). The API treats a bare string as <c>{ name }</c>; this type is always written as an object.
/// </summary>
[JsonConverter(typeof(EstampPartyConverter))]
public sealed record EstampParty
{
    /// <summary>Creates an empty party; set <see cref="Name"/>.</summary>
    public EstampParty()
    {
    }

    /// <summary>Creates a party with a name.</summary>
    /// <param name="name">Full name of the party, as on the identity document.</param>
    public EstampParty(string name)
    {
        Name = name;
    }

    /// <summary>Full name of the party, as on the identity document (1 to 150 characters). Required.</summary>
    public string? Name { get; init; }

    /// <summary>The party's address (up to 500 characters). Required by states that report <c>firstPartyAddressRequired</c>.</summary>
    public string? Address { get; init; }

    /// <summary>The party's phone number (up to 20 characters).</summary>
    public string? Phone { get; init; }

    /// <summary>Father's or husband's name (up to 150 characters). Required by states that report <c>fatherHusbandNameRequired</c>.</summary>
    public string? FatherHusbandName { get; init; }

    /// <summary>The party's gender, one of <see cref="EstampGender"/>.</summary>
    public string? Gender { get; init; }

    /// <summary>Creates a party from a bare name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>A party with only <see cref="Name"/> set.</returns>
    public static EstampParty FromName(string name) => new(name);

    /// <summary>Converts a bare name to a party.</summary>
    /// <param name="name">The name.</param>
    public static implicit operator EstampParty(string name) => FromName(name);
}

/// <summary>Reads a party from a name string or an object; writes the object form.</summary>
internal sealed class EstampPartyConverter : JsonConverter<EstampParty>
{
    public override EstampParty? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return new EstampParty(reader.GetString() ?? string.Empty);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A party must be a name string or an object");
        }

        using var doc = JsonDocument.ParseValue(ref reader);
        var e = doc.RootElement;
        return new EstampParty
        {
            Name = Str(e, "name"),
            Address = Str(e, "address"),
            Phone = Str(e, "phone"),
            FatherHusbandName = Str(e, "fatherHusbandName"),
            Gender = Str(e, "gender"),
        };
    }

    public override void Write(Utf8JsonWriter writer, EstampParty value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        Put(writer, "name", value.Name);
        Put(writer, "address", value.Address);
        Put(writer, "phone", value.Phone);
        Put(writer, "fatherHusbandName", value.FatherHusbandName);
        Put(writer, "gender", value.Gender);
        writer.WriteEndObject();
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static void Put(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}

/// <summary>Property area of a stamp line. Needed when the state's <c>propertyAreaRequired</c> flag is true.</summary>
public sealed record EstampPropertyArea
{
    /// <summary>One of <see cref="EstampAreaUnit"/>. Required.</summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>Area in the chosen unit; required, and greater than 0, unless the unit is Not Applicable.</summary>
    [JsonPropertyName("value")]
    public decimal? Value { get; init; }
}

/// <summary>One stamp line of an e-Stamp order. <see cref="Quantity"/> identical stamps are generated per line.</summary>
public sealed record EstampStampLine
{
    /// <summary>Your own reference for this stamp line, echoed back on the created order's stamps.</summary>
    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; init; }

    /// <summary>State code from <c>GET /v1/estamp/states</c> (case-insensitive). One order covers exactly one state. Required.</summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }

    /// <summary>Stamp category, one of <see cref="EstampCategory"/>.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>Article code from <c>GET /v1/estamp/states/{stateCode}/instruments</c>. Required.</summary>
    [JsonPropertyName("articleCode")]
    public string? ArticleCode { get; init; }

    /// <summary>Requested stamp duty denomination in rupees. Required.</summary>
    [JsonPropertyName("denomination")]
    public decimal? Denomination { get; init; }

    /// <summary>The party paying the duty: that party's name, <c>FIRST_PARTY</c> or <c>SECOND_PARTY</c>.</summary>
    [JsonPropertyName("stampDutyPayer")]
    public string? StampDutyPayer { get; init; }

    /// <summary>The payer's phone number.</summary>
    [JsonPropertyName("payerPhone")]
    public string? PayerPhone { get; init; }

    /// <summary>The payer's gender, one of <see cref="EstampGender"/>.</summary>
    [JsonPropertyName("payerGender")]
    public string? PayerGender { get; init; }

    /// <summary>First party: a name (implicit conversion from <see cref="string"/>) or an <see cref="EstampParty"/>. Required.</summary>
    [JsonPropertyName("firstParty")]
    public EstampParty? FirstParty { get; init; }

    /// <summary>Second party, when the article is not single-party. Same forms as <see cref="FirstParty"/>.</summary>
    [JsonPropertyName("secondParty")]
    public EstampParty? SecondParty { get; init; }

    /// <summary>Purpose or nature of the instrument.</summary>
    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    /// <summary>District code, when the state requires one.</summary>
    [JsonPropertyName("district")]
    public string? District { get; init; }

    /// <summary>Sub-registrar office value, when the state requires one.</summary>
    [JsonPropertyName("sro")]
    public string? Sro { get; init; }

    /// <summary>Cess amount in rupees, when it applies.</summary>
    [JsonPropertyName("cessAmount")]
    public decimal? CessAmount { get; init; }

    /// <summary>Property area, when the state requires it.</summary>
    [JsonPropertyName("propertyArea")]
    public EstampPropertyArea? PropertyArea { get; init; }

    /// <summary>https link to the payer's PAN card. Optional when placing the order, but a PAN card is mandatory to complete the stamp.</summary>
    [JsonPropertyName("panCardImage")]
    public string? PanCardImage { get; init; }

    /// <summary>Your own cost-centre tag.</summary>
    [JsonPropertyName("costCentre")]
    public string? CostCentre { get; init; }

    /// <summary>Your own business-unit tag.</summary>
    [JsonPropertyName("businessUnit")]
    public string? BusinessUnit { get; init; }

    /// <summary>Reference to a related or linked document.</summary>
    [JsonPropertyName("linkedDocRef")]
    public string? LinkedDocRef { get; init; }

    /// <summary>Number of identical stamps for this line, 1 to 1000 (default 1).</summary>
    [JsonPropertyName("quantity")]
    public int? Quantity { get; init; }
}

/// <summary>An e-Stamp order: one to 5000 stamp lines that all share one state.</summary>
public sealed record EstampOrderRequest
{
    /// <summary>Your own reference for the whole order (distinct from the Idempotency-Key).</summary>
    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    /// <summary>The stamp lines (1 to 5000).</summary>
    [JsonPropertyName("stamps")]
    public IReadOnlyList<EstampStampLine>? Stamps { get; init; }
}

/// <summary>Link to the payer's PAN card for one stamp.</summary>
public sealed record EstampPanCardRequest
{
    /// <summary>https link to the payer's PAN card (JPG, PNG, WebP, GIF, HEIC or PDF).</summary>
    [JsonPropertyName("panCardImage")]
    public string? PanCardImage { get; init; }
}

/// <summary>A wallet top-up request.</summary>
public sealed record WalletTopupRequest
{
    /// <summary>The amount you want in the wallet, in rupees, without any fee or tax. Must be positive.</summary>
    [JsonPropertyName("baseAmount")]
    public decimal? BaseAmount { get; init; }
}
