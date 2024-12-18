#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Utility;
using UdapEd.Shared;
using UdapEd.Shared.Services;
using Microsoft.Maui.Storage;

namespace UdapEdAppMaui.Services;

public class BaseUrlProvider : IBaseUrlProvider
{
    public Uri GetBaseUrl()
    {
        // I did this on purpose
        var baseUrl = Task.Run(() => SecureStorage.Default.GetAsync(UdapEdConstants.BASE_URL)).Result;

        if (baseUrl == null)
        {
            baseUrl = "https://fhirlabs.net/fhir/r4";
        }

        return new Uri(baseUrl.EnsureEndsWith("/"));
    }
}

