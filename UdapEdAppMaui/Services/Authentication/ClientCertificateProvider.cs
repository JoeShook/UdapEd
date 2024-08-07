#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using UdapEd.Shared;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services.Authentication;

public class ClientCertificateProvider : IClientCertificateProvider
{
    private readonly ILogger<AccessTokenProvider> _logger;

    public ClientCertificateProvider(ILogger<AccessTokenProvider> logger)
    {
        _logger = logger;
    }

    public X509Certificate2? GetClientCertificate(CancellationToken token = default)
    {
        var certBytesWithKey = SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY)
            .GetAwaiter().GetResult();

        if (certBytesWithKey != null)
        {
            var certBytes = Convert.FromBase64String(certBytesWithKey);
            var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
            return clientCert;
        }

        _logger.LogDebug($"Missing Client Certificate in Session: {UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY}");
        return null;
    }

    public X509Certificate2Collection? GetAnchorCertificates(CancellationToken token = default)
    {
        var anchorBytes = SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE)
            .GetAwaiter().GetResult();

        if (anchorBytes != null)
        {
            var certBytes = Convert.FromBase64String(anchorBytes);
            var anchorCerts = new X509Certificate2Collection() { new(certBytes) };
            return anchorCerts;
        }

        _logger.LogDebug($"Missing Client Certificate in Session: {UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY}");
        return null;
    }
}
