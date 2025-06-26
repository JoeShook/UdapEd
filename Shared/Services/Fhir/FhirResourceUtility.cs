using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace UdapEd.Shared.Services.Fhir;

public static class FhirResourceUtility
{
    /// <summary>
    /// Extracts an OperationOutcome from a FHIR JSON string, whether it's a direct OperationOutcome or wrapped in a Bundle.
    /// </summary>
    public static OperationOutcome? ExtractOperationOutcome(string json)
    {
        var parser = new FhirJsonParser();
        try
        {
            // Try direct OperationOutcome
            var outcome = parser.Parse<OperationOutcome>(json);
            if (outcome != null)
                return outcome;
        }
        catch { /* Not a direct OperationOutcome */ }
        try
        {
            // Try as Bundle
            var bundle = parser.Parse<Bundle>(json);
            var oo = bundle.Entry?.Select(e => e.Resource as OperationOutcome).FirstOrDefault(o => o != null);
            return oo;
        }
        catch { /* Not a Bundle or no OperationOutcome inside */ }
        return null;
    }
}