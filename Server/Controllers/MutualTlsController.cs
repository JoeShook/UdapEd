#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Udap.Util.Extensions;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Model;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class MutualTlsController : Controller
{
    private readonly ILogger<RegisterController> _logger;
    private readonly IConfiguration _configuration;

    public MutualTlsController( ILogger<RegisterController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPut("UploadTestClientCertificate")]
    public IActionResult UploadTestClientCertificate([FromBody] string testClientCert)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            var certificate = new X509Certificate2(testClientCert, "udap-test", X509KeyStorageFlags.Exportable);
            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            result.SubjectAltNames = certificate
                .GetSubjectAltNames(n => n.TagNo == (int)X509Extensions.GeneralNameType.URI)
            .Select(tuple => tuple.Item2)
                .ToList();

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

    [HttpPost("UploadClientCertificate")]
    public IActionResult UploadClientCertificate([FromBody] string base64String)
    {
        HttpContext.Session.SetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE, base64String);

        return Ok();
    }

    [HttpPost("ValidateCertificate")]
    public IActionResult ValidateCertificate([FromBody] string password)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE);

        if (clientCertSession == null)
        {
            return Ok(CertLoadedEnum.Negative);
        }

        var certBytes = Convert.FromBase64String(clientCertSession);
        try
        {
            var certificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;

            if (certificate.NotAfter < DateTime.Now.Date)
            {
                result.CertLoaded = CertLoadedEnum.Expired;
            }

            result.SubjectAltNames = certificate
                .GetSubjectAltNames(n => n.TagNo == (int)X509Extensions.GeneralNameType.URI)
                .Select(tuple => tuple.Item2)
                .ToList();

            result.PublicKeyAlgorithm = GetPublicKeyAlgorithm(certificate);
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
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = HttpContext.Session.GetString(UdapEdConstants.MTLS_CLIENT_CERTIFICATE_WITH_KEY);

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
                    .GetSubjectAltNames(n => n.TagNo == (int)X509Extensions.GeneralNameType.URI)
                    .Select(tuple => tuple.Item2)
                    .ToList();

                result.PublicKeyAlgorithm = GetPublicKeyAlgorithm(clientCert);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return Ok(result);
        }
    }

}
