using System.Text.RegularExpressions;

namespace StampMitra;

/// <summary>
/// Client-side input validators. The patterns mirror the API's request validators, so malformed input fails locally
/// with a <see cref="ValidationException"/> (no network call, nothing billed). Values are trimmed and ASCII
/// upper-cased before matching, except bank account numbers (trimmed only). Separators (spaces, hyphens) are never
/// stripped: the API does not accept them either.
/// </summary>
public static class Validators
{
    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex PanRx = new(@"^[A-Z]{5}[0-9]{4}[A-Z]\z", Opts);
    private static readonly Regex GstinRx = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]\z", Opts);
    private static readonly Regex IfscRx = new(@"^[A-Z]{4}0[A-Z0-9]{6}\z", Opts);
    private static readonly Regex CinRx = new(@"^[LU][0-9]{5}[A-Z]{2}[0-9]{4}[A-Z]{3}[0-9]{6}\z", Opts);
    private static readonly Regex UdyamRx = new(@"^UDYAM-[A-Z]{2}-[0-9]{2}-[0-9]{7}\z", Opts);
    private static readonly Regex VehicleRx = new(@"^[A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{4}\z", Opts);
    private static readonly Regex BankAccountRx = new(@"^[0-9]{9,18}\z", Opts);
    private static readonly Regex PassportRx = new(@"^[A-Z]{2}[0-9]{13}\z", Opts);
    private static readonly Regex DrivingLicenseRx = new(@"^[A-Z]{2}[0-9]{2}[0-9]{11}\z", Opts);
    private static readonly Regex VoterIdRx = new(@"^[A-Z]{3}[0-9]{7}\z", Opts);
    private static readonly Regex PhoneRx = new(@"^(?:\+?91|0)?[6-9][0-9]{9}\z", Opts);
    private static readonly Regex IsoDateRx = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}(?:[T ][0-9:.]+(?:Z|[+-][0-9:]+)?)?\z", Opts);

    /// <summary>Validates a PAN (<c>ABCDE1234F</c>).</summary>
    /// <param name="value">The input.</param>
    /// <param name="field">Field name used in the error.</param>
    /// <returns>The normalised value.</returns>
    public static string Pan(string? value, string field = "pan") =>
        Match(value, PanRx, field, "must match the format ABCDE1234F");

    /// <summary>Validates a 15-character GSTIN.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Gstin(string? value, string field = "gstin") =>
        Match(value, GstinRx, field, "must be a valid 15 character GSTIN, e.g. 27ABCDE1234F1Z5");

    /// <summary>Validates an IFSC code (<c>ICIC0001234</c>).</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Ifsc(string? value, string field = "ifsc") =>
        Match(value, IfscRx, field, "must match the format ICIC0001234");

    /// <summary>Validates a company CIN.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Cin(string? value, string field = "cin") =>
        Match(value, CinRx, field, "must match the format U74999DL2015PTC284009");

    /// <summary>Validates a Udyam registration number.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Udyam(string? value, string field = "udyamNumber") =>
        Match(value, UdyamRx, field, "must match the format UDYAM-XX-00-0000000");

    /// <summary>Validates a vehicle registration number; separators such as <c>MH-12 AB 1234</c> are rejected, like the API does.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string VehicleNumber(string? value, string field = "vehicleNumber") =>
        Match(value, VehicleRx, field, "must be an Indian vehicle registration number, e.g. MH12AB1234");

    /// <summary>Validates a 9-18 digit bank account number (trimmed only).</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string BankAccount(string? value, string field = "accountNumber") =>
        Match(value, BankAccountRx, field, "must be a 9-18 digit numeric bank account number", upper: false);

    /// <summary>Validates a passport file number.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string PassportFileNumber(string? value, string field = "fileNumber") =>
        Match(value, PassportRx, field, "must match the format PA1079341954215");

    /// <summary>Validates a driving licence number.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string DrivingLicense(string? value, string field = "dlNumber") =>
        Match(value, DrivingLicenseRx, field, "must match the format MH1420110012345");

    /// <summary>Validates a Voter ID (EPIC) number.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string VoterId(string? value, string field = "voterId") =>
        Match(value, VoterIdRx, field, "must match the format ABC1234567");

    /// <summary>Validates an Indian mobile number (optionally prefixed with +91, 91 or 0).</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Phone(string? value, string field = "phone") =>
        Match(value, PhoneRx, field, "must be a valid 10-digit Indian mobile number", upper: false);

    /// <summary>Validates an ISO date (<c>1990-01-31</c>) or date-time.</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Date(string? value, string field = "dob") =>
        Match(value, IsoDateRx, field, "must be an ISO date, e.g. 1990-01-31", upper: false);

    /// <summary>Validates a free-text name (1-256 characters after trimming).</summary>
    /// <inheritdoc cref="Pan" path="/param|/returns"/>
    public static string Name(string? value, string field = "name")
    {
        var clean = value?.Trim() ?? string.Empty;
        if (clean.Length is 0 or > 256)
        {
            throw ValidationException.ForField(field, field + " must be 1-256 characters");
        }

        return clean;
    }

    private static string Match(string? value, Regex pattern, string field, string hint, bool upper = true)
    {
        var clean = (value ?? string.Empty).Trim();
        if (upper)
        {
            clean = AsciiUpper(clean);
        }

        if (!pattern.IsMatch(clean))
        {
            throw ValidationException.ForField(field, field + " " + hint);
        }

        return clean;
    }

    private static string AsciiUpper(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= 'a' and <= 'z')
            {
                chars[i] = (char)(chars[i] - 32);
            }
        }

        return new string(chars);
    }
}
