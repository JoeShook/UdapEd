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

public static class X5cSessionExtensions
{
    /// <summary>
    /// Builds the x5c certificate list for a JWT signed with <paramref name="signingCertificate"/>:
    /// the signing certificate followed by its issuing intermediate(s), taken from the intermediates
    /// stored under <paramref name="intermediateSessionKey"/> (PKCS#12 chain, AIA, or added by the user)
    /// and from the loaded anchor when that anchor is an intermediate.
    /// </summary>
    public static List<X509Certificate2> GetX5cCertificates(
        this ISession session,
        X509Certificate2 signingCertificate,
        string intermediateSessionKey)
    {
        var intermediates = session.GetString(intermediateSessionKey).DeserializeCertificates();

        return X5cChain.Order(signingCertificate, intermediates, session.GetAnchorCertificate());
    }

    /// <summary>
    /// Stores the intermediates found in an uploaded PKCS#12 under <paramref name="intermediateSessionKey"/>,
    /// replacing whatever was stored for the previous certificate.
    /// </summary>
    public static void StorePkcs12Intermediates(
        this ISession session,
        string intermediateSessionKey,
        byte[] pkcs12,
        string? password,
        X509Certificate2 signingCertificate,
        ILogger logger)
    {
        session.Remove(intermediateSessionKey);

        try
        {
            var intermediates = X5cChain.IntermediatesFromPkcs12(pkcs12, password, signingCertificate);

            if (intermediates.Count > 0)
            {
                session.SetString(intermediateSessionKey, intermediates.SerializeCertificates());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read intermediate certificates from the PKCS#12 file");
        }
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
