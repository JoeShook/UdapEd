#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
//
//  See LICENSE in the project root for license information.
// */
#endregion

using Udap.CdsHooks.Model;
using UdapEd.Shared.Model.CdsHooks;

namespace UdapEd.Shared.Services;

public interface ICdsService
{
    /// <summary>Reads a CDS Hooks discovery endpoint (…/cds-services) and lists the services it offers.</summary>
    Task<List<CdsServiceViewModel>?> FetchCdsServices(string discoveryUrl);

    /// <summary>Calls one CDS service with a hook request.</summary>
    Task<CdsResponse?> GetCdsService(string serviceUrl, CdsRequest request);
}
