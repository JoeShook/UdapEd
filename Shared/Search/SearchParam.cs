#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;
using Microsoft.IdentityModel.Tokens;

namespace UdapEd.Shared.Search;
public static class SearchParamLookup
{
    static SearchParamLookup()
    {
        var spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Organization" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Organization", spDefinition.Select(sp => sp).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Practitioner" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Practitioner", spDefinition.Select(sp => sp!).ToList()));

    }

    public static List<ResourceToSearchParamMap> Map { get; set; } = [];
}

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