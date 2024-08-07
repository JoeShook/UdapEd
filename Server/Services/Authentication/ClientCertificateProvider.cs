using System.Security.Cryptography.X509Certificates;
using UdapEd.Shared;
using UdapEd.Shared.Services;

namespace UdapEd.Server.Services.Authentication;

public class ClientCertificateProvider : IClientCertificateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AccessTokenProvider> _logger;

    public ClientCertificateProvider(IHttpContextAccessor httpContextAccessor, ILogger<AccessTokenProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public X509Certificate2? GetClientCertificate(CancellationToken token = default)
    {
        var certBytesWithKey = _httpContextAccessor.HttpContext?.Session.GetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY);

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
        var anchorBytes = _httpContextAccessor.HttpContext?.Session.GetString(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE);
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