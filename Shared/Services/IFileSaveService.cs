#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services;

public interface IFileSaveService
{
    Task SaveAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);
}