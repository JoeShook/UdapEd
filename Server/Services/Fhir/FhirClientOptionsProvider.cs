#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared;
using UdapEd.Shared.Services.Fhir;

namespace UdapEd.Server.Services.Fhir;

public class FhirClientOptionsProvider : IFhirClientOptionsProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FhirClientOptionsProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpContextAccessor.HttpContext?.Session.SetString(UdapEdConstants.FhirClient.EnableDecompression, true.ToString());
    }


    public Task SetDecompression(bool enabled)
    {
        _httpContextAccessor.HttpContext?.Session.SetString(UdapEdConstants.FhirClient.EnableDecompression, enabled.ToString());

        return Task.CompletedTask;
    }


    public Task<bool> GetDecompression()
    {
        if (bool.TryParse(
                _httpContextAccessor.HttpContext?.Session.GetString(UdapEdConstants.FhirClient.EnableDecompression),
                out bool enabled))
        {
            return Task.FromResult(enabled);
        }

        return Task.FromResult(false);
    }
}
