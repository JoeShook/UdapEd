#region (c) 2024 Joseph Shook. All rights reserved.
// /*
//  Authors:
//     Joseph Shook   Joseph.Shook@Surescripts.com
// 
//  See LICENSE in the project root for license information.
// */
#endregion

using Udap.Model.Registration;
using UdapEd.Shared.Model;
using UdapEd.Shared.Services;

namespace UdapEdAppMaui.Services;
public class CertificationService : ICertificationService
{
    public Task UploadCertificate(string certBytes)
    {
        throw new NotImplementedException();
    }

    public Task<RawSoftwareStatementAndHeader?> BuildSoftwareStatement(UdapCertificationAndEndorsementDocument request, string signingAlgorithm)
    {
        throw new NotImplementedException();
    }

    public Task<UdapRegisterRequest?> BuildRequestBody(RawSoftwareStatementAndHeader? request, string signingAlgorithm)
    {
        throw new NotImplementedException();
    }

    public Task<CertificateStatusViewModel?> ValidateCertificate(string password)
    {
        throw new NotImplementedException();
    }

    public Task<CertificateStatusViewModel?> ClientCertificateLoadStatus()
    {
        throw new NotImplementedException();
    }

    public Task<CertificateStatusViewModel?> LoadTestCertificate(string certificateName)
    {
        throw new NotImplementedException();
    }
}
