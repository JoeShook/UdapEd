#region (c) 2024 Joseph Shook. All rights reserved.

// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */

#endregion

using UdapEd.Shared.Model;

namespace UdapEd.Shared.Services;

public interface IFhirService
{
    Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchPatient(PatientSearchModel model, CancellationToken ct);
    Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> MatchPatient(string operation, string parametersJson);

    Task<FhirResultModel<Hl7.Fhir.Model.CodeSystem>> GetCodeSystem(string location);

    Task<FhirResultModel<Hl7.Fhir.Model.ValueSet>> GetValueSet(string location);

    Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchGet(string queryParameters);

    Task<FhirResultModel<Hl7.Fhir.Model.Bundle>> SearchPost(SearchForm searchForm);

    Task<FhirResultModel<Hl7.Fhir.Model.Resource>> Get(string resourcePath);
}