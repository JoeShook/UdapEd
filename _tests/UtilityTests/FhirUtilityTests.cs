using System.Text.Json;
using Firely.Fhir.Packages;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Xunit.Abstractions;
using Xunit.Sdk;
using T = System.Threading.Tasks;

namespace UtilityTests;

public class FhirUtilityTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private const string PACKAGESERVER = "http://packages.simplifier.net";

    public FhirUtilityTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async T.Task FhirTermExternalTest()
    {
        FhirPackageSource _clientResolver = new(new ModelInspector(FhirRelease.R4), PACKAGESERVER, new string[] { "hl7.fhir.r4.core@4.0.1" });
        var termService = new LocalTerminologyService(resolver: _clientResolver);
        var p = new ExpandParameters().WithValueSet(url: "http://hl7.org/fhir/us/identity-matching/ValueSet/Identity-Identifier-vs");
        await termService.Expand(p); //Don't that is fails.  Just want the package to download

    }

    /// <summary>
    /// Run this test second and use the json output as a expanded Identity-Identifier-vs.  Otherwise it is just too expensive to
    /// start up a cloud run instance and always downloading the bigger dependency of hl7.fhir.r4.core@4.0.1
    ///
    /// I am starting to understand why a terminology server is important.  They probably have strategies to pre expand and always being
    /// hot and ready to server data.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async T.Task FhirTermTest()
    {
        IAsyncResourceResolver coreSource = new FhirPackageSource(ModelInfo.ModelInspector, PACKAGESERVER, new string[] {  "hl7.fhir.r4.expansions@4.0.1" });
        var coreResolver = new CachedResolver(coreSource);

        IAsyncResourceResolver localSource = new FhirPackageSource(ModelInfo.ModelInspector, @"hl7.fhir.us.identity-matching-2.0.0-draft.tgz");
        var resourceResolver = new CachedResolver(localSource);

        var multiResourceResolver = new MultiResolver(coreResolver, resourceResolver);

        var termService = new LocalTerminologyService(resolver: multiResourceResolver, new ValueSetExpanderSettings() { IncludeDesignations = true });
        var p = new ExpandParameters()
            .WithValueSet(url: "http://hl7.org/fhir/us/identity-matching/ValueSet/Identity-Identifier-vs");
        
        var idiValueSet = await termService.Expand(p, useGet:true) as ValueSet;
        _testOutputHelper.WriteLine(await new FhirJsonSerializer().SerializeToStringAsync(idiValueSet));
    }

    
    [Fact]
    public void ShowSearchParameters()
    {
        foreach (var searchParamDefinition in ModelInfo.SearchParameters.Where(sp => sp.Resource == "Organization"))
        {
            _testOutputHelper.WriteLine($"{searchParamDefinition.Name}: {searchParamDefinition.Description}");
        }
    }

    [Fact(Skip = "")]
    public async T.Task CapabilityStatement()
    {
        var capabilityStatement = await new FhirJsonParser().ParseAsync<CapabilityStatement>(await File.ReadAllTextAsync(@"C:\Users\Joseph.Shook\.fhir\packages\hl7.fhir.us.ndh#1.0.0-ballot\package\CapabilityStatement-national-directory-api-server.json"));

        foreach (var resourceComponent in capabilityStatement.Rest.SelectMany(restComponent => restComponent.Resource))
        {
            _testOutputHelper.WriteLine(resourceComponent.Type);

            _testOutputHelper.WriteLine("\tSearch Parameters");
            foreach (var searchParam in resourceComponent.SearchParam)
            {
                _testOutputHelper.WriteLine($"\t\t{searchParam.Name} :: {searchParam.Documentation}");
            }

            _testOutputHelper.WriteLine("\t_includes");
            foreach (var include in resourceComponent.SearchInclude)
            {
                _testOutputHelper.WriteLine($"\t\t{include} ");
            }

            _testOutputHelper.WriteLine("\t_revincludes");
            foreach (var revInclude in resourceComponent.SearchRevInclude)
            {
                _testOutputHelper.WriteLine($"\t\t{revInclude} ");
            }
        }

        _testOutputHelper.WriteLine(string.Empty);

    }
}