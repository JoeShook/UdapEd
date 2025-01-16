using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Services;
using UdapEdAppMaui.Extensions;

namespace UdapEdAppMaui.Services;
public class Infrastructure : UdapEd.Shared.Services.Infrastructure
{
    public Infrastructure(HttpClient httpClient, CrlCacheService crlCacheService, ILogger<UdapEd.Shared.Services.Infrastructure> logger) 
        : base(httpClient, crlCacheService, logger)
    {
    }


    public override async Task<string?> GetIntermediateX509(string url)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(url);

            var certificate = new X509Certificate2(bytes);
            var intermediatesStored =
                await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
            var intermediateCerts = intermediatesStored.DeserializeCertificates();

            if (intermediateCerts != null && intermediateCerts.Any())
            {
                intermediateCerts.Add(certificate);
            }

            await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES,
                certificate.RawData);

            return certificate.GetRawCertDataString();
        }
        catch (HttpRequestException ex)
        {
            return null;
        }
    }
}
