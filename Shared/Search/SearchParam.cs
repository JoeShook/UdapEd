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
using System.Collections.Generic;

namespace UdapEd.Shared.Search;
public static class SearchParamLookup
{
    static SearchParamLookup()
    {
        var spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Organization" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Organization", spDefinition.Select(sp => sp).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Practitioner" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Practitioner", spDefinition.Select(sp => sp!).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "PractitionerRole" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("PractitionerRole", spDefinition.Select(sp => sp!).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "OrganizationAffiliation" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("OrganizationAffiliation", spDefinition.Select(sp => sp!).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "InsurancePlan" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("InsurancePlan", spDefinition.Select(sp => sp!).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Endpoint" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Endpoint", spDefinition.Select(sp => sp!).ToList()));
        spDefinition = ModelInfo.SearchParameters.Where(sp => sp.Resource == "Location" && !sp.Name.IsNullOrEmpty()).ToList();
        Map.Add(new ResourceToSearchParamMap("Location", spDefinition.Select(sp => sp!).ToList()));
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

public static class IncludesLookup
{
    static IncludesLookup()
    {
        Map.Add("Organization", ModelInfo.SearchParameters.Where(sp => sp.Resource == "Organization" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("Practitioner", ModelInfo.SearchParameters.Where(sp => sp.Resource == "Practitioner" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("PractitionerRole", ModelInfo.SearchParameters.Where(sp => sp.Resource == "PractitionerRole" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("OrganizationAffiliation", ModelInfo.SearchParameters.Where(sp => sp.Resource == "OrganizationAffiliation" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("InsurancePlan", ModelInfo.SearchParameters.Where(sp => sp.Resource == "InsurancePlan" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("Endpoint", ModelInfo.SearchParameters.Where(sp => sp.Resource == "Endpoint" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);
        Map.Add("Location", ModelInfo.SearchParameters.Where(sp => sp.Resource == "Location" && !sp.Name.IsNullOrEmpty() && sp is { Target: not null, Expression: not null } && !sp.Expression.EndsWith("partOf")).Select(sp => sp.Expression!.Replace('.', ':')).ToList()!);

        var reversedExpressions = Map.SelectMany(m => m.Value.Select(e =>
        {
            var parts = e.Split(":");
            return new Tuple<string, string>(parts[^1].Substring(0, 1).ToUpper() + parts[^1].Substring(1), parts[^2].ToLower());
        })).Where(t => Map.ContainsKey(t.Item1));

        foreach (var group in reversedExpressions.GroupBy(re => re.Item2))
        {
            Reverse.Add(group.Key.Substring(0, 1).ToUpper() + group.Key.Substring(1), group.Select(g => g.Item1 + ":" + g.Item2).ToList());
        }
    }

    public static Dictionary<string, List<string>> Map { get; set; } = new Dictionary<string, List<string>>();
    public static Dictionary<string, List<string>> Reverse { get; set; } = new Dictionary<string, List<string>>();
}

