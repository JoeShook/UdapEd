#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text;
using System.Text.Json;
using UdapEd.Shared;
using UdapEd.Shared.Model;

namespace UdapEd.Server.Services.Fhir;

/// <summary>
/// Captures the final outgoing HTTP request details (method, URL, headers, body)
/// and stores them in HttpContext.Items for propagation back to the Blazor client.
/// Registered as the innermost handler so it sees all headers added by the pipeline.
/// </summary>
public class RequestCaptureHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestCaptureHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var info = new OutgoingRequestInfo
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? string.Empty
        };

        // Capture request headers
        foreach (var header in request.Headers)
        {
            info.Headers.Add(new OutgoingRequestInfo.HeaderItem
            {
                Name = header.Key,
                Value = string.Join(", ", header.Value)
            });
        }

        // Capture content headers and body if present
        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                info.Headers.Add(new OutgoingRequestInfo.HeaderItem
                {
                    Name = header.Key,
                    Value = string.Join(", ", header.Value)
                });
            }

            info.Body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        // Serialize and base64 encode to fit in a response header
        var json = JsonSerializer.Serialize(info);
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[UdapEdConstants.FhirClient.FhirOutgoingRequest] =
                Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
