#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services.Fhir;

public class CustomDecompressionHandler : DelegatingHandler
{
    private readonly IFhirClientOptionsProvider _provider;
    
    public CustomDecompressionHandler(IFhirClientOptionsProvider provider)
    {
        _provider = provider;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await _provider.GetDecompression())
        {
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
        }
        else
        {
            request.Headers.AcceptEncoding.Clear();
        }

        return await base.SendAsync(request, cancellationToken);
    }
}