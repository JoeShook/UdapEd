﻿#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;
using CodeSystem = Hl7.Fhir.Model.CodeSystem;

namespace UdapEdAppMaui.Services;
internal class FhirService : IFhirService
{
    readonly HttpClient _httpClient;

    public FhirService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchPatient(PatientSearchModel model)
    {
        throw new NotImplementedException();
    }

    public Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> MatchPatient(string parametersJson)
    {
        throw new NotImplementedException();
    }

    public Task<FhirResultModel<CodeSystem>> GetCodeSystem(string location)
    {
        throw new NotImplementedException();
    }

    public Task<FhirResultModel<ValueSet>> GetValueSet(string location)
    {
        throw new NotImplementedException();
    }

    public Task<FhirResultModel<Bundle>> SearchGet(string queryParameters)
    {
        throw new NotImplementedException();
    }

    public Task<FhirResultModel<Bundle>> SearchPost(SearchForm searchForm)
    {
        throw new NotImplementedException();
    }
}