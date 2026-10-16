using System;
using System.Security.Cryptography;

namespace StampMitra;

internal static class Idempotency
{
    /// <summary>A random (version 4) UUID from the operating system CSPRNG.</summary>
    public static string NewKey()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        b[6] = (byte)((b[6] & 0x0F) | 0x40);
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        var h = Convert.ToHexString(b).ToLowerInvariant();
        return h[..8] + "-" + h.Substring(8, 4) + "-" + h.Substring(12, 4) + "-" + h.Substring(16, 4) + "-" + h[20..];
    }
}
