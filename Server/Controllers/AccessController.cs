#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Udap.Client.Client.Extensions;
using Udap.Model;
using Udap.Model.Access;
using Udap.Model.Statement;
using Udap.Model.UdapAuthenticationExtensions;
using Udap.Util.Extensions;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Mappers;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEd.Server.Controllers;

[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class AccessController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RegisterController> _logger;

    public AccessController(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RegisterController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet("{authorizeQuery}")]
    public async Task<IActionResult> GetAuthorizationCode(string authorizeQuery, CancellationToken token)
    {
        var handler = new HttpClientHandler() { AllowAutoRedirect = false };
        var httpClient = new HttpClient(handler);
        
        var response = await httpClient
            .GetAsync(Base64UrlEncoder
                .Decode(authorizeQuery), cancellationToken: token);

        var cookies = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value;

        try
        {
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Found)
            {
                var message = await response.Content.ReadAsStringAsync(token);
                _logger.LogWarning(message);

                return Ok(new AccessCodeRequestResult
                {
                    Message = $"{response.StatusCode}:: {message}",
                    IsError = true
                });
            }

            var result = new AccessCodeRequestResult
            {
                RedirectUrl = response.Headers.Location?.AbsoluteUri,
                Cookies = cookies
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Problem(ex.Message);
        }
    }

    [HttpPost("BuildRequestToken/authorization_code")]
    public Task<IActionResult> RequestAccessTokenAuthCode(
        [FromBody] AuthorizationCodeTokenRequestModel tokenRequestModel,
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            return Task.FromResult<IActionResult>(BadRequest("Cannot find a certificate.  Reload the certificate."));
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

        UdapAuthorizationCodeTokenRequest tokenRequest;

        if (x5cCerts.Count > 1)
        {
            var now = DateTime.UtcNow;
            var jwtPayload = new JwtPayLoadExtension(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                new List<Claim>
                {
                    new(JwtClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer),
                    new(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId()),
                    new(JwtClaimTypes.Subject, tokenRequestModel.ClientId ?? "")
                },
                now,
                now.AddMinutes(5));

            var clientAssertion = SignedSoftwareStatementBuilder<JwtPayLoadExtension>
                .Create(x5cCerts, jwtPayload)
                .Build(alg);

            tokenRequest = new UdapAuthorizationCodeTokenRequest
            {
                Address = tokenRequestModel.TokenEndpointUrl,
                RequestUri = new Uri(tokenRequestModel.TokenEndpointUrl!),
                Code = tokenRequestModel.Code!,
                RedirectUri = tokenRequestModel.RedirectUrl!,
                ClientAssertion = new Duende.IdentityModel.Client.ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = clientAssertion
                },
                Udap = UdapConstants.UdapVersionsSupportedValue
            };
        }
        else
        {
            var tokenRequestBuilder = AccessTokenRequestForAuthorizationCodeBuilder.Create(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                clientCert,
                tokenRequestModel.RedirectUrl,
                tokenRequestModel.Code);

            tokenRequest = tokenRequestBuilder.Build(alg);
        }

        if (tokenRequestModel.CodeVerifier != null)
        {
            tokenRequest.CodeVerifier = tokenRequestModel.CodeVerifier;
        }

        var tokenRequestModel2 = tokenRequest.ToModel();

        if (tokenRequestModel.EnableDPoP)
        {
            tokenRequestModel2.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                clientCert,
                alg,
                "POST",
                tokenRequestModel.TokenEndpointUrl!);

            tokenRequestModel2.DPoPJkt = DPoPProofTokenGenerator.ComputeJwkThumbprint(clientCert);

            HttpContext.Session.SetString(UdapEdConstants.DPOP_ENABLED, "true");
            HttpContext.Session.SetString(UdapEdConstants.DPOP_SIGNING_ALG, alg);
        }
        else
        {
            HttpContext.Session.Remove(UdapEdConstants.DPOP_ENABLED);
            HttpContext.Session.Remove(UdapEdConstants.DPOP_SIGNING_ALG);
        }

        return Task.FromResult<IActionResult>(Ok(tokenRequestModel2));
    }

    [HttpPost("BuildRequestToken/client_credentials")]
    public Task<IActionResult> RequestAccessTokenClientCredentials(
        [FromBody] ClientCredentialsTokenRequestModel tokenRequestModel,
        [FromQuery] string alg)
    {
        var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);

        if (clientCertWithKey == null)
        {
            return Task.FromResult<IActionResult>(BadRequest("Cannot find a certificate.  Reload the certificate."));
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

        UdapClientCredentialsTokenRequest tokenRequest;

        if (x5cCerts.Count > 1)
        {
            var now = DateTime.UtcNow;
            var jwtPayload = new JwtPayLoadExtension(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                new List<Claim>
                {
                    new(JwtClaimTypes.IssuedAt, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer),
                    new(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId()),
                    new(JwtClaimTypes.Subject, tokenRequestModel.ClientId ?? "")
                },
                now,
                now.AddMinutes(5));

            if (tokenRequestModel.Extensions != null)
            {
                var payload = jwtPayload as Dictionary<string, object>;
                payload.Add(UdapConstants.JwtClaimTypes.Extensions, tokenRequestModel.Extensions);
            }

            var clientAssertion = SignedSoftwareStatementBuilder<JwtPayLoadExtension>
                .Create(x5cCerts, jwtPayload)
                .Build(alg);

            tokenRequest = new UdapClientCredentialsTokenRequest
            {
                Address = tokenRequestModel.TokenEndpointUrl,
                ClientAssertion = new Duende.IdentityModel.Client.ClientAssertion
                {
                    Type = OidcConstants.ClientAssertionTypes.JwtBearer,
                    Value = clientAssertion
                },
                Udap = UdapConstants.UdapVersionsSupportedValue,
                Scope = tokenRequestModel.Scope
            };
        }
        else
        {
            var tokenRequestBuilder = AccessTokenRequestForClientCredentialsBuilder.Create(
                tokenRequestModel.ClientId,
                tokenRequestModel.TokenEndpointUrl,
                clientCert);

            if (tokenRequestModel.Extensions != null)
            {
                foreach (var extension in tokenRequestModel.Extensions)
                {
                    tokenRequestBuilder.WithExtension(extension.Key, extension.Value);
                }
            }

            if (tokenRequestModel.Scope != null)
            {
                tokenRequestBuilder.WithScope(tokenRequestModel.Scope);
            }

            tokenRequest = tokenRequestBuilder.Build(alg);
        }

        var tokenRequestModel2 = tokenRequest.ToModel();

        if (tokenRequestModel.EnableDPoP)
        {
            tokenRequestModel2.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                clientCert,
                alg,
                "POST",
                tokenRequestModel.TokenEndpointUrl!);

            tokenRequestModel2.DPoPJkt = DPoPProofTokenGenerator.ComputeJwkThumbprint(clientCert);

            HttpContext.Session.SetString(UdapEdConstants.DPOP_ENABLED, "true");
            HttpContext.Session.SetString(UdapEdConstants.DPOP_SIGNING_ALG, alg);
        }
        else
        {
            HttpContext.Session.Remove(UdapEdConstants.DPOP_ENABLED);
            HttpContext.Session.Remove(UdapEdConstants.DPOP_SIGNING_ALG);
        }

        return Task.FromResult<IActionResult>(Ok(tokenRequestModel2));
    }

    [HttpPost("RequestToken/client_credentials")]
    public async Task<IActionResult> RequestAccessTokenForClientCredentials([FromBody] UdapClientCredentialsTokenRequestModel request)
    {
        var tokenRequest = request.ToUdapClientCredentialsTokenRequest();

        // Regenerate a fresh DPoP proof with current iat/jti if DPoP is enabled
        var dpopEnabled = HttpContext.Session.GetString(UdapEdConstants.DPOP_ENABLED);
        if (dpopEnabled == "true" && !string.IsNullOrEmpty(request.DPoPProofToken))
        {
            var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
            var signingAlg = HttpContext.Session.GetString(UdapEdConstants.DPOP_SIGNING_ALG);

            if (clientCertWithKey != null && signingAlg != null)
            {
                var certBytes = Convert.FromBase64String(clientCertWithKey);
                var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);

                tokenRequest.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                    clientCert,
                    signingAlg,
                    "POST",
                    tokenRequest.Address!);
            }
        }

        var tokenResponse = await _httpClient.UdapRequestClientCredentialsTokenAsync(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            ErrorDescription = tokenResponse.ErrorDescription,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType,
            Headers = JsonSerializer.Serialize(
                tokenResponse.HttpResponse.Headers,
                new JsonSerializerOptions{WriteIndented = true})
        };

        if (tokenResponseModel.AccessToken != null)
        {
            HttpContext.Session.SetString(UdapEdConstants.TOKEN, tokenResponseModel.AccessToken);
        }

        return Ok(tokenResponseModel);
    }

    [HttpPost("RequestToken/authorization_code")]
    public async Task<IActionResult> RequestAccessTokenForAuthorizationCode([FromBody] UdapAuthorizationCodeTokenRequestModel request)
    {
        var tokenRequest = request.ToUdapAuthorizationCodeTokenRequest();

        // Regenerate a fresh DPoP proof with current iat/jti if DPoP is enabled
        var dpopEnabled = HttpContext.Session.GetString(UdapEdConstants.DPOP_ENABLED);
        if (dpopEnabled == "true" && !string.IsNullOrEmpty(request.DPoPProofToken))
        {
            var clientCertWithKey = HttpContext.Session.GetString(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
            var signingAlg = HttpContext.Session.GetString(UdapEdConstants.DPOP_SIGNING_ALG);

            if (clientCertWithKey != null && signingAlg != null)
            {
                var certBytes = Convert.FromBase64String(clientCertWithKey);
                var clientCert = new X509Certificate2(certBytes, "ILikePasswords", X509KeyStorageFlags.Exportable);

                tokenRequest.DPoPProofToken = DPoPProofTokenGenerator.GenerateProofToken(
                    clientCert,
                    signingAlg,
                    "POST",
                    tokenRequest.Address!);
            }
        }

        var tokenResponse = await _httpClient.ExchangeCodeForTokenResponse(tokenRequest);

        var tokenResponseModel = new TokenResponseModel
        {
            Raw = tokenResponse.Json.AsJson(),
            IsError = tokenResponse.IsError,
            Error = tokenResponse.Error,
            ErrorDescription = tokenResponse.ErrorDescription,
            AccessToken = tokenResponse.AccessToken,
            IdentityToken = tokenResponse.IdentityToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            Scope = tokenResponse.Raw,
            TokenType = tokenResponse.TokenType,
            Headers = JsonSerializer.Serialize(
                tokenResponse.HttpResponse.Headers,
                new JsonSerializerOptions{WriteIndented = true})
        };

        if (tokenResponseModel.AccessToken != null)
        {
            HttpContext.Session.SetString(UdapEdConstants.TOKEN, tokenResponseModel.AccessToken);
        }

        return Ok(tokenResponseModel);
    }

    [HttpDelete]
    public async Task DeleteAccessToken()
    {
        HttpContext.Session.Remove(UdapEdConstants.TOKEN);

        await Task.FromResult(NoContent());
    }
}
