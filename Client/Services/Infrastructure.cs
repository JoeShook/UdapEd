#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.IO.Compression;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public class Infrastructure : IInfrastructure
{
    private HttpClient _httpClient;
    public Infrastructure(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetMyIp()
    {
       var response = await _httpClient.GetAsync("Metadata/MyIp");

       if (response.IsSuccessStatusCode)
       {
           return await response.Content.ReadAsStringAsync();
       }

       return string.Empty;
    }

    public async Task<byte[]> BuildMyTestCertificatePackage(List<string> subjAltNames)
    {
        var queryString = string.Join("&", subjAltNames.Select(name => $"subjAltNames={Uri.EscapeDataString(name)}"));
        var response = await _httpClient.GetAsync($"Infrastructure/BuildMyTestCertificatePackage?{queryString}");

        if (response.IsSuccessStatusCode)
        {
            var contentBase64 = await response.Content.ReadAsStringAsync();
            return Convert.FromBase64String(contentBase64);
        }

        return new byte[] { };
    }
}
