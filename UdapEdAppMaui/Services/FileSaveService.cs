#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using CommunityToolkit.Maui.Storage;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;

public sealed class FileSaveService : IFileSaveService
{
    public async Task SaveAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await FileSaver.Default.SaveAsync(fileName, stream, cancellationToken);
    }
}