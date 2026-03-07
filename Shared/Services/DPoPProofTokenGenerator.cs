#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace UdapEd.Shared.Services;

public static class DPoPProofTokenGenerator
{
    /// <summary>
    /// Generates a DPoP proof JWT per RFC 9449 using the provided X.509 certificate's key pair.
    /// </summary>
    /// <param name="cert">The X.509 certificate with private key to sign the proof.</param>
    /// <param name="signingAlgorithm">The signing algorithm (e.g., RS256, ES256).</param>
    /// <param name="httpMethod">The HTTP method of the request (e.g., "POST").</param>
    /// <param name="httpUrl">The HTTP target URI of the request (e.g., the token endpoint URL).</param>
    /// <param name="accessToken">Optional access token for token-bound DPoP proofs (used with ath claim).</param>
    /// <returns>The signed DPoP proof JWT string.</returns>
    public static string GenerateProofToken(
        X509Certificate2 cert,
        string signingAlgorithm,
        string httpMethod,
        string httpUrl,
        string? accessToken = null)
    {
        var securityKey = new X509SecurityKey(cert);
        var jwkDict = BuildPublicJwkFromCertificate(cert);

        var signingCredentials = new SigningCredentials(securityKey, signingAlgorithm);

        var header = new JwtHeader(signingCredentials);
        header["typ"] = "dpop+jwt";
        header["jwk"] = jwkDict;

        // DPoP proofs use jwk, not x5t or x5c
        header.Remove("x5t");
        header.Remove("x5c");
        header.Remove("kid");

        var payload = new JwtPayload
        {
            { "htm", httpMethod },
            { "htu", httpUrl },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "jti", Guid.NewGuid().ToString() }
        };

        if (accessToken != null)
        {
            // ath claim: base64url-encoded SHA-256 hash of the access token
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));
            payload.Add("ath", Base64UrlEncoder.Encode(hash));
        }

        var token = new JwtSecurityToken(header, payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Computes the JWK Thumbprint (dpop_jkt) per RFC 7638 for the public key in the certificate.
    /// This is a base64url-encoded SHA-256 hash of the canonical JSON representation
    /// of the JWK's required members.
    /// </summary>
    public static string ComputeJwkThumbprint(X509Certificate2 cert)
    {
        // RFC 7638: canonical JSON with required members in lexicographic order
        string canonicalJson;

        var rsa = cert.GetRSAPublicKey();
        if (rsa != null)
        {
            var p = rsa.ExportParameters(false);
            var e = Base64UrlEncoder.Encode(p.Exponent!);
            var n = Base64UrlEncoder.Encode(p.Modulus!);
            canonicalJson = $"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}";
        }
        else
        {
            var ecdsa = cert.GetECDsaPublicKey();
            if (ecdsa != null)
            {
                var p = ecdsa.ExportParameters(false);
                var crv = GetCrvName(p.Curve);
                var x = Base64UrlEncoder.Encode(p.Q.X!);
                var y = Base64UrlEncoder.Encode(p.Q.Y!);
                canonicalJson = $"{{\"crv\":\"{crv}\",\"kty\":\"EC\",\"x\":\"{x}\",\"y\":\"{y}\"}}";
            }
            else
            {
                throw new NotSupportedException("Certificate key type is not supported for JWK Thumbprint.");
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Base64UrlEncoder.Encode(hash);
    }

    /// <summary>
    /// Extracts the public key parameters from the certificate and builds a JWK dictionary
    /// with the actual key material (n, e for RSA; crv, x, y for EC).
    /// </summary>
    private static Dictionary<string, string> BuildPublicJwkFromCertificate(X509Certificate2 cert)
    {
        var dict = new Dictionary<string, string>();

        var rsa = cert.GetRSAPublicKey();
        if (rsa != null)
        {
            var p = rsa.ExportParameters(false);
            dict["kty"] = "RSA";
            dict["n"] = Base64UrlEncoder.Encode(p.Modulus!);
            dict["e"] = Base64UrlEncoder.Encode(p.Exponent!);
            return dict;
        }

        var ecdsa = cert.GetECDsaPublicKey();
        if (ecdsa != null)
        {
            var p = ecdsa.ExportParameters(false);
            dict["kty"] = "EC";
            dict["crv"] = GetCrvName(p.Curve);
            dict["x"] = Base64UrlEncoder.Encode(p.Q.X!);
            dict["y"] = Base64UrlEncoder.Encode(p.Q.Y!);
            return dict;
        }

        throw new NotSupportedException("Certificate key type is not supported for DPoP.");
    }

    private static string GetCrvName(ECCurve curve)
    {
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP256.Oid?.Value)
            return "P-256";
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP384.Oid?.Value)
            return "P-384";
        if (curve.Oid?.Value == ECCurve.NamedCurves.nistP521.Oid?.Value)
            return "P-521";

        return curve.Oid?.FriendlyName ?? "unknown";
    }
}
