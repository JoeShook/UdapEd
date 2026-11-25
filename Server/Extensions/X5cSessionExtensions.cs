#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;

namespace UdapEd.Server.Extensions;

/// <summary>
/// Two session stores per certificate context:
/// <list type="bullet">
/// <item>the x5c store (<see cref="UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES"/> or
/// <see cref="UdapEdConstants.CERTIFICATION_INTERMEDIATE_CERTIFICATES"/>): what goes into the x5c header after the
/// signing certificate;</item>
/// <item>the PKCS#12 store (<see cref="UdapEdConstants.UDAP_PKCS12_INTERMEDIATE_CERTIFICATES"/> or
/// <see cref="UdapEdConstants.CERTIFICATION_PKCS12_INTERMEDIATE_CERTIFICATES"/>): intermediates found in the loaded
/// PKCS#12 file, available but not sent until they are included.</item>
/// </list>
/// For a TEFCA server the issuer chain is included automatically; for other communities only when the user asks.
/// </summary>
public static class X5cSessionExtensions
{
    /// <summary>
    /// Builds the x5c certificate list for a JWT signed with <paramref name="signingCertificate"/>: the signing
    /// certificate followed by the certificates in the x5c store, issuer path first.
    /// </summary>
    public static List<X509Certificate2> GetX5cCertificates(
        this ISession session,
        X509Certificate2 signingCertificate,
        string x5cSessionKey)
    {
        var intermediates = session.GetString(x5cSessionKey).DeserializeCertificates();

        return X5cChain.Order(signingCertificate, intermediates);
    }

    /// <summary>
    /// A new signing certificate was loaded: empties its x5c store and keeps the intermediates found in the
    /// PKCS#12 file in the PKCS#12 store, so they can be included later.
    /// </summary>
    public static void StorePkcs12Intermediates(
        this ISession session,
        string x5cSessionKey,
        string pkcs12SessionKey,
        byte[] pkcs12,
        string? password,
        X509Certificate2 signingCertificate,
        ILogger logger)
    {
        session.Remove(x5cSessionKey);
        session.Remove(pkcs12SessionKey);

        try
        {
            var intermediates = X5cChain.IntermediatesFromPkcs12(pkcs12, password, signingCertificate);

            if (intermediates.Count > 0)
            {
                session.SetString(pkcs12SessionKey, intermediates.SerializeCertificates());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read intermediate certificates from the PKCS#12 file");
        }
    }

    /// <summary>
    /// Adds the issuer chain of <paramref name="signingCertificate"/> to the x5c store: the issuer path found among
    /// the PKCS#12 intermediates, <paramref name="additionalCandidates"/> (e.g. resolved from AIA), what the x5c store
    /// already holds, and the loaded anchor when that anchor is an intermediate. Returns the certificates added.
    /// </summary>
    public static List<X509Certificate2> IncludeIssuerChain(
        this ISession session,
        X509Certificate2 signingCertificate,
        string x5cSessionKey,
        string pkcs12SessionKey,
        IEnumerable<X509Certificate2>? additionalCandidates = null)
    {
        var stored = session.GetString(x5cSessionKey).DeserializeCertificates();

        var candidates = session.GetString(pkcs12SessionKey).DeserializeCertificates()
            .Concat(additionalCandidates ?? Enumerable.Empty<X509Certificate2>())
            .Concat(stored)
            .ToList();

        var anchor = session.GetAnchorCertificate();
        if (anchor != null && !X5cChain.IsSelfSigned(anchor) && candidates.All(c => c.Thumbprint != anchor.Thumbprint))
        {
            candidates.Add(anchor);
        }

        var added = new List<X509Certificate2>();

        foreach (var certificate in X5cChain.IssuerPath(signingCertificate, candidates))
        {
            if (stored.All(s => s.Thumbprint != certificate.Thumbprint))
            {
                stored.Add(certificate);
                added.Add(certificate);
            }
        }

        if (added.Count > 0)
        {
            session.SetString(x5cSessionKey, stored.SerializeCertificates());
        }

        return added;
    }

    private static X509Certificate2? GetAnchorCertificate(this ISession session)
    {
        var anchorString = session.GetString(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);

        if (string.IsNullOrEmpty(anchorString))
        {
            return null;
        }

        try
        {
            var certBytes = Convert.FromBase64String(anchorString);
            return X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
        }
        catch
        {
            return null;
        }
    }
}
