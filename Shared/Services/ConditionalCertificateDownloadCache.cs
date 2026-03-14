using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.X509;
using Udap.Common.Certificates;

namespace UdapEd.Shared.Services;

public class ConditionalCertificateDownloadCache : ICertificateDownloadCache
{
    private readonly ICertificateDownloadCache _inner;
    private readonly CertificateCacheSettings _settings;

    public ConditionalCertificateDownloadCache(ICertificateDownloadCache inner, CertificateCacheSettings settings)
    {
        _inner = inner;
        _settings = settings;
    }

    public async Task<X509Certificate2?> GetIntermediateCertificateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            await _inner.RemoveIntermediateAsync(url, cancellationToken);
        }

        return await _inner.GetIntermediateCertificateAsync(url, cancellationToken);
    }

    public async Task<X509Crl?> GetCrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            await _inner.RemoveCrlAsync(url, cancellationToken);
        }

        return await _inner.GetCrlAsync(url, cancellationToken);
    }

    public Task RemoveIntermediateAsync(string url, CancellationToken cancellationToken = default)
        => _inner.RemoveIntermediateAsync(url, cancellationToken);

    public Task RemoveCrlAsync(string url, CancellationToken cancellationToken = default)
        => _inner.RemoveCrlAsync(url, cancellationToken);
}
