#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Text.Json;
using UdapEd.Shared;
using UdapEd.Shared.Model;

namespace UdapEdAppMaui.Services;
public class HttpResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Capture outgoing request info before sending
        var info = new OutgoingRequestInfo
        {
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? string.Empty
        };

        foreach (var header in request.Headers)
        {
            info.Headers.Add(new OutgoingRequestInfo.HeaderItem
            {
                Name = header.Key,
                Value = string.Join(", ", header.Value)
            });
        }

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

        await SecureStorage.Default.SetAsync(
            UdapEdConstants.FhirClient.FhirOutgoingRequest,
            JsonSerializer.Serialize(info));

        var response = await base.SendAsync(request, cancellationToken);

        response.Headers.Add("X-Http-Version", response.Version.ToString());
        response.Headers.Add("X-Http-Status", response.StatusCode.ToString());
        response.Headers.Add("X-Http-IsSuccessStatusCode", response.IsSuccessStatusCode.ToString());

        return response;
    }
}
