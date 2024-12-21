using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Firely.Fhir.Packages;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Navigation;
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
        //_testOutputHelper.WriteLine(await new FhirJsonSerializer().SerializeToStringAsync(idiValueSet));

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

    [Fact(Skip = "")]
    public async T.Task ExpandValueSets()
    {
        IAsyncResourceResolver coreSource = new FhirPackageSource(ModelInfo.ModelInspector, PACKAGESERVER, new string[] { "hl7.fhir.r4.expansions@4.0.1" });
        var coreResolver = new CachedResolver(coreSource);

        var localSource = new FhirPackageSource(ModelInfo.ModelInspector, @"hl7.fhir.us.ndh#1.0.0-ballot.tgz");
        var resourceResolver = new CachedResolver(localSource);

        var multiResourceResolver = new MultiResolver(coreResolver, resourceResolver);

        var termService = new LocalTerminologyService(resolver: multiResourceResolver, new ValueSetExpanderSettings() { IncludeDesignations = true });
        // var p = new ExpandParameters().WithValueSet(url: "http://hl7.org/fhir/us/ndh/ValueSet/TrustProfileVS");

        // var idiValueSet = await termService.Expand(p, useGet: true) as ValueSet;
        // _testOutputHelper.WriteLine(await new FhirJsonSerializer().SerializeToStringAsync(idiValueSet));


        // foreach (var sd in localSource.ListArtifactNames()) //("http://hl7.org/fhir/us/ndh/StructureDefinition/ndh-Endpoint"))
        // {
        //     _testOutputHelper.WriteLine(sd);
        // }

        foreach (var uri in localSource.ListCanonicalUris().Where(l => l.StartsWith("http://hl7.org/fhir/us/ndh/StructureDefinition/ndh-End"))) //("http://hl7.org/fhir/us/ndh/StructureDefinition/ndh-Endpoint"))
        {
             _testOutputHelper.WriteLine(uri);
            var sd = await localSource.FindStructureDefinitionAsync(uri);

            // _testOutputHelper.WriteLine(await new FhirJsonSerializer().SerializeToStringAsync(sd));

            var slices = sd.Snapshot.Element;
            foreach (var endpointSlice in slices.Where(e => e.Path.StartsWith("Endpoint.extension") && !string.IsNullOrEmpty(e.SliceName)))
            {
                _testOutputHelper.WriteLine(endpointSlice.SliceName);
                _testOutputHelper.WriteLine("\t" + endpointSlice.Type.First().Profile.First());

                var resource = await multiResourceResolver.ResolveByCanonicalUriAsync(endpointSlice.Type.First().Profile.First());
                _testOutputHelper.WriteLine("\t" + resource.TypeName);

                foreach (var extensionSlice in resource.ToTypedElement().ToPoco<StructureDefinition>().Snapshot.Element.Where(e => e.Path.StartsWith("Extension.extension") && !string.IsNullOrEmpty(e.SliceName)))
                {
                    _testOutputHelper.WriteLine("\t\t" + extensionSlice.SliceName);

                    var extensionValue = resource.ToTypedElement().ToPoco<StructureDefinition>().Snapshot.Element.SingleOrDefault(e => e.ElementId.StartsWith($"Extension.extension:{extensionSlice.SliceName}.value[x]"));

                    var valueSetUrl = extensionValue?.Binding?.ValueSet;
                    if (valueSetUrl != null)
                    {
                        _testOutputHelper.WriteLine("\t\t\t" + extensionValue?.Binding?.ValueSet);
                        var p = new ExpandParameters().WithValueSet(url: extensionValue?.Binding?.ValueSet);
                        var valueSet = await termService.Expand(p, useGet: true) as ValueSet;
                        File.WriteAllText("", await new FhirJsonSerializer() { Settings = { Pretty = true } }.SerializeToStringAsync(valueSet));
                        // _testOutputHelper.WriteLine(await new FhirJsonSerializer(){Settings = { Pretty = true}}.SerializeToStringAsync(valueSet));
                    }

                    // if (extensionSlice.Value() is CodeableConcept)
                    // {
                    //     _testOutputHelper.WriteLine("\t\t\t" + extensionSlice.Value().Code.First());
                    // }
                }
            }
            
            Assert.NotNull(sd);
            Assert.IsType<StructureDefinition>(sd);

            // _testOutputHelper.WriteLine(new FhirJsonSerializer().SerializeToString(sd));
            // foreach (var sdChild in sd.Children)
            // {
            //     _testOutputHelper.WriteLine(sdChild.TypeName);
            // }

            // var nav = ElementDefinitionNavigator.ForSnapshot(sd);
            // var walker = new StructureDefinitionWalker(nav, localSource);
            // nav.MoveToFirstChild();
            
            // var joe = nav.MoveToNextSlice();
            // // walker.Child("Extension");
            // _testOutputHelper.WriteLine(nav.Current.Path);
            // nav.MoveToNextSlice();
            // // walker.Child("Extension");
            // _testOutputHelper.WriteLine(nav.Current.Path);

            // foreach (var extension in sd.Extension)
            // {
            //     _testOutputHelper.WriteLine(extension.Url);
            // }

        }

        // foreach (var sd in localSource.ListResourceUris()) //("http://hl7.org/fhir/us/ndh/StructureDefinition/ndh-Endpoint"))
        // {
        //     _testOutputHelper.WriteLine(sd);
        // }

        
    }

    [Fact(Skip = "")]
    public async T.Task mTLS_Call()
    {
        using var httpClientHandler = new HttpClientHandler();
        var clientCert = X509CertificateLoader.LoadPkcs12FromFile("FhirLabs_mTLS_Client.pfx", "udap-test", X509KeyStorageFlags.Exportable);
        httpClientHandler.ClientCertificates.Add(clientCert);
        var httpClient = new HttpClient(httpClientHandler);
        var weather = await httpClient.GetStringAsync("https://localhost:7057/weatherforecast");

        _testOutputHelper.WriteLine(weather);
    }
}