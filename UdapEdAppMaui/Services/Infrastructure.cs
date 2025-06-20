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
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;
using UdapEdAppMaui.Extensions;

namespace UdapEdAppMaui.Services;
public class Infrastructure : UdapEd.Shared.Services.Infrastructure
{
    public Infrastructure(HttpClient httpClient, CrlCacheService crlCacheService, IFhirClientOptionsProvider fhirClientOptionsProvider, ILogger<UdapEd.Shared.Services.Infrastructure> logger) 
        : base(httpClient, crlCacheService, fhirClientOptionsProvider, logger)
    {
    }


    public override async Task<string?> GetIntermediateX509(string url)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);

            var certificate = new X509Certificate2(bytes);
            var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
            var intermediateCerts =
                intermediatesStored == null ? new X509Certificate2Collection() :
                Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates() ?? new X509Certificate2Collection();

            if (intermediateCerts.All(i => i.Thumbprint != certificate.Thumbprint))
            {
                intermediateCerts.Add(certificate);
                await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES, intermediateCerts.SerializeCertificates());
            }

            // if (intermediateCerts != null && intermediateCerts.Any())
            // {
            //     intermediateCerts.Add(certificate);
            // }
            //
            // await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES,
            //     certificate.RawData);

            return certificate.GetRawCertDataString();
        }
        catch (HttpRequestException ex)
        {
            return null;
        }
    }
}
