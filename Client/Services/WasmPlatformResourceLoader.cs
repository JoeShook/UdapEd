#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public sealed class WasmPlatformResourceLoader : IPlatformResourceLoader
{
    private readonly HttpClient _http;

    public WasmPlatformResourceLoader(HttpClient http) => _http = http;

    public Task<string> LoadTextAsync(string logicalName, CancellationToken cancellationToken = default)
        => _http.GetStringAsync($"Packages/{logicalName}", cancellationToken);
}