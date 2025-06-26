#region (c) 2023 Joseph Shook. All rights reserved.
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using UdapEd.Server.Extensions;
using UdapEd.Server.Services.Fhir;
using UdapEd.Shared.Extensions;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using UdapEd.Shared.Services.Fhir;
using FhirClientWithUrlProvider = UdapEd.Shared.Services.FhirClientWithUrlProvider;

namespace UdapEd.Server.Controllers;


[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class FhirController : FhirBaseController<FhirController>
{
   public FhirController(
        FhirClientWithUrlProvider fhirClient, 
        FhirClient fhirTerminologyClient, 
        ILogger<FhirController> logger)
        : base(fhirClient, fhirTerminologyClient, logger)
    { }
}


[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class FhirMtlsController : FhirBaseController<FhirMtlsController>
{
    public FhirMtlsController(
        FhirMTlsClientWithUrlProvider fhirClient, 
        FhirClient fhirTerminologyClient,
        ILogger<FhirMtlsController> logger)
        : base(fhirClient, fhirTerminologyClient, logger)
    { }
}

public class FhirBaseController<T> : ControllerBase
{
    private readonly FhirClient _fhirClient;
    private readonly FhirClient _fhirTerminologyClient;
    private readonly ILogger<T> _logger;

    public FhirBaseController(
        FhirClient fhirClient, 
        FhirClient fhirTerminologyClient,
        ILogger<T> logger)
    {
        _fhirClient = fhirClient;
        _fhirTerminologyClient = fhirTerminologyClient;
        _logger = logger;
    }

    [HttpPost("SearchForPatient")]
    public async Task<IActionResult> SearchForPatient([FromBody] PatientSearchModel model)
    {
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
                var patientJson = await new FhirJsonSerializer().SerializeToStringAsync(patient);
                CopyCustomHeadersToResponse();
                return Ok(patientJson);
            }
            // new bundle
            if (model.Bundle.IsNullOrEmpty())
            {
                var bundle = await _fhirClient.SearchAsync<Patient>(SearchParamsExtensions.OrderBy(BuildSearchParams(model), "given"));
                var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);
                CopyCustomHeadersToResponse();
                return Ok(bundleJson);
            }
            // existing bundle... paging
            var links = new FhirJsonParser().Parse<Bundle>(model.Bundle);
            var nextBundle = await _fhirClient.ContinueAsync(links, model.PageDirection);
            CopyCustomHeadersToResponse();
            return Ok(await new FhirJsonSerializer().SerializeToStringAsync(nextBundle));
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            // hacky but mTLS is tougher to error handle
            if (ex.InnerException != null && ex.InnerException.Message.Contains("The decryption operation failed"))
            {
                return NotFound("Resource Server Error: Cannot create a successful mTLS connection.  Check logs.");
            }

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

    [HttpPost("MatchPatient/{operation}")]
    public async Task<IActionResult> MatchPatient([FromRoute] string operation, [FromBody] string parametersJson)
    {
        try
        {
            _fhirClient.Settings.PreferredFormat = ResourceFormat.Json;
            var parametersResource = await new FhirJsonParser().ParseAsync<Parameters>(parametersJson);
            var bundle = await _fhirClient.TypeOperationAsync<Patient>(operation, parametersResource);
            var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);
            CopyCustomHeadersToResponse();
            return Ok(bundleJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                // Wrap OperationOutcome in a Bundle
                var bundle = WrapOperationOutcomeInBundle(ex.Outcome);
                var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);
                return NotFound(bundleJson);
            }
            else
            {
                // Also wrap generic error in a Bundle
                var outcome = new OperationOutcome
                {
                    Issue =
                    [
                        new OperationOutcome.IssueComponent
                        {
                            Diagnostics = "Resource Server Error: " + ex.Message
                        }
                    ]
                };
                var bundle = WrapOperationOutcomeInBundle(outcome);
                var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);
                return NotFound(bundleJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    private static Bundle WrapOperationOutcomeInBundle(OperationOutcome outcome)
    {
        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Entry =
            [
                new Bundle.EntryComponent
                {
                    Resource = outcome
                }
            ]
        };
        return bundle;
    }

    /// <summary>
    /// Get code system.  Currently, needs a terminology server configured in DI
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    [HttpGet("CodeSystem")]
    public async Task<IActionResult> GetCodeSystem([FromQuery] string location)
    {
        try
        {
            _fhirTerminologyClient.Settings.PreferredFormat = ResourceFormat.Json;
            var codeSystem = await _fhirTerminologyClient.ReadAsync<Hl7.Fhir.Model.CodeSystem>(location);
            var codeSystemJson = await new FhirJsonSerializer().SerializeToStringAsync(codeSystem);

            return Ok(codeSystemJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    [HttpGet("ValueSet")]
    public async Task<IActionResult> GetValueSet([FromQuery] string location)
    {
        try
        {
            //
            // This technique only works if the hl7.fhir.r4.core@4.0.1 package has been downloaded and
            // extracted
            //

            // var resolver = new FhirPackageSource(ModelInfo.ModelInspector, @"hl7.fhir.us.identity-matching-2.0.0-draft.tgz");
            // var termService = new LocalTerminologyService(resolver);
            // var p = new ExpandParameters().WithValueSet(url: location);
            // var valueSet = await termService.Expand(p) as ValueSet;
            // var valueSetJson = await new FhirJsonSerializer().SerializeToStringAsync(valueSet);

            var valueSetJson = await System.IO.File.ReadAllTextAsync("hl7.fhir.us.identity-matching-2.0.0-draft-expanded.json");

            return Ok(valueSetJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    [HttpPost("GetSearch")]
    public async Task<IActionResult> GetSearch([FromBody] string fullSearch)
    {
        try
        {
            //Todo maybe inject in the future, so we don't exhaust underlying HttpClient
            var fhirClient = new FhirClient(fullSearch, new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });
            Console.WriteLine(fullSearch);
            var bundle = await fhirClient.GetAsync(fullSearch);
            var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);

            return Ok(bundleJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    [HttpPost("PostSearch")]
    public async Task<IActionResult> PostSearch([FromBody] SearchForm searchForm)
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
            var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);

            return Ok(bundleJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    [HttpPost("Get")]
    public async Task<IActionResult> Get([FromBody] string resourcePath)
    {
        try
        {
            var fhirClient = new FhirClient(resourcePath, new FhirClientSettings() { PreferredFormat = ResourceFormat.Json });
            Console.WriteLine(resourcePath);
            var resource = await fhirClient.GetAsync(resourcePath);
            var resourceJson = await new FhirJsonSerializer().SerializeToStringAsync(resource);

            return Ok(resourceJson);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogWarning(ex.Message);

            if (ex.Status == HttpStatusCode.Unauthorized)
            {
                return Unauthorized();
            }

            if (ex.Outcome != null)
            {
                var outcomeJson = await new FhirJsonSerializer().SerializeToStringAsync(ex.Outcome);
                return NotFound(outcomeJson);
            }
            else
            {
                return NotFound("Resource Server Error: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }

    private void CopyCustomHeadersToResponse()
    {
        foreach (var kvp in HttpContext.Items.Where(i => i.Key is string key && key.StartsWith("X-")))
        {
            Response.Headers[(string)kvp.Key] = kvp.Value?.ToString();
        }
    }
}

