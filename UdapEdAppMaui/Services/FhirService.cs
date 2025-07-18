﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using UdapEd.Shared;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;
using CodeSystem = Hl7.Fhir.Model.CodeSystem;
using Task = System.Threading.Tasks.Task;

namespace UdapEdAppMaui.Services;
internal class FhirService : IFhirService
{
    private readonly FhirClientWithUrlProvider _fhirClient;
    private readonly FhirClient _fhirTerminologyClient;
    private readonly ILogger<FhirService> _logger;


    public FhirService(FhirClientWithUrlProvider fhirClient, FhirClient fhirTerminologyClient, ILogger<FhirService> logger)
    {
        _fhirClient = fhirClient;
        _fhirTerminologyClient = fhirTerminologyClient;
        _logger = logger;
    }

    public async Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchPatient(PatientSearchModel model, CancellationToken ct)
    {
        FhirResultModel<Bundle> resultModel;

        try
        {
            _fhirClient.Settings.PreferredFormat = ResourceFormat.Json;

            if (model.LaunchContext != null && !model.LaunchContext.Patient.IsNullOrEmpty())
            {
                _fhirClient.RequestHeaders?.Add("X-Authorization-Patient", model.LaunchContext.Patient);
            }

            if (model.GetResource)
            {
                var patient = await _fhirClient.ReadAsync<Patient>($"Patient/{model.Id}");
                var singlePatientBundle = new Bundle();

                var bundleEntry = new Bundle.EntryComponent
                {
                    Resource = patient
                };

                singlePatientBundle.Entry.Add(bundleEntry);

                resultModel = new FhirResultModel<Bundle>(singlePatientBundle);
                await EnrichResult(resultModel);
                return resultModel;
            }

            

            Hl7.Fhir.Model.Bundle? bundle;

            if (model.Bundle.IsNullOrEmpty())
            {
                bundle = await _fhirClient.SearchAsync<Patient>(SearchParamsExtensions.OrderBy(BuildSearchParams(model), "given"));
                resultModel = new FhirResultModel<Bundle>(bundle);
                await EnrichResult(resultModel);
                return resultModel;
            }

            var links = new FhirJsonParser().Parse<Bundle>(model.Bundle);
            bundle = await _fhirClient.ContinueAsync(links, model.PageDirection);
            
            var operationOutcome = bundle?.Entry.Select(e => e.Resource as OperationOutcome).ToList();

            if (operationOutcome != null && operationOutcome.Any(o => o != null))
            {
                resultModel = new FhirResultModel<Bundle>(operationOutcome.First());
                await EnrichResult(resultModel);
                return resultModel;
            }

            if (bundle == null)
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = "Missing Bundle Resource"
                        }
                    ]
                };

                resultModel = new FhirResultModel<Bundle>(operationOutCome);
                await EnrichResult(resultModel);
                return resultModel;
            }

            resultModel = new FhirResultModel<Bundle>(bundle);
            await EnrichResult(resultModel);
            return resultModel;
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                resultModel = new FhirResultModel<Bundle>(true);
                await EnrichResult(resultModel);
                return resultModel;
            }

            if (ex.Outcome != null)
            {
                resultModel = new FhirResultModel<Bundle>(ex.Outcome);
                await EnrichResult(resultModel);
                return resultModel;
            }
            else
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = "Resource Server Error: " + ex.Message
                        }
                    ]
                };

                resultModel = new FhirResultModel<Bundle>(operationOutCome);
                await EnrichResult(resultModel);
                return resultModel;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            // hacky but mTLS is tougher to error handle
            if (ex.InnerException != null && ex.InnerException.Message.Contains("The decryption operation failed"))
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = "esource Server Error: Cannot create a successful mTLS connection.  Check logs."
                        }
                    ]
                };

                resultModel = new FhirResultModel<Bundle>(operationOutCome);
                await EnrichResult(resultModel);
                return resultModel;
            }

            throw;
        }
    }

    private async Task EnrichResult(FhirResultModel<Bundle> resultModel)
    {
        int? compressedSize = null;
        int? decompressedSize = null;

        var headerValue = await SecureStorage.Default.GetAsync(UdapEdConstants.FhirClient.FhirCompressedSize); 
        if (int.TryParse(headerValue, out var size))
        {
            compressedSize = size;
        }

        headerValue = await SecureStorage.Default.GetAsync(UdapEdConstants.FhirClient.FhirDecompressedSize);
        if (int.TryParse(headerValue, out size))
        {
            decompressedSize = size;
        }

        resultModel.FhirCompressedSize = compressedSize;
        resultModel.FhirDecompressedSize = decompressedSize;
    }

    public async Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> MatchPatient(string operation, string parametersJson)
    {
        try
        {
            _fhirClient.Settings.PreferredFormat = ResourceFormat.Json;
            var parametersResource = await new FhirJsonParser().ParseAsync<Parameters>(parametersJson);
            var resource = await _fhirClient.TypeOperationAsync<Patient>(operation, parametersResource);

            if (resource is Bundle bundle)
            {
                return new FhirResultModel<Bundle>(bundle);
            }
            else if (resource is Parameters parameters)
            {
                // Try to extract a Bundle from Parameters, if present
                var bundlePart = parameters.Parameter?.FirstOrDefault(p => p.Resource is Bundle)?.Resource as Bundle;
                if (bundlePart != null)
                {
                    return new FhirResultModel<Bundle>(bundlePart);
                }
                // Optionally, handle OperationOutcome in Parameters
                var outcome = parameters.Parameter?.FirstOrDefault(p => p.Resource is OperationOutcome)?.Resource as OperationOutcome;
                if (outcome != null)
                {
                    return new FhirResultModel<Bundle>(outcome);
                }
                // If neither, return error
                var operationOutCome = new OperationOutcome()
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
                return new FhirResultModel<Bundle>(operationOutCome);
            }
            else if (resource is OperationOutcome opOutcome)
            {
                return new FhirResultModel<Bundle>(opOutcome);
            }
            else
            {
                var operationOutCome = new OperationOutcome()
                {
                    ResourceBase = null,
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = $"Unexpected resource type: {resource?.TypeName ?? "null"}"
                        }
                    ]
                };
                return new FhirResultModel<Bundle>(operationOutCome);
            }
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<Bundle>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<Bundle>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<Bundle>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<FhirResultModel<CodeSystem>> GetCodeSystem(string location)
    {
        try
        {
            _fhirTerminologyClient.Settings.PreferredFormat = ResourceFormat.Json;
            var codeSystem = await _fhirTerminologyClient.ReadAsync<CodeSystem>(location);
            
            return new FhirResultModel<CodeSystem>(codeSystem);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<CodeSystem>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<CodeSystem>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<CodeSystem>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<FhirResultModel<ValueSet>> GetValueSet(string location)
    {
        try
        {
            // Get the assembly containing the resource
            var assembly = Assembly.GetExecutingAssembly();

            // Get the resource stream
            var resourceName = "UdapEdAppMaui.hl7.fhir.us.identity-matching-2.0.0-draft-expanded.json";
            await using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new FileNotFoundException("Resource not found", resourceName);
            }

            // Read the stream
            using var reader = new StreamReader(stream);
            var valueSetJson = await reader.ReadToEndAsync();
            var valueSet = new FhirJsonParser().Parse<ValueSet>(valueSetJson);

            return new FhirResultModel<ValueSet>(valueSet);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<ValueSet>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<ValueSet>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<ValueSet>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchGet(string queryParameters)
    {
        try
        {
            //Todo maybe inject in the future, so we don't exhaust underlying HttpClient
            var fhirClient = new FhirClient(queryParameters, new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });
            var bundle = await fhirClient.GetAsync(queryParameters) as Bundle;

            var operationOutcome = bundle?.Entry.Select(e => e.Resource as OperationOutcome).ToList();

            if (operationOutcome != null && operationOutcome.Any(o => o != null))
            {
                return new FhirResultModel<Bundle>(operationOutcome.First());
            }

            return new FhirResultModel<Bundle>(bundle);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<Bundle>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<Bundle>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<Bundle>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<FhirResultModel<Bundle>> SearchPost(SearchForm searchForm)
    {
        try
        {
            //Todo maybe inject in the future, so we don't exhaust underlying HttpClient
            var fhirClient = new FhirClient(searchForm.Url, new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });
            var searchParams = new SearchParams();

            foreach (var pair in searchForm.FormUrlEncoded)
            {
                var item = pair.Split('=');
                searchParams.Add(item[0], item[1]);
            }

            var bundle = await fhirClient.SearchUsingPostAsync(searchParams, searchForm.Resource);
            var operationOutcome = bundle?.Entry.Select(e => e.Resource as OperationOutcome).ToList();

            if (operationOutcome != null && operationOutcome.Any(o => o != null))
            {
                return new FhirResultModel<Bundle>(operationOutcome.First());
            }

            return new FhirResultModel<Bundle>(bundle);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<Bundle>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<Bundle>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<Bundle>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    public async Task<FhirResultModel<Hl7.Fhir.Model.Resource>> Get(string resourcePath)
    {
        try
        {
            var fhirClient = new FhirClient(resourcePath, new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });
            var resource = await fhirClient.GetAsync(resourcePath);
            
            if (resource is OperationOutcome operationOutcome)
            {
                return new FhirResultModel<Hl7.Fhir.Model.Resource>(operationOutcome);
            }

            return new FhirResultModel<Hl7.Fhir.Model.Resource>(resource);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return new FhirResultModel<Hl7.Fhir.Model.Resource>(true);
            }

            if (ex.Outcome != null)
            {
                return new FhirResultModel<Hl7.Fhir.Model.Resource>(ex.Outcome);
            }

            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Resource Server Error:"
                    }
                ]
            };

            return new FhirResultModel<Hl7.Fhir.Model.Resource>(operationOutCome);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    private static SearchParams BuildSearchParams(PatientSearchModel model)
    {
        var searchParams = new SearchParams();

        if (!string.IsNullOrEmpty(model.Id))
        {
            searchParams.Add("_id", model.Id);
        }

        if (!string.IsNullOrEmpty(model.Identifier))
        {
            searchParams.Add("identifier", model.Identifier);
        }

        if (!string.IsNullOrEmpty(model.Family))
        {
            searchParams.Add("family", model.Family);
        }

        if (!string.IsNullOrEmpty(model.Given))
        {
            searchParams.Add("given", model.Given);
        }

        if (!string.IsNullOrEmpty(model.Name))
        {
            searchParams.Add("name", model.Name);
        }

        if (model.BirthDate.HasValue)
        {
            searchParams.Add("birthdate", model.BirthDate.Value.ToString("yyyy-MM-dd"));
        }

        searchParams.Add("_count", model.RowsPerPage.ToString());
        return searchParams;
    }

    private static async Task<FhirResultModel<Bundle>> HandleResponseError(HttpResponseMessage? response)
    {
        if (response == null)
        {
            var operationOutCome = new OperationOutcome()
            {
                ResourceBase = null,
                Issue =
                [
                    new OperationOutcome.IssueComponent
                    {
                        Diagnostics = "Http response was null"
                    }
                ]
            };

            return new FhirResultModel<Bundle>(operationOutCome, HttpStatusCode.MethodNotAllowed, new Version(1, 1));
        }

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
            var operationOutcome = FhirResourceUtility.ExtractOperationOutcome(result);

            return new FhirResultModel<Bundle>(operationOutcome, response.StatusCode, response.Version);
        }
    }

}