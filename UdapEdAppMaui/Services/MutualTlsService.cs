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
using Udap.Common.Certificates;
using Udap.Util.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;
public class MutualTlsService : IMutualTlsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MutualTlsService> _logger;
    private readonly TrustChainValidator _trustChainValidator;

    public MutualTlsService(HttpClient httpClientClient, TrustChainValidator trustChainValidator, ILogger<MutualTlsService> logger)
    {
        _httpClient = httpClientClient;
        _trustChainValidator = trustChainValidator;
        _logger = logger;
    }

    public async Task UploadClientCertificate(string certBytes)
    {
        await SecureStorage.Default.SetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE, certBytes);
    }

    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            // Get the path to the certificate file in the app's assets
            await using var fileStream = await FileSystem.Current.OpenAppPackageFileAsync(certificateName);
            var certBytes = new byte[fileStream.Length];
            await fileStream.ReadAsync(certBytes, 0, certBytes.Length);

            var certificate = new X509Certificate2(certBytes, "udap-test", X509KeyStorageFlags.Exportable);
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SecureStorage.Default.SetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY,
                Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            result.SubjectAltNames = certificate
                .GetSubjectAltNames()
                .Select(tuple => tuple.Item2)
                .ToList();

            result.PublicKeyAlgorithm = certificate.GetPublicKeyAlgorithm();
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            result.CertLoaded = CertLoadedEnum.InvalidPassword;

            return result;
        }

        return result;
    }

    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        var clientCertSession = await SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE);

        if (clientCertSession == null)
        {
            return new CertificateStatusViewModel
            {
                CertLoaded = CertLoadedEnum.Negative
            };
        }

        var certBytes = Convert.FromBase64String(clientCertSession);
        try
        {
            var certificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SecureStorage.Default.SetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            if (certificate.NotAfter < DateTime.Now.Date)
            {
                result.CertLoaded = CertLoadedEnum.Expired;
            }

            result.SubjectAltNames = certificate
                .GetSubjectAltNames()
                .Select(tuple => tuple.Item2)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            result.CertLoaded = CertLoadedEnum.InvalidPassword;
            return result;
        }

        return result;
    }

    public async Task<CertificateStatusViewModel?> ClientCertificateLoadStatus()
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            var clientCertSession = await SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = await SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY);

            if (certBytesWithKey != null)
            {
                var certBytes = Convert.FromBase64String(certBytesWithKey);
                var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
                result.DistinguishedName = clientCert.SubjectName.Name;
                result.Thumbprint = clientCert.Thumbprint;
                result.CertLoaded = CertLoadedEnum.Positive;

                if (clientCert.NotAfter < DateTime.Now.Date)
                {
                    result.CertLoaded = CertLoadedEnum.Expired;
                }

                result.SubjectAltNames = clientCert
                    .GetSubjectAltNames()
                    .Select(tuple => tuple.Item2)
                    .ToList();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> UploadAnchorCertificate(string base64String)
    {
        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative };

        try
        {
            var certBytes = Convert.FromBase64String(base64String);
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            await SecureStorage.Default.SetAsync(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE, base64String);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading certificate");
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(base64String)} {base64String}");

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> LoadAnchor()
    {
        var anchorCertificate = "http://crl.fhircerts.net/certs/SureFhirmTLS_CA.cer";

        var result = new CertificateStatusViewModel { CertLoaded = CertLoadedEnum.Negative };

        try
        {
            var response = await _httpClient.GetAsync(new Uri(anchorCertificate));
            response.EnsureSuccessStatusCode();
            var certBytes = await response.Content.ReadAsByteArrayAsync();
            var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            await SecureStorage.Default.SetAsync(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE, Convert.ToBase64String(certBytes));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading anchor from {anchorCertificate}", anchorCertificate);
            _logger.LogDebug(ex,
                $"Failed loading certificate from {nameof(anchorCertificate)} {anchorCertificate}");

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> AnchorCertificateLoadStatus()
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            var base64String = await SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE);
            
            if (base64String != null)
            {
                var certBytes = Convert.FromBase64String(base64String);
                var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());
                result.DistinguishedName = certificate.SubjectName.Name;
                result.Thumbprint = certificate.Thumbprint;
                result.CertLoaded = CertLoadedEnum.Positive;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return result;
        }
    }

    public async Task<List<string>?> VerifyMtlsTrust(string publicCertificate)
    {
        var clientCertBytes = Convert.FromBase64String(publicCertificate);
        var clientCertificate = new X509Certificate2(clientCertBytes);

        var base64String = await SecureStorage.Default.GetAsync(UdapEdConstants.MTLS_ANCHOR_CERTIFICATE);

        if (base64String == null)
        {
            return new List<string>() { "mTLS anchor certificate is not loaded" };
        }

        var certBytes = Convert.FromBase64String(base64String);
        var certificate = X509Certificate2.CreateFromPem(certBytes.ToPemFormat());

        var notifications = new List<string>();
        _trustChainValidator.Problem += element => notifications.Add($"Validation Problem: {element.ChainElementStatus.Summarize(TrustChainValidator.DefaultProblemFlags)}");
        _trustChainValidator.Untrusted += message => notifications.Add($"Validation Untrusted: {message}");
        _trustChainValidator.Error += (_, message) => notifications.Add($"Validation Error: {message}");

        var trusted = _trustChainValidator.IsTrustedCertificate("UdapEd",
            clientCertificate,
            new X509Certificate2Collection(),
            new X509Certificate2Collection(certificate));

        if (!trusted && !notifications.Any())
        {
            notifications.Add("Failed validation for unknown reason");
        }

        return notifications;
    }
}
