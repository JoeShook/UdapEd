#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using UdapEd.Shared.Model.CdsHooks;

namespace UdapEd.Shared.Services;

public interface ICdsService
{
    Task<List<CdsServiceViewModel>?> FetchCdsServices();
}
