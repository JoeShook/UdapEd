#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Udap.Model.Registration;
using Udap.Util.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Registration;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;
public class CertificationService : ICertificationService
{

    readonly HttpClient _httpClient;
    private readonly ILogger<CertificationService> _logger;

    public CertificationService(HttpClient httpClient, ILogger<CertificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = false
        };

        try
        {
            await using var fileStream = await FileSystem.Current.OpenAppPackageFileAsync(certificateName);
            var certBytes = new byte[fileStream.Length];
            await fileStream.ReadAsync(certBytes, 0, certBytes.Length);


            var certificate = new X509Certificate2(certBytes, "udap-test", X509KeyStorageFlags.Exportable);

            var subjectName = certificate.SubjectName.Name;
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SecureStorage.Default.SetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = subjectName;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            if (certificate.GetSubjectAltNames().Any())
            {
                result.SubjectAltNames = certificate
                    .GetSubjectAltNames()
                    .Select(tuple => tuple.Item2)
                    .ToList();
            }

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

    public async Task UploadCertificate(string certBytes)
    {
        await SecureStorage.Default.SetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE, certBytes);
    }


    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE) ?? false.ToString()) != false.ToString()
        };

        var clientCertSession = await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE);

        if (clientCertSession == null)
        {
            return result;
        }

        var certBytes = Convert.FromBase64String(clientCertSession);
        try
        {
            var certificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SecureStorage.Default.SetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            if (certificate.NotAfter < DateTime.Now.Date)
            {
                result.CertLoaded = CertLoadedEnum.Expired;
            }

            if (certificate.GetSubjectAltNames().Any())
            {
                result.SubjectAltNames = certificate
                    .GetSubjectAltNames()
                    .Select(tuple => tuple.Item2)
                    .ToList();
            }

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

    public async Task<CertificateStatusViewModel?> ClientCertificateLoadStatus()
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE) ?? false.ToString()) != false.ToString()
        };

        try
        {
            var clientCertSession = await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

            if (certBytesWithKey != null)
            {
                var certBytes = Convert.FromBase64String(certBytesWithKey);
                var certificate = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
                result.DistinguishedName = certificate.SubjectName.Name;
                result.Thumbprint = certificate.Thumbprint;
                result.CertLoaded = CertLoadedEnum.Positive;

                if (certificate.NotAfter < DateTime.Now.Date)
                {
                    result.CertLoaded = CertLoadedEnum.Expired;
                }

                if (certificate.GetSubjectAltNames().Any())
                {
                    result.SubjectAltNames = certificate
                        .GetSubjectAltNames()
                        .Select(tuple => tuple.Item2)
                        .ToList();
                }

                result.PublicKeyAlgorithm = certificate.GetPublicKeyAlgorithm();
                result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return result;
        }
    }

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatement(UdapCertificationAndEndorsementDocument request, string signingAlgorithm)
    {
        var clientCertWithKey = await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);

        var certificationBuilder = UdapCertificationsAndEndorsementBuilder.Create(request.CertificationName, clientCert);

        var signedSoftwareStatement = certificationBuilder
            .WithExpiration(request.Expiration)
            .WithAudience(request.Audience)
            .WithCertificationDescription(request.CertificationDescription)
            .WithCertificationUris(request.CertificationUris)
            .WithDeveloperName(request.DeveloperName)
            .WithDeveloperAddress(request.DeveloperAddress)
            .WithClientName(request.ClientName)
            .WithSoftwareId(request.SoftwareId)
            .WithSoftwareVersion(request.SoftwareVersion)
            .WithClientUri(request.ClientUri)
            .WithLogoUri(request.LogoUri)
            .WithTermsOfService(request.TosUri)
            .WithPolicyUri(request.PolicyUri)
            .WithContacts(request.Contacts)
            .WithRedirectUris(request.RedirectUris)
            .WithIPsAllowed(request.IpAllowed)
            .WithGrantTypes(request.GrantTypes)
            .WithResponseTypes(request.ResponseTypes) // omit for client_credentials rule
            .WithScope(request.Scope)
            .WithTokenEndpointAuthMethod(request.TokenEndpointAuthMethod)
            .BuildSoftwareStatement(signingAlgorithm);

        var tokenHandler = new JsonWebTokenHandler();
        var jsonToken = tokenHandler.ReadToken(signedSoftwareStatement);
        var requestToken = jsonToken as JsonWebToken;

        if (requestToken == null)
        {
            throw new Exception("Failed to read signed software statement using JsonWebTokenHandler");
        }

        var result = new RawSoftwareStatementAndHeader
        {
            Header = requestToken.EncodedHeader.DecodeJwtHeader(),
            SoftwareStatement = Base64UrlEncoder.Decode(requestToken.EncodedPayload),
            Scope = request.Scope
        };

        return result;
    }

    public async Task<string?> BuildRequestBody(RawSoftwareStatementAndHeader? request, string signingAlgorithm)
    {
        var clientCertWithKey = await SecureStorage.Default.GetAsync(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);

        var document = JsonSerializer
            .Deserialize<UdapCertificationAndEndorsementDocument>(request.SoftwareStatement)!;

        var certificationBuilder = UdapCertificationsAndEndorsementBuilderUnchecked.Create(document.CertificationName, clientCert);

        var signedSoftwareStatement = certificationBuilder
            .WithExpiration(document.Expiration)
            .WithAudience(document.Audience)
            .WithCertificationDescription(document.CertificationDescription)
            .WithCertificationUris(document.CertificationUris)
            .WithDeveloperName(document.DeveloperName)
            .WithDeveloperAddress(document.DeveloperAddress)
            .WithClientName(document.ClientName)
            .WithSoftwareId(document.SoftwareId)
            .WithSoftwareVersion(document.SoftwareVersion)
            .WithClientUri(document.ClientUri)
            .WithLogoUri(document.LogoUri)
            .WithTermsOfService(document.TosUri)
            .WithPolicyUri(document.PolicyUri)
            .WithContacts(document.Contacts)
            .WithRedirectUris(document.RedirectUris)
            .WithIPsAllowed(document.IpAllowed)
            .WithGrantTypes(document.GrantTypes)
            .WithResponseTypes(document.ResponseTypes) // omit for client_credentials rule
            .WithScope(document.Scope)
            .WithTokenEndpointAuthMethod(document.TokenEndpointAuthMethod)
            .BuildSoftwareStatement(signingAlgorithm);

        return signedSoftwareStatement;
    }
}
