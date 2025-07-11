﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Udap.Model;
using Udap.Model.Registration;
using Udap.Util.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using Udap.Model.Statement;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model.Registration;
using Duende.IdentityModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using UdapEdAppMaui.Extensions;

namespace UdapEdAppMaui.Services;
internal class RegisterService : IRegisterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegisterService> _logger;
    private readonly IConfiguration _configuration;


    public RegisterService(HttpClient httpClient, ILogger<RegisterService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task UploadClientCertificate(string base64EncodedBytes)
    {
        var certBytes = Convert.FromBase64String(base64EncodedBytes);
        await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE, certBytes);
        await SessionExtensions.RemoveChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
    }

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatementForClientCredentials(
        UdapDynamicClientRegistrationDocument request, string signingAlgorithm)
    {
        var clientCertWithKey = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
        await SessionExtensions.RemoveChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored == null ? null : Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();

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
        else
        {
            dcrBuilder = UdapDcrBuilderForClientCredentialsUnchecked
                .Create(x5cCerts);
        }

        dcrBuilder.Document.Issuer = request.Issuer;
        dcrBuilder.Document.Subject = request.Subject;


        var document = dcrBuilder
            .WithAudience(request.Audience)
            .WithExpiration(request.Expiration.GetValueOrDefault())
            .WithJwtId(request.JwtId)
            .WithClientName(request.ClientName ?? UdapEdConstants.CLIENT_NAME)
            .WithContacts(request.Contacts)
            .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(request.Scope ?? string.Empty)
            .Build();

        if (request.Extensions != null && request.Extensions.Any())
        {
            document.Extensions = request.Extensions;
        }

        var signedSoftwareStatement =
            SignedSoftwareStatementBuilder<UdapDynamicClientRegistrationDocument>
                .Create(x5cCerts, document)
                .Build(signingAlgorithm);

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

    public async Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatementForAuthorizationCode(UdapDynamicClientRegistrationDocument request, string signingAlgorithm)
    {
        var clientCertWithKey = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
        await SessionExtensions.RemoveChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored == null ? null : Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();

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
            .WithExpiration(request.Expiration.GetValueOrDefault())
            .WithJwtId(request.JwtId)
            .WithClientName(request.ClientName ?? UdapEdConstants.CLIENT_NAME)
            .WithContacts(request.Contacts)
            .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(request.Scope ?? string.Empty)
            .WithResponseTypes(request.ResponseTypes)
            .WithRedirectUrls(request.RedirectUris?.ToPlatformSchemes())
            .WithLogoUri(request.LogoUri ?? "https://udaped.fhirlabs.net/images/UdapEdLogobyDesigner.png")
            .Build();

        if (request.Extensions != null && request.Extensions.Any())
        {
            document.Extensions = request.Extensions;
        }

        var signedSoftwareStatement =
            SignedSoftwareStatementBuilder<UdapDynamicClientRegistrationDocument>
                .Create(x5cCerts, document)
                .Build(signingAlgorithm);

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

    public async Task<UdapRegisterRequest?> BuildRequestBodyForClientCredentials(RawSoftwareStatementAndHeader? request, string signingAlgorithm)
    {
        var clientCertWithKey = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored == null ? null : Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();

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
            .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(document.Scope);

        if (document.Extensions != null && document.Extensions.Any())
        {
            dcrBuilder.Document.Extensions = document.Extensions;
        }

        if (!request.SoftwareStatement.Contains(UdapConstants.RegistrationDocumentValues.GrantTypes))
        {
            dcrBuilder.Document.GrantTypes = null;
        }

        var signedSoftwareStatement = dcrBuilder.BuildSoftwareStatement(signingAlgorithm);

        var requestBody = new UdapRegisterRequest
        (
            signedSoftwareStatement,
            UdapConstants.UdapVersionsSupportedValue
        );

        return requestBody;
    }

    public async Task<UdapRegisterRequest?> BuildRequestBodyForAuthorizationCode(RawSoftwareStatementAndHeader? request, string signingAlgorithm)
    {
        var clientCertWithKey = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            throw new Exception("Cannot find a certificate.  Reload the certificate.");
        }

        var certBytes = Convert.FromBase64String(clientCertWithKey);
        var flags = X509KeyStorageFlags.DefaultKeySet;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            flags |= X509KeyStorageFlags.Exportable;
        }
        var clientCert = new X509Certificate2(certBytes, "ILikePasswords", flags);
        var x5cCerts = new List<X509Certificate2> { clientCert };
        var intermediatesStored = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
        var intermediateCerts = intermediatesStored == null? null: Base64UrlEncoder.Decode(intermediatesStored).DeserializeCertificates();

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
            .WithTokenEndpointAuthMethod(UdapConstants.RegistrationDocumentValues.TokenEndpointAuthMethodValue)
            .WithScope(document.Scope!)
            .WithResponseTypes(document.ResponseTypes)
            .WithRedirectUrls(document.RedirectUris)
            .WithLogoUri(document.LogoUri!);

        if (document.Extensions != null && document.Extensions.Any())
        {
            dcrBuilder.Document.Extensions = document.Extensions;
        }

        var signedSoftwareStatement = dcrBuilder.BuildSoftwareStatement(signingAlgorithm);

        var requestBody = new UdapRegisterRequest
        (
            signedSoftwareStatement,
            UdapConstants.UdapVersionsSupportedValue
        );

        return requestBody;
    }

    public async Task<ResultModel<RegistrationDocument>?> Register(RegistrationRequest registrationRequest)
    {
        if (registrationRequest.UdapRegisterRequest == null)
        {
            return new ResultModel<RegistrationDocument>(
                $"{nameof(registrationRequest.UdapRegisterRequest)} is Null.");
        }

        var content = new StringContent(
            JsonSerializer.Serialize(registrationRequest.UdapRegisterRequest, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }),
            new MediaTypeHeaderValue("application/json"));

        //TODO: Centralize all registration in UdapClient.  See RegisterTieredClient
        var response = await _httpClient.PostAsync(registrationRequest.RegistrationEndpoint, content);


        if (!response.IsSuccessStatusCode)
        {
            var failResult = new ResultModel<RegistrationDocument?>(
                await response.Content.ReadAsStringAsync(),
                response.StatusCode,
                response.Version);

            return failResult!;
        }

        var resultRaw = await response.Content.ReadAsStringAsync();

        try
        {
            var result = new ResultModel<RegistrationDocument?>(
                JsonSerializer.Deserialize<RegistrationDocument>(resultRaw),
                response.StatusCode,
                response.Version);

            return result!;
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed Registration");
            _logger.LogError(resultRaw);

            return new ResultModel<RegistrationDocument>(ex.Message);
        }
    }

    public async Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        var clientCertSession = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE);

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
            var flags = X509KeyStorageFlags.DefaultKeySet;
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                flags |= X509KeyStorageFlags.Exportable;
            }
            var certificate = new X509Certificate2(certBytes, password, flags);

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY, clientCertWithKeyBytes);
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
            var clientCertSession = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE);

            if (clientCertSession != null)
            {
                result.CertLoaded = CertLoadedEnum.InvalidPassword;
            }
            else
            {
                result.CertLoaded = CertLoadedEnum.Negative;
            }

            var certBytesWithKey = await SessionExtensions.RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

            if (certBytesWithKey != null)
            {
                var certBytes = Convert.FromBase64String(certBytesWithKey);
                var flags = X509KeyStorageFlags.DefaultKeySet;
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    flags |= X509KeyStorageFlags.Exportable;
                }
                var certificate = new X509Certificate2(certBytes, "ILikePasswords", flags);
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

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.Message);

            return result;
        }
    }

    public async Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        var result = new CertificateStatusViewModel
        {
            CertLoaded = CertLoadedEnum.Negative
        };

        try
        {
            await using var fileStream = await FileSystem.Current.OpenAppPackageFileAsync(certificateName);
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            var certBytes = ms.ToArray();


            X509Certificate2 certificate;

            var flags = X509KeyStorageFlags.DefaultKeySet;
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                flags |= X509KeyStorageFlags.Exportable;
            }
            
            try
            {
                certificate = new X509Certificate2(certBytes, "udap-test", flags);
            }
            catch
            {
                certificate = new X509Certificate2(certBytes, _configuration["sampleKeyC"], flags);
            }

            var clientCertWithKeyBytes = certificate.Export(X509ContentType.Pkcs12, "ILikePasswords");
            await SessionExtensions.StoreInChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY, clientCertWithKeyBytes);
            await SessionExtensions.RemoveChunks(UdapEdConstants.UDAP_INTERMEDIATE_CERTIFICATES);
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

            return result;
        }

        return result;
    }

    /// <summary>
    /// This service currently gets all scopes from Metadata published supported scopes.
    /// In the future we could maintain session data or local data to retain previous
    /// user preferences.
    /// </summary>
    /// <param name="scopes"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public string GetScopes(ICollection<string>? scopes)
    {
        return scopes.ToSpaceSeparatedString();
    }

    public string? GetScopesForClientCredentials(ICollection<string>? scopes,
        bool smartV1Scopes = true,
        bool smartV2Scopes = true)
    {
        if (scopes != null)
        {
            return scopes
                .Where(s => !s.StartsWith("user") &&
                            !s.StartsWith("patient") &&
                            !s.StartsWith("openid") &&
                            KeepSmartVersion(s, smartV1Scopes, smartV2Scopes))
                .Take(10).ToList()
                .ToSpaceSeparatedString();
        }

        return null;
    }

    public string GetScopesForAuthorizationCode(ICollection<string>?
            scopes,
        bool tieredOauth = false,
        bool oidcScope = true,
        string? scopeLevel = null,
        bool smartLaunch = false,
        bool smartV1Scopes = true,
        bool smartV2Scopes = true)
    {
        var published = scopes == null ? new List<string>() :
            scopes
                .Where(s => KeepSmartVersion(s, smartV1Scopes, smartV2Scopes))
                .ToList();



        var enrichScopes = new List<string>();

        if (tieredOauth)
        {
            enrichScopes.Add(UdapConstants.StandardScopes.Udap);
        }

        if (oidcScope)
        {
            enrichScopes.Add(OidcConstants.StandardScopes.OpenId);
        }

        if (smartLaunch && scopeLevel == "patient")
        {
            enrichScopes.Add($"launch/{scopeLevel}");
        }

        if (smartLaunch && scopeLevel == "user")
        {
            enrichScopes.Add($"launch");
        }

        if (published.Any() && !scopeLevel.IsNullOrEmpty())
        {
            var selectedScopes = published
                .Where(s => s.StartsWith(scopeLevel))
                .Take(10).ToList();

            enrichScopes.AddRange(selectedScopes);
        }
        else if (!scopeLevel.IsNullOrEmpty())
        {
            enrichScopes.Add($"{scopeLevel}/*.read");
        }

        return enrichScopes.ToSpaceSeparatedString();
    }

    private static bool KeepSmartVersion(string scope, bool smartV1Scopes, bool smartV2Scopes)
    {
        if (!smartV1Scopes)
        {
            var smartV1Regex = new Regex(@"^(system|user|patient)[\/].*\.(read|write)$");
            var match = smartV1Regex.Match(scope);
            if (match.Success)
            {
                return false;
            }
        }

        if (!smartV2Scopes)
        {
            var smartV2Regex = new Regex(@"^(system|user|patient)[\/].*\.[cruds]+$");
            var match = smartV2Regex.Match(scope);
            if (match.Success)
            {
                return false;
            }
        }

        return true;
    }
}