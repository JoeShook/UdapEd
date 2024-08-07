#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEdAppMaui.Services;
public class HttpResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        
        response.Headers.Add("X-Http-Version", response.Version.ToString());
        response.Headers.Add("X-Http-Status", response.StatusCode.ToString());
        response.Headers.Add("X-Http-IsSuccessStatusCode", response.IsSuccessStatusCode.ToString());

        return response;
    }
}
