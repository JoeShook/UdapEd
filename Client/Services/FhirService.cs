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

            // Allow bundle + OperationOutcome to coexist (warnings, partial success, etc.)
            var operationOutcome = bundle.Entry
                .Select(e => e.Resource as OperationOutcome)
                .FirstOrDefault(o => o != null);

            if (operationOutcome != null)
            {
                resultModel = new FhirResultModel<Bundle>(bundle, operationOutcome, response.StatusCode, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
        }

        return await HandleResponseError(response);
    }

    public async Task<FhirResultModel<Bundle>> MatchPatient(string operation, string parametersJson)
    {
        FhirResultModel<Bundle> resultModel;
        var inParameters = await new FhirJsonParser().ParseAsync<Parameters>(parametersJson);
        var json = await new FhirJsonSerializer().SerializeToStringAsync(inParameters); // removing line feeds
        var jsonMessage = JsonSerializer.Serialize(json); // needs to be json
        var content = new StringContent(jsonMessage, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        var controller = await GetControllerPath();
        var response = await _httpClient.PostAsync($"{controller}/MatchPatient/{operation}", content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var resource = new FhirJsonParser().Parse<Resource>(result);

            switch (resource)
            {
                case Bundle bundle:
                {
                    var oo = bundle.Entry
                        .Select(e => e.Resource as OperationOutcome)
                        .FirstOrDefault(o => o != null);

                    if (oo != null)
                    {
                        resultModel = new FhirResultModel<Bundle>(bundle, oo, response.StatusCode, response.Version);
                        EnrichResult(response, resultModel);
                        return resultModel;
                    }

                    resultModel = new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
                    EnrichResult(response, resultModel);
                    return resultModel;
                }
                case Parameters outParameters:
                {
                    var bundlePart = outParameters.Parameter?
                        .FirstOrDefault(p => p.Resource is Bundle)?
                        .Resource as Bundle;

                    var outcome = outParameters.Parameter?
                        .FirstOrDefault(p => p.Resource is OperationOutcome)?
                        .Resource as OperationOutcome;

                    if (bundlePart != null && outcome != null)
                    {
                        return new FhirResultModel<Bundle>(bundlePart, outcome, response.StatusCode, response.Version);
                    }

                    if (bundlePart != null)
                    {
                        return new FhirResultModel<Bundle>(bundlePart, response.StatusCode, response.Version);
                    }

                    if (outcome != null)
                    {
                        return new FhirResultModel<Bundle>(outcome);
                    }

                    var opOutcome = new OperationOutcome()
                    {
                        ResourceBase = null,
                        Issue =
                        [
                            new OperationOutcome.IssueComponent
                            {
                                Diagnostics = "Parameters resource did not contain a Bundle or OperationOutcome."
                            }
                        ]
                    };
                    return new FhirResultModel<Bundle>(opOutcome);
                }
                case OperationOutcome operationOutcome:
                    return new FhirResultModel<Bundle>(operationOutcome);
            }
        }

        return await HandleResponseError(response);
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
            var unauthorizedDetail = await response.Content.ReadAsStringAsync();
            return new FhirResultModel<CodeSystem>(true, unauthorizedDetail);
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
            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);
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
            var unauthorizedDetail = await response.Content.ReadAsStringAsync();
            return new FhirResultModel<ValueSet>(true, unauthorizedDetail);
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
            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);
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

    private async Task<FhirResultModel<Resource>> HandleGetResponseError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var unauthorizedDetail = await response.Content.ReadAsStringAsync();
            return new FhirResultModel<Resource>(true, unauthorizedDetail);
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

            // General 500 fallback: try to extract OperationOutcome, otherwise use raw body
            var operationOutcome500 = FhirResourceUtility.ExtractOperationOutcome(result);
            if (operationOutcome500 != null)
            {
                return new FhirResultModel<Resource>(operationOutcome500, HttpStatusCode.InternalServerError, response.Version);
            }

            return new FhirResultModel<Resource>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                HttpStatusCode.InternalServerError, response.Version);
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

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();

            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);
            if (operationOutcome != null)
            {
                return new FhirResultModel<Resource>(operationOutcome, HttpStatusCode.NotFound, response.Version);
            }

            return new FhirResultModel<Resource>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                HttpStatusCode.NotFound, response.Version);
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);

            if (operationOutcome != null)
            {
                return new FhirResultModel<Resource>(operationOutcome, response.StatusCode, response.Version);
            }

            return new FhirResultModel<Resource>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                response.StatusCode, response.Version);
        }
    }

    private async Task<FhirResultModel<Bundle>> SearchHandler(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var bundle = new FhirJsonParser().Parse<Bundle>(result);
            var operationOutcome = bundle.Entry
                .Select(e => e.Resource as OperationOutcome)
                .FirstOrDefault(o => o != null);

            if (operationOutcome != null)
            {
                return new FhirResultModel<Bundle>(bundle, operationOutcome, response.StatusCode, response.Version);
            }

            return new FhirResultModel<Bundle>(bundle, response.StatusCode, response.Version);
        }

        return await HandleResponseError(response);
    }

    private async Task<FhirResultModel<Bundle>> HandleResponseError(HttpResponseMessage response)
    {
        FhirResultModel<Bundle> resultModel;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var unauthorizedDetail = await response.Content.ReadAsStringAsync();
            resultModel = new FhirResultModel<Bundle>(true, unauthorizedDetail);
            EnrichResult(response, resultModel);
            return resultModel;
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

                resultModel = new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.PreconditionFailed, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
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

                resultModel = new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.Unauthorized, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            // General 500 fallback: try to extract OperationOutcome, otherwise use raw body
            var operationOutcome500 = FhirResourceUtility.ExtractOperationOutcome(result);
            if (operationOutcome500 != null)
            {
                resultModel = new FhirResultModel<Bundle>(operationOutcome500, HttpStatusCode.InternalServerError, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                HttpStatusCode.InternalServerError, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
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

            resultModel = new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.MethodNotAllowed, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
        }

        //todo constant :: and this whole routine is ugly.  Should move logic upstream to controller
        //This code exists from testing various FHIR servers like MEDITECH.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var result = await response.Content.ReadAsStringAsync();

            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);
            if (operationOutcome != null)
            {
                resultModel = new FhirResultModel<Bundle>(operationOutcome, HttpStatusCode.NotFound, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                HttpStatusCode.NotFound, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
        }

        {
            var result = await response.Content.ReadAsStringAsync();
            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);

            if (operationOutcome != null)
            {
                resultModel = new FhirResultModel<Bundle>(operationOutcome, response.StatusCode, response.Version);
                EnrichResult(response, resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(
                new OperationOutcome
                {
                    Issue = [new OperationOutcome.IssueComponent { Diagnostics = result }]
                },
                response.StatusCode, response.Version);
            EnrichResult(response, resultModel);
            return resultModel;
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

        await EnsureAccessTokenSynced(appState);

        var controller = appState?.ClientMode == ClientSecureMode.mTLS ? "FhirMtls" : "Fhir";

        return controller;
    }

    /// <summary>
    /// Re-syncs the access token from client-side LocalStorage to the server-side session.
    /// This handles the case where the ASP.NET session expires but the client still has a valid token.
    /// </summary>
    private async System.Threading.Tasks.Task EnsureAccessTokenSynced(UdapClientState? appState)
    {
        var accessToken = appState?.AccessTokens?.AccessToken;
        if (!string.IsNullOrEmpty(accessToken))
        {
            await _httpClient.PutAsJsonAsync("Metadata/Token", accessToken);
        }
    }

    private void EnrichResult<T>(HttpResponseMessage responseMessage, FhirResultModel<T> resultModel)
    {
        if (responseMessage.Headers.TryGetValues(UdapEdConstants.FhirClient.FhirCompressedSize, out var compressedValues))
        {
            if (int.TryParse(compressedValues.FirstOrDefault(), out var compressedSize))
            {
                resultModel.FhirCompressedSize = compressedSize;
            }
        }

        if (responseMessage.Headers.TryGetValues(UdapEdConstants.FhirClient.FhirDecompressedSize, out var decompressedValues))
        {
            if (int.TryParse(decompressedValues.FirstOrDefault(), out var decompressedSize))
            {
                resultModel.FhirDecompressedSize = decompressedSize;
            }
        }

        if (responseMessage.Headers.TryGetValues(UdapEdConstants.FhirClient.FhirOutgoingRequest, out var reqValues))
        {
            var base64 = reqValues.FirstOrDefault();
            if (base64 != null)
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                resultModel.OutgoingRequestInfo = JsonSerializer.Deserialize<OutgoingRequestInfo>(json);
            }
        }
    }
}