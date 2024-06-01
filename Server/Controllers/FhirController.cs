#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using Firely.Fhir.Packages;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Udap.Client.Rest;
using UdapEd.Server.Extensions;
using UdapEd.Shared.Model;
using FhirClientWithUrlProvider = UdapEd.Server.Services.FhirClientWithUrlProvider;

namespace UdapEd.Server.Controllers;
[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class FhirController : ControllerBase
{
    private readonly FhirClientWithUrlProvider _fhirClient;
    private readonly FhirClient _fhirTerminologyClient;
    private readonly ILogger<RegisterController> _logger;

    public FhirController(FhirClientWithUrlProvider fhirClient, FhirClient fhirTerminologyClient, ILogger<RegisterController> logger)
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
                return Ok(patientJson);
            }

            if (model.Bundle.IsNullOrEmpty())
            {
                
                var bundle = await _fhirClient.SearchAsync<Patient>(BuildSearchParams(model).OrderBy("given"));
                var bundleJson = await new FhirJsonSerializer().SerializeToStringAsync(bundle);
                return Ok(bundleJson);
            }
            var links = new FhirJsonParser().Parse<Bundle>(model.Bundle);
            var nextBundle = await _fhirClient.ContinueAsync(links, model.PageDirection);
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

    [HttpPost("MatchPatient")]
    public async Task<IActionResult> MatchPatient([FromBody] string parametersJson)
    {
        try
        {
            _fhirClient.Settings.PreferredFormat = ResourceFormat.Json;
            var parametersResource = await new FhirJsonParser().ParseAsync<Parameters>(parametersJson);
            var bundle = await _fhirClient.TypeOperationAsync<Patient>("match", parametersResource);
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

            if(ex.Outcome != null)
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
        Console.WriteLine("Hello Joe");
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
}
