using Udap.Model.Registration;
using UdapEd.Shared.Model;

namespace UdapEd.Shared.Services;

public interface ICertificationService
{
    Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName);

    Task UploadCertificate(string certBytes);

    Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatement(
        UdapCertificationAndEndorsementDocument request,
        string signingAlgorithm);

    Task<string?> BuildRequestBody(
        RawSoftwareStatementAndHeader? request,
        string signingAlgorithm);

    Task<CertificateStatusViewModel?> ValidateCertificate(string password);
    Task<CertificateStatusViewModel?> ClientCertificateLoadStatus();
    
}