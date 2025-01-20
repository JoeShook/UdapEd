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

namespace UdapEdAppMaui.Services.Fhir;

public class FhirClientOptionsProvider : IFhirClientOptionsProvider
{
    public async Task SetDecompression(bool enabled)
    {
        await SecureStorage.Default.SetAsync(UdapEdConstants.FhirClient.EnableDecompression, enabled.ToString());
    }

    public async Task<bool> GetDecompression()
    {
        if (bool.TryParse(
                await SecureStorage.Default.GetAsync(UdapEdConstants.FhirClient.EnableDecompression),
                out bool enabled))
        {
            return enabled;
        }

        return true;
    }
}
