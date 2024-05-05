﻿#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using System.Reflection.Metadata;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Udap.Client.Rest;
using UdapEd.Server.Extensions;
using UdapEd.Shared;
using UdapEd.Shared.Model;

namespace UdapEd.Server.Controllers;
[Route("[controller]")]
[EnableRateLimiting(RateLimitExtensions.Policy)]
public class FhirController : ControllerBase
{
    private readonly FhirClientWithUrlProvider _fhirClient;
    private readonly ILogger<RegisterController> _logger;

    public FhirController(FhirClientWithUrlProvider fhirClient, ILogger<RegisterController> logger)
    {
        _fhirClient = fhirClient;
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
}
