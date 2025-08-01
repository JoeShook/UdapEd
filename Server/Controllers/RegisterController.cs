﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Udap.Model;
using Udap.Model.Registration;
using Udap.Model.Statement;
using Udap.Util.Extensions;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Model.Registration;
using static Udap.Model.UdapConstants;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class RegisterController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegisterController> _logger;
    private readonly IConfiguration _configuration;


    public RegisterController(HttpClient httpClient, ILogger<RegisterController> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPut("UploadTestClientCertificate")]
    public IActionResult UploadTestClientCertificate([FromBody] string testClientCert)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = false
        };

        try
        {
            X509Certificate2 certificate;

            try
            {
                certificate = new X509Certificate2(testClientCert, "udap-test", X509KeyStorageFlags.Exportable);
            }
            catch
            {

                certificate = new X509Certificate2(testClientCert, _configuration["sampleKeyC"], X509KeyStorageFlags.Exportable);
            }

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
            HttpContext.Session.SetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE, 0);
            HttpContext.Session.Remove(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
            result.DistinguishedName = certificate.SubjectName.Name;
            result.Thumbprint = certificate.Thumbprint;
            result.CertLoaded = CertLoadedEnum.Positive;
            
            result.SubjectAltNames = certificate
                .GetSubjectAltNames(n => n.TagNo == (int)X509Extensions.GeneralNameType.URI)
            .Select(tuple => tuple.Item2)
                .ToList();
            
            result.PublicKeyAlgorithm = certificate.GetPublicKeyAlgorithm();
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

    [HttpPost("UploadClientCertificate")]
    public IActionResult UploadClientCertificate([FromBody] string base64String)
    {
        HttpContext.Session.SetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE, base64String);
        HttpContext.Session.SetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE, 1);
        HttpContext.Session.Remove(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);

        return Ok();
    }

    [HttpPost("ValidateCertificate")]
    public IActionResult ValidateCertificate([FromBody] string password)
    {
        // Console.WriteLine(HttpContext.Session.GetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE));
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (HttpContext.Session.GetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE) ?? 0) != 0
        };

        var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE);

        if (clientCertSession == null)
        {
            return Ok(CertLoadedEnum.Negative);
        }

        var certBytes = Convert.FromBase64String(clientCertSession);
        try
        {
            var certificate = new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            HttpContext.Session.SetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY, Convert.ToBase64String(clientCertWithKeyBytes));
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

            result.PublicKeyAlgorithm = certificate.GetPublicKeyAlgorithm();
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
        // Console.WriteLine(HttpContext.Session.GetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE));
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative,
            UserSuppliedCertificate = (HttpContext.Session.GetInt32(UdapEdConstants.UDAP_CLIENT_UPLOADED_CERTIFICATE) ?? 0) != 0
        };

        try
        {
            var clientCertSession = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

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

                result.SubjectAltNames = certificate
                    .GetSubjectAltNames(n => n.TagNo == (int)X509Extensions.GeneralNameType.URI)
                    .Select(tuple => tuple.Item2)
                    .ToList();

                result.PublicKeyAlgorithm = certificate.GetPublicKeyAlgorithm();
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

    [HttpPost("BuildSoftwareStatement/ClientCredentials")]
    public IActionResult BuildSoftwareStatementWithHeaderForClientCredentials(
        [FromBody] UdapDynamicClientRegistrationDocument request, 
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
        HttpContext.Session.Remove(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);

        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);

        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = HttpContext.Session.GetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored.DeserializeCertificates();

        if (intermediateCerts != null && intermediateCerts.Any())
        {
            x5cCerts.AddRange(intermediateCerts);
        }

        UdapDcrBuilderForClientCredentialsUnchecked dcrBuilder;

        if (request.GrantTypes == null || !request.GrantTypes.Any())
        {
            dcrBuilder = UdapDcrBuilderForClientCredentialsUnchecked
                .Cancel(clientCert);
        }
        else{
            dcrBuilder = UdapDcrBuilderForClientCredentialsUnchecked
                .Create(x5cCerts);
        }

        dcrBuilder.Document.Issuer = request.Issuer;
        dcrBuilder.Document.Subject = request.Subject;


        var document = dcrBuilder
            .WithAudience(request.Audience)
            .WithIssuedAt(request.IssuedAt.GetValueOrDefault())
            .WithExpiration(request.Expiration.GetValueOrDefault())
            .WithJwtId(request.JwtId)
            .WithClientName(request.ClientName ?? UdapEdConstants.CLIENT_NAME)
            .WithContacts(request.Contacts)
            .WithTokenEndpointAuthMethod(RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(request.Scope ?? string.Empty)
            .Build();

        if (request.Extensions != null && request.Extensions.Any())
        {
            document.Extensions = request.Extensions;
        }

        var signedSoftwareStatement =
            SignedSoftwareStatementBuilder<UdapDynamicClientRegistrationDocument>
                .Create(x5cCerts, document)
                .Build(alg);

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

    

    [HttpPost("BuildSoftwareStatement/AuthorizationCode")]
    public IActionResult BuildSoftwareStatementWithHeaderForAuthorizationCode(
        [FromBody] UdapDynamicClientRegistrationDocument request, 
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
        HttpContext.Session.Remove(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);

        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = HttpContext.Session.GetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored.DeserializeCertificates();

        if (intermediateCerts != null && intermediateCerts.Any())
        {
            x5cCerts.AddRange(intermediateCerts);
        }


        UdapDcrBuilderForAuthorizationCodeUnchecked dcrBuilder;
        
        if (request.GrantTypes == null || !request.GrantTypes.Any())
        {
            dcrBuilder = UdapDcrBuilderForAuthorizationCodeUnchecked
                .Cancel(clientCert);
        }
        else
        {
            dcrBuilder = UdapDcrBuilderForAuthorizationCodeUnchecked
                .Create(x5cCerts);
        }
        
        dcrBuilder.Document.Issuer = request.Issuer;
        dcrBuilder.Document.Subject = request.Subject;


        var document = dcrBuilder
            .WithAudience(request.Audience)
            .WithIssuedAt(request.IssuedAt.GetValueOrDefault())
            .WithExpiration(request.Expiration.GetValueOrDefault())
            .WithJwtId(request.JwtId)
            .WithClientName(request.ClientName ?? UdapEdConstants.CLIENT_NAME)
            .WithContacts(request.Contacts)
            .WithTokenEndpointAuthMethod(RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(request.Scope ?? string.Empty)
            .WithResponseTypes(request.ResponseTypes)
            .WithRedirectUrls(request.RedirectUris)
            .WithLogoUri(request.LogoUri ?? "https://udaped.fhirlabs.net/_content/UdapEd.Shared/images/UdapEdLogobyDesigner.png")
            .Build();

        if (request.Extensions != null && request.Extensions.Any())
        {
            document.Extensions = request.Extensions;
        }

        var signedSoftwareStatement =
            SignedSoftwareStatementBuilder<UdapDynamicClientRegistrationDocument>
                .Create(x5cCerts, document)
                .Build(alg);

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

    [HttpPost("BuildRequestBody/ClientCredentials")]
    public IActionResult BuildRequestBodyForClientCredentials(
        [FromBody] RawSoftwareStatementAndHeader request, 
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
        
        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = HttpContext.Session.GetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored.DeserializeCertificates();

        if (intermediateCerts != null && intermediateCerts.Any())
        {
            x5cCerts.AddRange(intermediateCerts);
        }

        var document = JsonSerializer
            .Deserialize<UdapDynamicClientRegistrationDocument>(request.SoftwareStatement)!;

        UdapDcrBuilderForClientCredentialsUnchecked dcrBuilder;

        if (document.GrantTypes == null || !document.GrantTypes.Any())
        {
            dcrBuilder = UdapDcrBuilderForClientCredentialsUnchecked
                .Cancel(clientCert);
        }
        else
        {
            dcrBuilder = UdapDcrBuilderForClientCredentialsUnchecked
                .Create(x5cCerts);
        }
        

        dcrBuilder.Document.Issuer = document.Issuer;
        dcrBuilder.Document.Subject = document.Subject;

        dcrBuilder.WithAudience(document.Audience)
            .WithIssuedAt(document.IssuedAt.GetValueOrDefault())
            .WithExpiration(document.Expiration.GetValueOrDefault())
            .WithJwtId(document.JwtId)
            .WithClientName(document.ClientName!)
            .WithContacts(document.Contacts)
            .WithTokenEndpointAuthMethod(RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(document.Scope) ;

        if (document.Extensions != null && document.Extensions.Any())
        {
            dcrBuilder.Document.Extensions = document.Extensions;
        }

        if (!request.SoftwareStatement.Contains(RegistrationDocumentValues.GrantTypes))
        {
            dcrBuilder.Document.GrantTypes = null;
        }

        var signedSoftwareStatement = dcrBuilder.BuildSoftwareStatement(alg);

        var requestBody = new UdapRegisterRequest
        (
            signedSoftwareStatement,
            UdapConstants.UdapVersionsSupportedValue
        );

        return Ok(requestBody);
    }

    [HttpPost("BuildRequestBody/AuthorizationCode")]
    public IActionResult BuildRequestBodyForAuthorizationCode(
        [FromBody] RawSoftwareStatementAndHeader request, 
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
    
        if (clientCertWithKey == null)
        {
            return BadRequest("Cannot find a certificate.  Reload the certificate.");
        }
    
        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = HttpContext.Session.GetString(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored.DeserializeCertificates();

        if (intermediateCerts != null && intermediateCerts.Any())
        {
            x5cCerts.AddRange(intermediateCerts);
        }

        var document = JsonSerializer
            .Deserialize<UdapDynamicClientRegistrationDocument>(request.SoftwareStatement)!;

        UdapDcrBuilderForAuthorizationCodeUnchecked dcrBuilder;

        if (document.GrantTypes == null || !document.GrantTypes.Any())
        {
            dcrBuilder = UdapDcrBuilderForAuthorizationCodeUnchecked
                .Cancel(clientCert);
        }
        else
        {
            dcrBuilder = UdapDcrBuilderForAuthorizationCodeUnchecked
                .Create(x5cCerts);

            dcrBuilder.Document.GrantTypes = document.GrantTypes;
        }

        dcrBuilder.Document.Issuer = document.Issuer;
        dcrBuilder.Document.Subject = document.Subject;

        dcrBuilder.WithAudience(document.Audience)
            .WithIssuedAt(document.IssuedAt.GetValueOrDefault())
            .WithExpiration(document.Expiration.GetValueOrDefault())
            .WithJwtId(document.JwtId)
            .WithClientName(document.ClientName!)
            .WithContacts(document.Contacts)
            .WithTokenEndpointAuthMethod(RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(document.Scope!)
            .WithResponseTypes(document.ResponseTypes)
            .WithRedirectUrls(document.RedirectUris)
            .WithLogoUri(document.LogoUri!);

        if (document.Extensions != null && document.Extensions.Any())
        {
            dcrBuilder.Document.Extensions = document.Extensions;
        }

        var signedSoftwareStatement = dcrBuilder.BuildSoftwareStatement(alg);

        var requestBody = new UdapRegisterRequest
        (
            signedSoftwareStatement,
            UdapConstants.UdapVersionsSupportedValue
        );
    
        return Ok(requestBody);
    }

    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        if (request.UdapRegisterRequest == null)
        {
            return BadRequest($"{nameof(request.UdapRegisterRequest)} is Null.");
        }

        var content = new StringContent(
            JsonSerializer.Serialize(request.UdapRegisterRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }), 
            new MediaTypeHeaderValue("application/json"));

        //TODO: Centralize all registration in UdapClient.  See RegisterTieredClient
        var response = await _httpClient.PostAsync(request.RegistrationEndpoint, content);


        if (!response.IsSuccessStatusCode)
        {
           var failResult = new ResultModel<RegistrationDocument?>(
                await response.Content.ReadAsStringAsync(),
                response.StatusCode,
                response.Version);

            return Ok(failResult);
        }
        
        var resultRaw = await response.Content.ReadAsStringAsync();
        try
        {
            var udapRegistrationDocument = JsonSerializer.Deserialize<UdapDynamicClientRegistrationDocument>(resultRaw);
            var normalizedRaw = udapRegistrationDocument.SerializeToJson();
            
            try
            {
                var result = new ResultModel<RegistrationDocument?>(
                    JsonSerializer.Deserialize<RegistrationDocument>(normalizedRaw),
                    response.StatusCode,
                    response.Version);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed Serialization");
                _logger.LogError(normalizedRaw);

                return BadRequest(ex);
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed Registration");
            _logger.LogError(resultRaw);

            return BadRequest(ex);
        }
    }
}