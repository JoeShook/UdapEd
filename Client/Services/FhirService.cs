#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Blazored.LocalStorage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.CdsHooks;
using Hl7.Fhir.Serialization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using UdapEd.Shared;
using UdapEd.Shared.Components;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;
using CodeSystem = Hl7.Fhir.Model.CodeSystem;

namespace UdapEd.Client.Services;

public class FhirService : IFhirService
{
    readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorageService;

    public FhirService(HttpClient httpClient, ILocalStorageService localStorageService)
    {
        _httpClient = httpClient;
        _localStorageService = localStorageService;
    }
    
    public async Task<FhirResultModel<Bundle>> SearchPatient(PatientSearchModel model, CancellationToken ct)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsJsonAsync($"{controller}/SearchForPatient", model, cancellationToken: ct);

        if (response.IsSuccessStatusCode)
        {
            FhirResultModel<Bundle> resultModel;

            var result = await response.Content.ReadAsStringAsync(ct);
            if (model.GetResource)
            {
                var patient = new FhirJsonParser().Parse<Patient>(result);
                var singlePatientBundle = new Bundle();
                
                var bundleEntry = new Bundle.EntryComponent
                {
                    Resource = patient
                };

                singlePatientBundle.Entry.Add(bundleEntry);

                resultModel = new FhirResultModel<Bundle>(singlePatientBundle, response.StatusCode, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            var bundle = new FhirJsonParser().Parse<Bundle>(result);
            var operationOutcome = bundle.Entry.Select(e => e.Resource as OperationOutcome).ToList();
            
            if (operationOutcome.Any(o => o != null))
            {
                resultModel = new FhirResultModel<Bundle>(operationOutcome.First(), response.StatusCode, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
        }

        return await HandleResponseError(response);
    }

    private void EnrichResult(HttpResponseMessage responseMessage, FhirResultModel<Bundle> resultModel)
    {
        int? compressedSize = null;
        int? decompressedSize = null;

        var headerValue = responseMessage.Headers.GetValues(UdapEdConstants.FhirClient.FhirCompressedSize).FirstOrDefault();
        if (int.TryParse(headerValue, out var size))
        {
            compressedSize = size;
        }

        headerValue = responseMessage.Headers.GetValues(UdapEdConstants.FhirClient.FhirDecompressedSize).FirstOrDefault();
        if (int.TryParse(headerValue, out size))
        {
            decompressedSize = size;
        }

        resultModel.FhirCompressedSize = compressedSize;
        resultModel.FhirDecompressedSize = decompressedSize;
    }

    public async Task<FhirResultModel<Bundle>> MatchPatient(string parametersJson)
    {
        var parameters = await new FhirJsonParser().ParseAsync<Parameters>(parametersJson);
        var json = await new FhirJsonSerializer().SerializeToStringAsync(parameters); // removing line feeds
        var jsonMessage = JsonSerializer.Serialize(json); // needs to be json
        var content = new StringContent(jsonMessage, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsync($"{controller}/MatchPatient", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var bundle = new FhirJsonParser().Parse<Bundle>(result);
            // var patients = bundle.Entry.Select(e => e.Resource as Patient).ToList();

            return new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
        }
        
        Console.WriteLine(response.StatusCode);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FhirResultModel<Bundle>(true);
        }

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains(nameof(UriFormatException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null
                };

                return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
            }
        }

        //todo constant :: and this whole routine is ugly.  Should move logic upstream to controller
        //This code exists from testing various FHIR servers like MEDITECH.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();
            if (result.Contains("Resource Server Error:"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue = new List<OperationOutcome.IssueComponent>
                    {
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    }
                };

                return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.InternalServerError,
                    response.Version);
            }
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = new FhirJsonParser().Parse<OperationOutcome>(result);

            return new FhirResultModel<Bundle>(operationOutcome, response.StatusCode, response.Version);
        }
    }

    public async Task<FhirResultModel<CodeSystem>> GetCodeSystem(string location)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.GetAsync($"{controller}/CodeSystem?location={location}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var codeSystem = new FhirJsonParser().Parse<CodeSystem>(result);

            return new FhirResultModel<CodeSystem>(codeSystem, response.StatusCode, response.Version);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FhirResultModel<CodeSystem>(true);
        }

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains(nameof(UriFormatException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null
                };

                return new FhirResultModel<CodeSystem>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
            }
        }
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();
            if (result.Contains("Resource Server Error:"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<CodeSystem>(operationOutCome, HttpStatusCode.InternalServerError,
                    response.Version);
            }
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = new FhirJsonParser().Parse<OperationOutcome>(result);

            return new FhirResultModel<CodeSystem>(operationOutcome, response.StatusCode, response.Version);
        }
    }

    public async Task<FhirResultModel<ValueSet>> GetValueSet(string location)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.GetAsync($"{controller}/ValueSet?location={location}");

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var codeSystem = new FhirJsonParser().Parse<ValueSet>(result);

            return new FhirResultModel<ValueSet>(codeSystem, response.StatusCode, response.Version);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FhirResultModel<ValueSet>(true);
        }

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains(nameof(UriFormatException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null
                };

                return new FhirResultModel<ValueSet>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
            }
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();
            if (result.Contains("Resource Server Error:"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue = new List<OperationOutcome.IssueComponent>
                    {
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    }
                };

                return new FhirResultModel<ValueSet>(operationOutCome, HttpStatusCode.InternalServerError,
                    response.Version);
            }
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = new FhirJsonParser().Parse<OperationOutcome>(result);

            return new FhirResultModel<ValueSet>(operationOutcome, response.StatusCode, response.Version);
        }
    }

    public async Task<FhirResultModel<Bundle>> SearchGet(string queryParameters)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsJsonAsync($"{controller}/GetSearch", queryParameters);

        return await SearchHandler(response);
    }

    public async Task<FhirResultModel<Bundle>> SearchPost(SearchForm searchForm)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsJsonAsync($"{controller}/PostSearch", searchForm);

        return await SearchHandler(response);
    }

    public async Task<FhirResultModel<Resource>> Get(string resourcePath)
    {
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsJsonAsync($"{controller}/Get", resourcePath);
        return await GetHandler(response);
    }

    private async Task<FhirResultModel<Resource>> GetHandler(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var resource = new FhirJsonParser().Parse<Resource>(result);


            if (resource is OperationOutcome operationOutcome)
            {
                return new FhirResultModel<Hl7.Fhir.Model.Resource>(operationOutcome);
            }

            return new FhirResultModel<Hl7.Fhir.Model.Resource>(resource);
        }

        return await HandleGetResponseError(response);
    }

    private static async Task<FhirResultModel<Resource>> HandleGetResponseError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FhirResultModel<Resource>(true);
        }

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains(nameof(UriFormatException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<Resource>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
            }

            if (result.Contains(nameof(AuthenticationException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<Resource>(operationOutCome, HttpStatusCode.Unauthorized, response.Version);
            }
        }

        if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "UdapEd does not have a endpoint for this request yet"
                    }
                ]
            };

            return new FhirResultModel<Resource>(operationOutCome, HttpStatusCode.MethodNotAllowed, response.Version);
        }

        //todo constant :: and this whole routine is ugly.  Should move logic upstream to controller
        //This code exists from testing various FHIR servers like MEDITECH.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains("Resource Server Error:"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<Resource>(operationOutCome, HttpStatusCode.InternalServerError,
                    response.Version);
            }
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = new FhirJsonParser().Parse<OperationOutcome>(result);

            return new FhirResultModel<Resource>(operationOutcome, response.StatusCode, response.Version);
        }
    }
    private async Task<FhirResultModel<Bundle>> SearchHandler(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var bundle = new FhirJsonParser().Parse<Bundle>(result);
            var operationOutcome = bundle.Entry.Select(e => e.Resource as OperationOutcome).ToList();

            if (operationOutcome.Any(o => o != null))
            {
                return new FhirResultModel<Bundle>(operationOutcome.First(), response.StatusCode, response.Version);
            }

            return new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
        }

        return await HandleResponseError(response);
    }

    private static async Task<FhirResultModel<Bundle>> HandleResponseError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new FhirResultModel<Bundle>(true);
        }

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var result = await response.Content.ReadAsStringAsync();

            if (result.Contains(nameof(UriFormatException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
            }

            if (result.Contains(nameof(AuthenticationException)))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };

                return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.Unauthorized, response.Version);
            }
        }
        
        if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "UdapEd does not have a endpoint for this request yet"
                    }
                ]
            };
            
            return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.MethodNotAllowed, response.Version);
        }

        //todo constant :: and this whole routine is ugly.  Should move logic upstream to controller
        //This code exists from testing various FHIR servers like MEDITECH.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();
            
            if (result.Contains("Resource Server Error:"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = result
                        }
                    ]
                };
                
                return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.InternalServerError,
                    response.Version);
            }
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = new FhirJsonParser().Parse<OperationOutcome>(result);

            return new FhirResultModel<Bundle>(operationOutcome, response.StatusCode, response.Version);
        }
    }

    private async Task<string> GetControllerPath()
    {
        var json = await _localStorageService.GetItemAsStringAsync("udapClientState");
        if (json == null)
        {
            return "Fhir";
        }
        
        var appState = JsonSerializer.Deserialize<UdapClientState>(json,  UdapJsonContext.UdapDefault.UdapClientState);
        var controller = appState?.ClientMode == ClientSecureMode.mTLS ? "FhirMtls" : "Fhir";
        
        return controller;
    }

}