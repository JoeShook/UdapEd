using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;

namespace UdapEd.Shared.Services;

/// <summary>
/// Specialize the FhirClient injecting a url resolver in the implementation of a IBaseUrlProvider
/// </summary>
public class FhirClientWithUrlProvider : FhirClient
{
    private HttpClient _httpClient;

    public FhirClientWithUrlProvider(IBaseUrlProvider baseUrlProvider, HttpClient httpClient, FhirClientSettings? settings = null, IStructureDefinitionSummaryProvider? provider = null)
         : base(baseUrlProvider.GetBaseUrl(), httpClient, settings)
    {
        _httpClient = httpClient;
    }

    public HttpClient HttpClient => _httpClient;
}

/// <summary>
/// Specialize the FhirClient injecting a url resolver in the implementation of a IBaseUrlProvider
/// </summary>
public class FhirMTlsClientWithUrlProvider : FhirClient
{
    public FhirMTlsClientWithUrlProvider(IBaseUrlProvider baseUrlProvider, HttpClient httpClient, FhirClientSettings? settings = null, IStructureDefinitionSummaryProvider? provider = null)
        : base(baseUrlProvider.GetBaseUrl(), httpClient, settings)
    {
        
    }
}
