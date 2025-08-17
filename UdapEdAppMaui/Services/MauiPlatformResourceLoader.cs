#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;

public sealed class MauiPlatformResourceLoader : IPlatformResourceLoader
{
    public async Task<string> LoadTextAsync(string logicalName, CancellationToken cancellationToken = default)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}