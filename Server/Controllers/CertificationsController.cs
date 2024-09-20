#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Udap.Model;
using Udap.Model.Registration;
using Udap.Util.Extensions;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Registration;
using static Udap.Model.UdapConstants;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class CertificationsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CertificationsController> _logger;
    private readonly IConfiguration _configuration;

    public CertificationsController(HttpClient httpClient, ILogger<CertificationsController> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPut("LoadTestCertificate")]
    public IActionResult LoadTestCertificate([FromBody] string testClientCert)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = false
        };

        try
        {
            var certificate = new X509Certificate2(testClientCert, "udap-test", X509KeyStorageFlags.Exportable);
         
            var subjectName = certificate.SubjectName.Name;
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            HttpContext.Session.SetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE, 0);
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

            result.PublicKeyAlgorithm = GetPublicKeyAlgorithm(certificate);
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            result.CertLoaded = CertLoadedEnum.InvalidPassword;

            return Ok(result);
        }

        return Ok(result);
    }

    [HttpPost("UploadCertificate")]
    public IActionResult UploadCertificate([FromBody] string base64String)
    {
        HttpContext.Session.SetString(UdapEdConstants.CERTIFICATION_CERTIFICATE, base64String);
        HttpContext.Session.SetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE, 1);

        return Ok();
    }

    [HttpPost("ValidateCertificate")]
    public IActionResult ValidateCertificate([FromBody] string password)
    {
        // Console.WriteLine(HttpContext.Session.GetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE));
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (HttpContext.Session.GetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE) ?? 0) != 0
        };

        var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.CERTIFICATION_CERTIFICATE);

        if (clientCertSession == null)
        {
            return Ok(CertLoadedEnum.Negative);
        }

        var certBytes = Convert.FromBase64String(clientCertSession);
        try
        {
            var certificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);
            var subjectName = certificate.SubjectName.Name;
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
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

            result.PublicKeyAlgorithm = GetPublicKeyAlgorithm(certificate);
            result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);
            result.CertLoaded = CertLoadedEnum.InvalidPassword;
            return Ok(result);
        }

        return Ok(result);
    }

    [HttpGet("IsClientCertificateLoaded")]
    public IActionResult IsClientCertificateLoaded()
    {
        // Console.WriteLine(HttpContext.Session.GetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE));
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (HttpContext.Session.GetInt32(UdapEdConstants.CERTIFICATION_UPLOADED_CERTIFICATE) ?? 0) != 0
        };

        try
        {
            var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.CERTIFICATION_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = HttpContext.Session.GetString(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

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

                result.PublicKeyAlgorithm = GetPublicKeyAlgorithm(certificate);
                result.Issuer = certificate.IssuerName.EnumerateRelativeDistinguishedNames().FirstOrDefault()?.GetSingleElementValue() ?? string.Empty;
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return Ok(result);
        }
    }

    [HttpPost("BuildSoftwareStatement")]
    public IActionResult BuildSoftwareStatementWithHeaderForClientCredentials(
        [FromBody] UdapCertificationAndEndorsementDocument request,
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
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
            .BuildSoftwareStatement(alg);
                
        var tokenHandler = new JsonWebTokenHandler();
        var jsonToken = tokenHandler.ReadToken(signedSoftwareStatement);
        var requestToken = jsonToken as JsonWebToken;

        if (requestToken == null)
        {
            return BadRequest("Failed to read signed software statement using JsonWebTokenHandler");
        }

        var result = new RawSoftwareStatementAndHeader
        {
            Header = requestToken.EncodedHeader.DecodeJwtHeader(),
            SoftwareStatement = Base64UrlEncoder.Decode(requestToken.EncodedPayload),
            Scope = request.Scope
        };

        return Ok(result);
    }

    [HttpPost("BuildRequestBody")]
    public IActionResult BuildRequestBodyForClientCredentials(
       [FromBody] RawSoftwareStatementAndHeader request,
       [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.CERTIFICATION_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
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
            .BuildSoftwareStatement(alg);
        
        return Ok(signedSoftwareStatement);
    }

    private string GetPublicKeyAlgorithm(X509Certificate2 certificate)
    {
        string keyAlgOid = certificate.GetKeyAlgorithm();
        var oid = new Oid(keyAlgOid);

        if (oid.Value == "1.2.840.113549.1.1.1")
        {
            return "RS";
        }

        if (oid.Value == "1.2.840.10045.2.1")
        {
            return "ES";
        }

        return "";
    }
}
