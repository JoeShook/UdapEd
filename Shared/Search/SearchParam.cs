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
     
        
        spDefinition.Add(new ModelInfo.SearchParamDefinition(){ Resource = "Endpoint", Name = "EndpointAccessControlMechanismSearchParameter", Code = "access-control-mechanism", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific access-control-mechanism"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-endpointAccessControlMechanism').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-access-control-mechanism" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointConnectionTypeVersionSearchParameter", Code = "connection-type-version", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific connection-type-version"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-endpoint-connection-type-version').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-connection-type-version" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointDynamicRegistrationTrustProfileSearchParameter", Code = "dynamic-registration-trust-profile", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific dynamic-registration-trust-profile"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-dynamicRegistration').extension.where(url='trustProfile').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-dynamic-registration-trust-profile" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointIheConnectionTypeSearchParameter", Code = "ihe-connection-type", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific ihe-connection-type"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-endpoint-ihe-specific-connection-type').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-ihe-connection-type" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointNonfhirUsecaseTypeSearchParameter", Code = "nonfhir-usecase-type", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific nonfhir-usecase-type"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-endpoint-non-fhir-usecase').extension.where(url='endpointUsecasetype').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-nonfhir-usecase-type" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointTrustFrameworkTypeSearchParameter", Code = "trust-framework-type", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific trust-framework-type"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-trustFramework').extension.where(url='trustFrameworkType').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-trust-framework-type" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointUsecaseTypeSearchParameter", Code = "usecase-type", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific usecase-type"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-endpoint-usecase').extension.where(url='endpointUsecasetype').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-usecase-type" });
        spDefinition.Add(new ModelInfo.SearchParamDefinition() { Resource = "Endpoint", Name = "EndpointVerificationStatusSearchParameter", Code = "verification-status", Description = new Markdown(@"Select Endpoints that support the type of services indicated by a specific verification-status"), Type = SearchParamType.Token, Expression = "Endpoint.extension.where(url='http://hl7.org/fhir/us/ndh/StructureDefinition/base-ext-verification-status').value.ofType(CodeableConcept)", Url = "http://hl7.org/fhir/us/ndh/SearchParameter/endpoint-verification-status" });


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