#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.X509;

namespace UdapEd.Shared.Conformance;

/// <summary>
/// Reads the parts of a compact JWT that conformance checks look at. It does not verify the signature or the
/// certificate chain; UdapEd's discovery and registration steps already do that.
/// </summary>
public sealed class JwtInspection
{
    private JwtInspection(JsonObject header, JsonObject payload)
    {
        Header = header;
        Payload = payload;
    }

    public JsonObject Header { get; }

    public JsonObject Payload { get; }

    public string? Alg => Header.String("alg");

    /// <summary>The x5c header's certificates, leaf first, as base64 DER strings.</summary>
    public IReadOnlyList<string> X5c => Header["x5c"] is JsonArray array
        ? array.Select(n => n?.GetValue<string>()).OfType<string>().ToList()
        : [];

    /// <summary>Parses a compact-serialized JWT; null when it isn't one.</summary>
    public static JwtInspection? Parse(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var parts = jwt.Trim().Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var header = JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]))) as JsonObject;
            var payload = JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]))) as JsonObject;
            return header != null && payload != null ? new JwtInspection(header, payload) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The uniformResourceIdentifier entries in the Subject Alternative Name extension of the leaf (first x5c)
    /// certificate, exactly as written. Null when there is no leaf certificate or it can't be read.
    /// </summary>
    public IReadOnlyList<string>? LeafSanUris()
    {
        if (X5c.Count == 0)
        {
            return null;
        }

        try
        {
            // BouncyCastle rather than X509Certificate2, so this also runs in the browser.
            var certificate = new X509CertificateParser().ReadCertificate(Convert.FromBase64String(X5c[0]));
            var names = certificate.GetSubjectAlternativeNames();
            if (names == null)
            {
                return [];
            }

            const int uniformResourceIdentifier = 6;
            return names
                .OfType<System.Collections.IList>()
                .Where(entry => entry.Count >= 2 && entry[0] is int type && type == uniformResourceIdentifier)
                .Select(entry => entry[1]?.ToString())
                .OfType<string>()
                .ToList();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal static class JsonNodeExtensions
{
    public static string? String(this JsonObject obj, string name) =>
        obj[name] is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    public static long? Long(this JsonObject obj, string name) =>
        obj[name] is JsonValue value && value.TryGetValue<long>(out var l) ? l : null;

    /// <summary>The string elements of an array member; null when the member is missing or not an array.</summary>
    public static List<string>? Strings(this JsonObject obj, string name) =>
        obj[name] is JsonArray array
            ? array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToList()
            : null;

    public static bool Has(this JsonObject obj, string name) => obj.ContainsKey(name) && obj[name] != null;
}
