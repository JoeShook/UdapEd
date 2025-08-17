#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Microsoft.JSInterop;
using UdapEd.Shared.Services;

namespace UdapEd.Client.Services;

public sealed class FileSaveService : IFileSaveService
{
    private readonly IJSRuntime _js;

    public FileSaveService(IJSRuntime js) => _js = js;

    public Task SaveAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default)
        => _js.InvokeVoidAsync("downloadFileFromBytes", cancellationToken, content, contentType, fileName).AsTask();
}