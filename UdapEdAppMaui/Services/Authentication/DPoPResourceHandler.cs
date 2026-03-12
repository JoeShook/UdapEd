#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using UdapEd.Shared;
using UdapEd.Shared.Services;
using static UdapEdAppMaui.Extensions.SessionExtensions;

namespace UdapEdAppMaui.Services.Authentication;

/// <summary>
/// Adds DPoP proof headers to outgoing FHIR resource requests when DPoP is enabled.
/// MAUI equivalent of Server's DPoPResourceHandler — reads state from SecureStorage
/// instead of HttpContext.Session.
/// </summary>
public class DPoPResourceHandler : DelegatingHandler
{
    private readonly ILogger<DPoPResourceHandler> _logger;

    public DPoPResourceHandler(ILogger<DPoPResourceHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var dpopEnabled = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_ENABLED);

        if (dpopEnabled == "true")
        {
            var accessToken = request.Headers.Authorization?.Parameter;

            if (!string.IsNullOrEmpty(accessToken))
            {
                // Change auth scheme from Bearer to DPoP
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("DPoP", accessToken);

                var clientCertWithKey = await RetrieveFromChunks(UdapEdConstants.UDAP_CLIENT_CERTIFICATE_WITH_KEY);
                var signingAlg = await SecureStorage.Default.GetAsync(UdapEdConstants.DPOP_SIGNING_ALG);

                if (clientCertWithKey != null && signingAlg != null)
                {
                    var certBytes = Convert.FromBase64String(clientCertWithKey);
                    var flags = X509KeyStorageFlags.DefaultKeySet;
                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                    {
                        flags = X509KeyStorageFlags.Exportable;
                    }

                    var clientCert = new X509Certificate2(
                        certBytes, "ILikePasswords", flags);

                    var proofToken = DPoPProofTokenGenerator.GenerateProofToken(
                        clientCert,
                        signingAlg,
                        request.Method.Method,
                        GetRequestUri(request),
                        accessToken);

                    request.Headers.Add("DPoP", proofToken);

                    _logger.LogDebug(
                        "Added DPoP proof for {Method} {Uri}",
                        request.Method.Method,
                        request.RequestUri);
                }
                else
                {
                    _logger.LogWarning(
                        "DPoP is enabled but client certificate or signing algorithm is missing from SecureStorage");
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Per RFC 9449 Section 4.2, the htu claim must be the HTTP URI
    /// without query and fragment parts.
    /// </summary>
    private static string GetRequestUri(HttpRequestMessage request)
    {
        if (request.RequestUri == null)
            return string.Empty;

        var uri = request.RequestUri;
        return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
    }
}
