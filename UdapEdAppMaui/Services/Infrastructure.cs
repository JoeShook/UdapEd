#region (c) 2025 Joseph Shook. All rights reserved.
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
using Udap.Common.Certificates;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;
using UdapEdAppMaui.Extensions;

namespace UdapEdAppMaui.Services;
public class Infrastructure : UdapEd.Shared.Services.Infrastructure
{
    public Infrastructure(HttpClient httpClient, CrlCacheService crlCacheService, IFhirClientOptionsProvider fhirClientOptionsProvider, CertificateCacheSettings certificateCacheSettings, ICertificateDownloadCache certificateDownloadCache, ILogger<UdapEd.Shared.Services.Infrastructure> logger)
        : base(httpClient, crlCacheService, fhirClientOptionsProvider, certificateCacheSettings, certificateDownloadCache, logger)
    {
    }


    /// <summary>
    /// For a TEFCA server UdapEd includes the issuer chain automatically; same as <see cref="IncludeIssuerInX5c"/>.
    /// </summary>
    public override async Task ResolveAiaIntermediates(string? certContext = null)
    {
        await IncludeIssuerInX5c(certContext);
    }

    /// <summary>
    /// Includes the signing certificate's issuer chain in x5c: the issuer path found among the intermediates of the
    /// loaded PKCS#12 file, the certificates its AIA extension points to, and the loaded anchor when it is an
    /// intermediate. Returns the base64 certificates that were added.
    /// </summary>
    public override async Task<List<string>> IncludeIssuerInX5c(string? certContext = null)
    {
        try
        {
            var clientCertWithKey = await SessionExtensions.RetrieveFromChunks(X5cStore.SigningCertificateKey(certContext));
            if (clientCertWithKey == null)
            {
                return [];
            }

            var clientCert = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(clientCertWithKey), "ILikePasswords", X509KeyStorageFlags.Exportable);

            var aiaCerts = new List<X509Certificate2>();
            if (clientCert.Extensions["1.3.6.1.5.5.7.1.1"] is X509AuthorityInformationAccessExtension aiaExtension)
            {
                foreach (var aiaUrl in aiaExtension.EnumerateCAIssuersUris())
                {
                    try
                    {
                        aiaCerts.Add(X509CertificateLoader.LoadCertificate(await HttpClient.GetByteArrayAsync(aiaUrl)));
                    }
                    catch (HttpRequestException)
                    {
                        // An unreachable AIA URL; the PKCS#12 file or the anchor may still supply the issuer.
                    }
                }
            }

            var added = await X5cStore.IncludeIssuerChain(certContext, clientCert, aiaCerts);
            return added.Select(c => Convert.ToBase64String(c.RawData)).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Empties the x5c store; the PKCS#12 intermediates stay available to include again.</summary>
    public override Task RemoveIntermediatesFromX5c(string? certContext = null) =>
        X5cStore.Clear(X5cStore.X5cKey(certContext));

    public override async Task<string?> GetIntermediateX509(string url, string? certContext = null)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);
            var certificate = X509CertificateLoader.LoadCertificate(bytes);
            await X5cStore.Add(X5cStore.X5cKey(certContext), certificate);

            // Base64, as it goes into the x5c header (the server returns the same).
            return Convert.ToBase64String(certificate.RawData);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
