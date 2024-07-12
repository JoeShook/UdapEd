﻿namespace UdapEd.Shared;

public static class UdapEdConstants
{
    public const string UDAP_CLIENT_CERTIFICATE_WITH_KEY = "udap_clientCertificateWithKey";
    public const string UDAP_CLIENT_CERTIFICATE = "udap_clientCertificate";
    public const string UDAP_ANCHOR_CERTIFICATE = "udap_anchorCertificate";
    public const string CLIENT_NAME = "FhirLabs UdapEd";

    public const string TOKEN = "Token";
    public const string BASE_URL = "BaseUrl";
    public const string CLIENT_HEADERS = "ClientHeaders";

    public const string MTLS_CLIENT_CERTIFICATE = "mtls_ClientCertificate";
    public const string MTLS_CLIENT_CERTIFICATE_WITH_KEY = "mtls_ClientCertificateWithKey";
    public const string MTLS_ANCHOR_CERTIFICATE = "mtls_anchorCertificate";

    /// <summary>
    /// See <a href="https://www.hl7.org/fhir/patient-operation-match.html">Patient-match</a>
    /// Canonical URL:: https://www.hl7.org/fhir/patient-operation-match.html
    ///
    /// The following are the defined In Parameter names from the Patient-match operation
    /// </summary>
    public static class PatientMatch
    {

        public static class InParameterNames
        {
            /// <summary>
            /// Note: One and only one resource where the name of the Parameter is "resource"
            /// </summary>
            public const string RESOURCE = "resource";

            public const string ONLY_CERTAIN_MATCHES = "onlyCertainMatches";

            public const string COUNT = "count";
        }

        public static class OutParameterNames
        {
            public const string SEARCH = "search";
        }
    }
}