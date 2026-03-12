#region (c) 2023 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using System.Net;
using Hl7.Fhir.Model;

namespace UdapEd.Shared.Model;

public class FhirResultModel<T>
{
    public T? Result { get; }
    public OperationOutcome? OperationOutcome { get; }
    public bool UnAuthorized { get; }
    public HttpStatusCode? HttpStatusCode { get; private set; }
    public Version? Version { get; private set; }
    public int? FhirCompressedSize { get; set; }
    public int? FhirDecompressedSize { get; set; }
    public OutgoingRequestInfo? OutgoingRequestInfo { get; set; }

    // Public flags (kept as fields to match existing signature)
    public bool HasResult;
    public bool HasOperationOutcome;
    public bool IsPureOutcomeOnly;
    public bool IsSuccessWithWarnings;

    // 1) Result only
    public FhirResultModel(T? result)
    {
        Result = result;
        ComputeFlags();
    }

    // 2) Result + Http status + version
    public FhirResultModel(T result, HttpStatusCode httpStatusCode, Version version)
    {
        Result = result;
        HttpStatusCode = httpStatusCode;
        Version = version;
        ComputeFlags();
    }

    // 3) Result + OperationOutcome + Http status + version
    public FhirResultModel(T result, OperationOutcome? operationOutcome, HttpStatusCode httpStatusCode, Version version)
    {
        Result = result;
        OperationOutcome = operationOutcome;
        HttpStatusCode = httpStatusCode;
        Version = version;
        ComputeFlags();
    }

    // 4) OperationOutcome only
    public FhirResultModel(OperationOutcome? operationOutcome)
    {
        OperationOutcome = operationOutcome;
        ComputeFlags();
    }

    // 5) OperationOutcome + Http status + version
    public FhirResultModel(OperationOutcome? operationOutcome, HttpStatusCode httpStatusCode, Version version)
    {
        OperationOutcome = operationOutcome;
        HttpStatusCode = httpStatusCode;
        Version = version;
        ComputeFlags();
    }

    // 6) Unauthorized flag
    public FhirResultModel(bool unAuthorized, string? unauthorizedMessage = null)
    {
        UnAuthorized = unAuthorized;
        UnauthorizedMessage = unauthorizedMessage;
        ComputeFlags();
    }

    public string? UnauthorizedMessage { get; }

    // 7) NEW overload: Result + OperationOutcome (no HTTP metadata available in MAUI code paths)
    public FhirResultModel(T result, OperationOutcome? operationOutcome)
    {
        Result = result;
        OperationOutcome = operationOutcome;
        ComputeFlags();
    }

    public void SetHttpStatus(HttpStatusCode statusCode, Version version)
    {
        HttpStatusCode = statusCode;
        Version = version;
    }

    private void ComputeFlags()
    {
        HasResult = Result is not null;
        HasOperationOutcome = OperationOutcome is not null;
        IsPureOutcomeOnly = !HasResult && HasOperationOutcome;
        IsSuccessWithWarnings = HasResult && HasOperationOutcome;
    }
}
