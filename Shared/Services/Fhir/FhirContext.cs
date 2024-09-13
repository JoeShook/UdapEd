#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Hl7.Fhir.Model;

public class FhirContext
{
    public Patient? CurrentPatient { get; set; }
    public RelatedPerson? CurrentRelatedPerson { get; set; }
    public Person? CurrentPerson { get; set; }
}
