#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;

namespace UdapEd.Server.Services.Authentication;

/// <summary>
/// Captures detailed error information from upstream 401 responses (e.g., WWW-Authenticate header)
/// and stores it in HttpContext.Items for the controller to include in its response.
/// </summary>
public class UnauthorizedResponseHandler : DelegatingHandler
{
    public const string UnauthorizedDetailKey = "UnauthorizedDetail";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public UnauthorizedResponseHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var details = new List<string>();

            if (response.Headers.WwwAuthenticate.Any())
            {
                details.Add(response.Headers.WwwAuthenticate.ToString());
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                details.Add(body);
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (details.Any() && httpContext != null)
            {
                httpContext.Items[UnauthorizedDetailKey] = string.Join(" | ", details);
            }
        }

        return response;
    }
}
