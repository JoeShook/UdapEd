#region (c) 2025 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

namespace UdapEd.Shared.Services.Fhir;


public interface IFhirClientOptionsProvider
{
    Task SetDecompression(bool enabled);

    Task<bool> GetDecompression();
}
