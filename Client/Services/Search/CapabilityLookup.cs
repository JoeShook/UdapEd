#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using UdapEd.Shared.Search;
using UdapEd.Shared.Services.Search;
using Task = System.Threading.Tasks.Task;

namespace UdapEd.Client.Services.Search;
public class CapabilityLookup : ICapabilityLookup
{
    private readonly HttpClient _httpClient;
    private bool _built;

    public CapabilityLookup(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public List<ResourceToSearchParamMap> SearchParamMap { get; set; } = [];
    public Dictionary<string, List<string>> IncludeMap { get; set; } = new Dictionary<string, List<string>>();
    public Dictionary<string, List<string>> ReverseIncludeMap { get; set; } = new Dictionary<string, List<string>>();

    public async Task Build()
    {
        if (_built)
        {
            return;
        }

        //
        // Get definition from Capability Statement
        //

        // Maybe read from metadata first?
        // There are two other less constrained capability statement you could use.

        var response = await _httpClient.GetAsync("_content/UdapEd.Shared/CapabilityStatement-national-directory-api-server.json");
        var capabilityStatement = await new FhirJsonParser().ParseAsync<CapabilityStatement>(await response.Content.ReadAsStringAsync());
        

        foreach (var resourceComponent in capabilityStatement.Rest.SelectMany(restComponent => restComponent.Resource))
        {
            var spDefinition = new List<ModelInfo.SearchParamDefinition>();
            foreach (var searchParam in resourceComponent.SearchParam)
            {
                //_testOutputHelper.WriteLine($"\t\t{searchParam.Name} :: {searchParam.Type}");
                spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = resourceComponent.Type, Code = searchParam.Name, Type = searchParam.Type.GetValueOrDefault(), Url = searchParam.Definition, Description = searchParam.Documentation});

            }
            
            SearchParamMap.Add(new ResourceToSearchParamMap(resourceComponent.Type, spDefinition.Select(sp => sp).ToList()));
            
            IncludeMap.Add(resourceComponent.Type, resourceComponent.SearchInclude.ToList());
            ReverseIncludeMap.Add(resourceComponent.Type, resourceComponent.SearchRevInclude.ToList());
        }

        _built = true;
    }
}