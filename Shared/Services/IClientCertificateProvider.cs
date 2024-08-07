using System.Security.Cryptography.X509Certificates;

namespace UdapEd.Shared.Services;

public interface IClientCertificateProvider
{
    X509Certificate2? GetClientCertificate(CancellationToken token = default);
    X509Certificate2Collection? GetAnchorCertificates(CancellationToken token = default);
}