#region (c) 2023-2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services;

public interface IPlatformResourceLoader
{
    Task<string> LoadTextAsync(string logicalName, CancellationToken cancellationToken = default);
}