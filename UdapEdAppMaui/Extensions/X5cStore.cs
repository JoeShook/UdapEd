#region (c) 2026 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;

namespace UdapEdAppMaui.Extensions;

/// <summary>
/// The desktop app's version of the server's X5cSessionExtensions, kept in secure storage. Two stores per
/// certificate context:
/// <list type="bullet">
/// <item>the x5c store (<see cref="UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES"/> or
/// <see cref="UdapEdConstants.CERTIFICATION_INTERMEDIATE_CERTIFICATES"/>): what goes into the x5c header after the
/// signing certificate;</item>
/// <item>the PKCS#12 store (<see cref="UdapEdConstants.UDAP_PKCS12_INTERMEDIATE_CERTIFICATES"/> or
/// <see cref="UdapEdConstants.CERTIFICATION_PKCS12_INTERMEDIATE_CERTIFICATES"/>): intermediates found in the loaded
/// PKCS#12 file, available but not sent until they are included.</item>
/// </list>
/// </summary>
public static class X5cStore
{
    public static string X5cKey(string? certContext) => certContext == "certification"
        ? UdapEdConstants.CERTIFICATION_INTERMEDIATE_CERTIFICATES
        : UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES;

    public static string Pkcs12Key(string? certContext) => certContext == "certification"
        ? UdapEdConstants.CERTIFICATION_PKCS12_INTERMEDIATE_CERTIFICATES
        : UdapEdConstants.UDAP_PKCS12_INTERMEDIATE_CERTIFICATES;

    public static string SigningCertificateKey(string? certContext) => certContext == "certification"
        ? UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY
        : UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY;

    public static async Task<X509Certificate2Collection> Read(string key)
    {
        var stored = await SessionExtensions.RetrieveFromChunks(key);
        return stored == null
            ? new X509Certificate2Collection()
            : Base64UrlEncoder.Decode(stored).DeserializeCertificates();
    }

    private static async Task Write(string key, X509Certificate2Collection certificates)
    {
        await SessionExtensions.RemoveChunks(key);
        await SessionExtensions.StoreInChunks(key, certificates.SerializeCertificates());
    }

    /// <summary>The signing certificate followed by the x5c store's certificates, issuer path first.</summary>
    public static async Task<List<X509Certificate2>> GetX5cCertificates(X509Certificate2 signingCertificate, string x5cKey) =>
        X5cChain.Order(signingCertificate, await Read(x5cKey));

    /// <summary>Adds one certificate to the x5c store, unless it is already there.</summary>
    public static async Task Add(string x5cKey, X509Certificate2 certificate)
    {
        var stored = await Read(x5cKey);
        if (stored.All(s => s.Thumbprint != certificate.Thumbprint))
        {
            stored.Add(certificate);
            await Write(x5cKey, stored);
        }
    }

    /// <summary>Empties the x5c store; the PKCS#12 store is kept, so the intermediates can be included again.</summary>
    public static Task Clear(string x5cKey) => SessionExtensions.RemoveChunks(x5cKey);

    /// <summary>
    /// A new signing certificate was loaded: empties its x5c store and keeps the intermediates found in the PKCS#12
    /// file in the PKCS#12 store.
    /// </summary>
    public static async Task StorePkcs12Intermediates(
        string? certContext,
        byte[] pkcs12,
        string? password,
        X509Certificate2 signingCertificate,
        ILogger logger)
    {
        await SessionExtensions.RemoveChunks(X5cKey(certContext));
        await SessionExtensions.RemoveChunks(Pkcs12Key(certContext));

        try
        {
            var intermediates = X5cChain.IntermediatesFromPkcs12(pkcs12, password, signingCertificate);
            if (intermediates.Count > 0)
            {
                await Write(Pkcs12Key(certContext), intermediates);
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
    public static async Task<List<X509Certificate2>> IncludeIssuerChain(
        string? certContext,
        X509Certificate2 signingCertificate,
        IEnumerable<X509Certificate2>? additionalCandidates = null)
    {
        var stored = await Read(X5cKey(certContext));

        var candidates = (await Read(Pkcs12Key(certContext)))
            .Concat(additionalCandidates ?? Enumerable.Empty<X509Certificate2>())
            .Concat(stored)
            .ToList();

        var anchor = await ReadAnchor();
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
            await Write(X5cKey(certContext), stored);
        }

        return added;
    }

    private static async Task<X509Certificate2?> ReadAnchor()
    {
        try
        {
            var anchor = await SecureStorage.Default.GetAsync(UdapEdConstants.UDAP_ANCHOR_CERTIFICATE);
            return string.IsNullOrEmpty(anchor) ? null : X509CertificateLoader.LoadCertificate(Convert.FromBase64String(anchor));
        }
        catch
        {
            return null;
        }
    }
}
