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


    public override async Task<string?> GetIntermediateX509(string url, string? certContext = null)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);

            var certificate = new X509Certificate2(bytes);
            var sessionKey = certContext == "certification"
                ? UdapEdConstants.CERTIFICATION_INTERMEDIATE_CERTIFICATES
                : UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES;
            var intermediatesStored = await SessionExtensions.RetrieveFromChunks(sessionKey);
            var intermediateCerts =
                intermediatesStored == null ? new X509Certificate2Collection() :
                Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates() ?? new X509Certificate2Collection();

            if (intermediateCerts.All(i => i.Thumbprint != certificate.Thumbprint))
            {
                intermediateCerts.Add(certificate);
                await SessionExtensions.StoreInChunks(sessionKey, intermediateCerts.SerializeCertificates());
            }

            return certificate.GetRawCertDataString();
        }
        catch (HttpRequestException ex)
        {
            return null;
        }
    }
}
