#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;

namespace UdapEd.Shared.Search;

public class ResourceToSearchParamMap
{
    public ResourceToSearchParamMap(string resource, List<ModelInfo.SearchParamDefinition> @params)
    {
        Resource = resource;
        ParamDefinitions = @params;
    }

    public string Resource { get; }

    public List<ModelInfo.SearchParamDefinition>? ParamDefinitions { get; }

}
